using System.Globalization;
using StarPie.Plugin;

namespace StarPie.Plugin.PromptPaste;

/// <summary>
/// 「粘贴提示词」动作：把提示词模板渲染后粘贴到当前前台窗口，并按需恢复原剪贴板纯文本。
/// <para>
/// 串行类动作（<see cref="ActionKind.Sequential"/>）：会与前台窗口、输入、剪贴板交互，
/// 由宿主在唯一的动作线程上顺序执行。每次执行的状态都放在局部变量里，不持有静态状态。
/// </para>
/// </summary>
internal sealed class PastePromptAction : IActionContribution
{
    /// <summary>动作短 ID。完整 ID：io.github.yukinomon.promptpaste.pastePrompt。发布后不得变更。</summary>
    internal const string ActionId = "pastePrompt";

    private const int TimeoutSeconds = 5;

    private readonly IPluginContext _context;
    private readonly ClipboardGuard _clipboard;
    private readonly IReadOnlyList<ParameterField> _parameters;

    internal PastePromptAction(IPluginContext context, string? iconKey)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _clipboard = new ClipboardGuard(context.Host, context.Log);

        Descriptor = new ActionDescriptor
        {
            Id = ActionId,
            DisplayName = I18nKeys.ZhActionName,
            DisplayNameKey = I18nKeys.ActionName,
            Description = I18nKeys.ZhActionDescription,
            DescriptionKey = I18nKeys.ActionDescription,
            IconKey = iconKey,
            Kind = ActionKind.Sequential,
            TimeoutSeconds = TimeoutSeconds,
        };

        _parameters = new List<ParameterField>
        {
            new()
            {
                Key = ParameterRules.TemplateKey,
                Label = I18nKeys.ZhTemplateLabel,
                LabelKey = I18nKeys.TemplateLabel,
                Type = ParameterFieldType.MultilineText,
                Required = true,
                // 刻意为 null 而不是 ""：必填项的「空默认值」若写成 ""，
                // 自检 [3b] ③「按声明默认值填充必须全部通过」会把它判成不合法默认值。
                DefaultValue = null,
                MaxLength = ParameterRules.MaxTemplateUtf8Bytes,
                HelpText = I18nKeys.ZhTemplateHelp,
                HelpTextKey = I18nKeys.TemplateHelp,
            },
            new()
            {
                Key = ParameterRules.RestoreClipboardKey,
                Label = I18nKeys.ZhRestoreLabel,
                LabelKey = I18nKeys.RestoreLabel,
                Type = ParameterFieldType.Bool,
                Required = false,
                DefaultValue = "true",
                HelpText = I18nKeys.ZhRestoreHelp,
                HelpTextKey = I18nKeys.RestoreHelp,
            },
            new()
            {
                Key = ParameterRules.RestoreDelayKey,
                Label = I18nKeys.ZhDelayLabel,
                LabelKey = I18nKeys.DelayLabel,
                Type = ParameterFieldType.Number,
                Required = false,
                DefaultValue = ParameterRules.DefaultRestoreDelayMs.ToString(CultureInfo.InvariantCulture),
                Min = ParameterRules.MinRestoreDelayMs,
                Max = ParameterRules.MaxRestoreDelayMs,
                Unit = "ms",
                HelpText = I18nKeys.ZhDelayHelp,
                HelpTextKey = I18nKeys.DelayHelp,
            },
        }.AsReadOnly();
    }

    public ActionDescriptor Descriptor { get; }

    public IReadOnlyList<ParameterField> Parameters => _parameters;

    public string? Validate(IReadOnlyDictionary<string, string> parameters)
    {
        string? template = Read(parameters, ParameterRules.TemplateKey);
        if (string.IsNullOrWhiteSpace(template))
        {
            return T(I18nKeys.ErrorTemplateEmpty, I18nKeys.ZhErrorTemplateEmpty);
        }

        if (!ParameterRules.IsTemplateWithinLimit(template, out int utf8Bytes))
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(I18nKeys.ErrorTemplateTooLarge, I18nKeys.ZhErrorTemplateTooLarge),
                utf8Bytes,
                ParameterRules.MaxTemplateUtf8Bytes);
        }

        if (!ParameterRules.TryParseRestoreFlag(Read(parameters, ParameterRules.RestoreClipboardKey), out _))
        {
            return T(I18nKeys.ErrorRestoreInvalid, I18nKeys.ZhErrorRestoreInvalid);
        }

        if (!ParameterRules.TryParseRestoreDelay(Read(parameters, ParameterRules.RestoreDelayKey), out _))
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(I18nKeys.ErrorDelayInvalid, I18nKeys.ZhErrorDelayInvalid),
                ParameterRules.MinRestoreDelayMs,
                ParameterRules.MaxRestoreDelayMs);
        }

        return null;
    }

    public string Preview(IReadOnlyDictionary<string, string> parameters)
    {
        // 纯内存摘要：只看模板开头一小段，禁止任何 IO
        string preview = TemplateRenderer.BuildPreview(Read(parameters, ParameterRules.TemplateKey));
        return preview.Length > 0 ? preview : T(I18nKeys.PreviewEmpty, I18nKeys.ZhPreviewEmpty);
    }

    public async Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
    {
        ClipboardSnapshot? snapshot = null;
        uint? writtenSequence = null;
        bool restoreEnabled = ParameterRules.DefaultRestoreClipboard;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ① 读取并校验参数（宿主执行前已校验过一次，这里兜底）
            string? error = Validate(input.Parameters);
            if (error is not null)
            {
                return ActionResult.Fail(error);
            }

            if (!_context.Info.HasCapability(PluginCapability.Clipboard) ||
                !_context.Info.HasCapability(PluginCapability.InputSimulation))
            {
                return ActionResult.Fail(T(I18nKeys.ErrorCapability, I18nKeys.ZhErrorCapability));
            }

            string template = input.Parameter(ParameterRules.TemplateKey)!;
            ParameterRules.TryParseRestoreFlag(input.Parameter(ParameterRules.RestoreClipboardKey), out restoreEnabled);
            ParameterRules.TryParseRestoreDelay(input.Parameter(ParameterRules.RestoreDelayKey), out int restoreDelayMs);

            // ② 先快照原剪贴板（必须早于选区抓取，否则 {clipboard} 会被 Ctrl+C 覆盖）
            snapshot = _clipboard.TakeSnapshot();

            // ③ 模板含 {selection} 才模拟 Ctrl+C 抓取选区
            string selection = string.Empty;
            bool selectionMissing = false;
            if (TemplateRenderer.ContainsSelection(template))
            {
                selection = await _clipboard.CaptureSelectionAsync(snapshot, cancellationToken).ConfigureAwait(false);
                selectionMissing = selection.Length == 0;
            }

            // ④ 单遍替换 4 个占位符，并统一为 CRLF 换行
            string text = TemplateRenderer.NormalizeLineEndings(
                TemplateRenderer.Render(template, snapshot.Text, selection, DateTime.Now));

            cancellationToken.ThrowIfCancellationRequested();

            // ⑤ 写入剪贴板并模拟 Ctrl+V
            if (!_clipboard.TryWriteText(text, out uint sequence))
            {
                RollBack(snapshot, restoreEnabled, writtenSequence: null);
                return ActionResult.Fail(T(I18nKeys.ErrorClipboardWrite, I18nKeys.ZhErrorClipboardWrite));
            }

            writtenSequence = sequence;

            if (!_clipboard.SendPaste())
            {
                RollBack(snapshot, restoreEnabled, writtenSequence);
                return ActionResult.Fail(T(I18nKeys.ErrorPaste, I18nKeys.ZhErrorPaste));
            }

            if (selectionMissing)
            {
                NotifyEmptySelection();
            }

            // ⑥ 延时恢复原剪贴板纯文本
            if (restoreEnabled)
            {
                switch (snapshot.Kind)
                {
                    case ClipboardContentKind.Text:
                        await Task.Delay(restoreDelayMs, cancellationToken).ConfigureAwait(false);
                        _clipboard.TryRestore(snapshot, writtenSequence);
                        break;

                    case ClipboardContentKind.NonText:
                        _context.Log.Info("原剪贴板为图片、文件列表等非文本内容，无法以纯文本还原，已安全跳过恢复。");
                        break;

                    default:
                        _context.Log.Debug("原剪贴板为空，无需恢复。");
                        break;
                }
            }

            return ActionResult.Ok();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 超时或插件停用：尽力把原剪贴板写回，避免用户剪贴板被提示词长期占用
            RollBack(snapshot, restoreEnabled, writtenSequence);
            _context.Log.Warn("动作被取消（超时或插件停用），已尝试恢复原剪贴板。");
            return ActionResult.Fail(T(I18nKeys.ErrorCancelled, I18nKeys.ZhErrorCancelled));
        }
        catch (Exception ex)
        {
            RollBack(snapshot, restoreEnabled, writtenSequence);
            _context.Log.Error("粘贴提示词失败。", ex);
            return ActionResult.Fail(T(I18nKeys.ErrorUnexpected, I18nKeys.ZhErrorUnexpected));
        }
    }

    private void RollBack(ClipboardSnapshot? snapshot, bool restoreEnabled, uint? writtenSequence)
    {
        if (snapshot is null || !restoreEnabled)
        {
            return;
        }

        try
        {
            _clipboard.TryRestore(snapshot, writtenSequence);
        }
        catch (Exception ex)
        {
            _context.Log.Warn($"回滚原剪贴板失败：{ex.Message}");
        }
    }

    private void NotifyEmptySelection()
    {
        try
        {
            // 宿主通知是非阻塞的托盘气泡；没有托盘时宿主自动降级为日志
            _context.Notify.Notify(
                T(I18nKeys.NotifyTitle, I18nKeys.ZhNotifyTitle),
                T(I18nKeys.NotifySelectionEmpty, I18nKeys.ZhNotifySelectionEmpty));
        }
        catch (Exception ex)
        {
            _context.Log.Warn($"发送空选区提醒失败：{ex.Message}");
        }
    }

    private string T(string key, string fallback) => _context.I18n.T(key, fallback);

    private static string? Read(IReadOnlyDictionary<string, string>? parameters, string key) =>
        parameters is not null && parameters.TryGetValue(key, out string? value) ? value : null;
}
