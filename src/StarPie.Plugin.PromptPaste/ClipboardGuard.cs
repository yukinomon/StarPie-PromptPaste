using System.Diagnostics;
using System.Runtime.InteropServices;
using StarPie.Plugin;

namespace StarPie.Plugin.PromptPaste;

/// <summary>原剪贴板内容的分类。</summary>
internal enum ClipboardContentKind
{
    /// <summary>剪贴板为空，无需恢复。</summary>
    Empty = 0,

    /// <summary>含纯文本，可按纯文本恢复。</summary>
    Text = 1,

    /// <summary>图片、文件列表等非文本内容，无法以纯文本还原，安全跳过恢复。</summary>
    NonText = 2,
}

/// <summary>触发瞬间对原剪贴板做的快照。</summary>
internal sealed class ClipboardSnapshot
{
    public ClipboardContentKind Kind { get; init; }

    /// <summary>原剪贴板纯文本（无文本时为空串），同时作为 <c>{clipboard}</c> 的取值。</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>快照时刻的剪贴板序号（Win32 GetClipboardSequenceNumber；不可用时为 0）。</summary>
    public uint Sequence { get; init; }

    public bool IsRestorable => Kind == ClipboardContentKind.Text;
}

/// <summary>
/// 剪贴板读写、选区抓取与恢复保护。
/// <para>
/// 读写文本、发送 Ctrl+C / Ctrl+V 全部走宿主 <see cref="IHostActionInvoker"/>（宿主已处理 STA 线程、
/// 修饰键时延与重试）；宿主没有提供的「格式探测」与「剪贴板序号」两项只读查询，以 Win32 P/Invoke 补齐。
/// 不持有任何静态状态，实例随动作对象一同释放。
/// </para>
/// </summary>
internal sealed class ClipboardGuard
{
    /// <summary>模拟 Ctrl+C 后等待选区写入剪贴板的最长时间。</summary>
    internal const int SelectionTimeoutMs = 100;

    /// <summary>等待期间的轮询间隔。</summary>
    internal const int SelectionPollIntervalMs = 10;

    private const string CopyHotkey = "Ctrl+C";
    private const string PasteHotkey = "Ctrl+V";

    private readonly IHostActionInvoker _host;
    private readonly IPluginLogger _log;

    internal ClipboardGuard(IHostActionInvoker host, IPluginLogger log)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// 对原剪贴板做状态检测与快照。必须在抓取选区（模拟 Ctrl+C）之前调用，
    /// 否则 <c>{clipboard}</c> 会被选区覆盖。
    /// </summary>
    internal ClipboardSnapshot TakeSnapshot()
    {
        uint sequence = NativeClipboard.GetSequenceNumber();
        bool hasFileList = NativeClipboard.IsFormatAvailable(NativeClipboard.CF_HDROP);
        string? text = _host.GetClipboardText();

        ClipboardContentKind kind;
        if (hasFileList)
        {
            // 文件列表即使附带文本，以纯文本写回也不等于原内容（资源管理器里将无法再粘贴文件）
            kind = ClipboardContentKind.NonText;
        }
        else if (text is not null)
        {
            kind = ClipboardContentKind.Text;
        }
        else
        {
            kind = NativeClipboard.CountFormats() > 0
                ? ClipboardContentKind.NonText
                : ClipboardContentKind.Empty;
        }

        if (kind == ClipboardContentKind.Text && NativeClipboard.HasImage())
        {
            _log.Info("原剪贴板除文本外还含图片等富格式；恢复时只能还原其中的纯文本部分。");
        }

        return new ClipboardSnapshot
        {
            Kind = kind,
            Text = text ?? string.Empty,
            Sequence = sequence,
        };
    }

    /// <summary>
    /// 模拟一次 Ctrl+C 并读取当前选中文本。以剪贴板序号「标记」写入前后的状态，
    /// 序号在 <see cref="SelectionTimeoutMs"/> 内没有变化即视为没有选中文本，返回空串。
    /// </summary>
    /// <param name="snapshot">先前的原剪贴板快照（序号不可用时用其文本做变化判定）。</param>
    internal async Task<string> CaptureSelectionAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken)
    {
        uint before = NativeClipboard.GetSequenceNumber();
        bool sequenceAvailable = before != 0;

        if (!_host.SendHotkey(CopyHotkey))
        {
            _log.Warn("模拟 Ctrl+C 未能下发，{selection} 按空文本处理。");
            return string.Empty;
        }

        var stopwatch = Stopwatch.StartNew();
        bool changed = false;

        while (true)
        {
            await Task.Delay(SelectionPollIntervalMs, cancellationToken).ConfigureAwait(false);

            if (sequenceAvailable)
            {
                if (!changed && NativeClipboard.GetSequenceNumber() != before)
                {
                    changed = true;
                }

                if (changed)
                {
                    string? text = _host.GetClipboardText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
            else
            {
                // 拿不到序号（极少见的会话权限受限场景）时，退化为「文本是否变化」判定
                string? text = _host.GetClipboardText();
                if (!string.IsNullOrEmpty(text) && !string.Equals(text, snapshot.Text, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            if (stopwatch.ElapsedMilliseconds >= SelectionTimeoutMs)
            {
                break;
            }
        }

        _log.Info(changed
            ? "Ctrl+C 后剪贴板已更新但不含文本，{selection} 按空文本处理。"
            : "未检测到选中文本（Ctrl+C 后剪贴板没有变化），{selection} 按空文本处理。");
        return string.Empty;
    }

    /// <summary>把最终文本写入剪贴板，并记下写入后的剪贴板序号用于后续的恢复保护。</summary>
    internal bool TryWriteText(string text, out uint sequenceAfterWrite)
    {
        bool ok = _host.SetClipboardText(text);
        sequenceAfterWrite = NativeClipboard.GetSequenceNumber();
        return ok;
    }

    /// <summary>向当前前台窗口模拟 Ctrl+V。</summary>
    internal bool SendPaste() => _host.SendHotkey(PasteHotkey);

    /// <summary>
    /// 把原剪贴板纯文本写回。
    /// </summary>
    /// <param name="snapshot">触发时的快照；非纯文本快照一律不恢复。</param>
    /// <param name="ourWriteSequence">
    /// 本插件写入最终文本后的序号。非空时，若当前序号已不同（用户或其它程序在等待期间又复制了新内容），
    /// 则跳过恢复，避免覆盖新内容；为空表示尚未写入最终文本（失败回滚路径），此时只要剪贴板被改动过就写回。
    /// </param>
    internal bool TryRestore(ClipboardSnapshot snapshot, uint? ourWriteSequence)
    {
        if (!snapshot.IsRestorable)
        {
            return false;
        }

        uint current = NativeClipboard.GetSequenceNumber();
        if (ourWriteSequence is uint written)
        {
            if (written != 0 && current != 0 && current != written)
            {
                _log.Info("等待期间剪贴板已被其它程序或用户更新，已跳过恢复，避免覆盖新内容。");
                return false;
            }
        }
        else if (current != 0 && current == snapshot.Sequence)
        {
            // 剪贴板自快照以来没被动过，无需写回
            return true;
        }

        if (!_host.SetClipboardText(snapshot.Text))
        {
            _log.Warn("恢复原剪贴板文本失败（宿主写入剪贴板返回 false）。");
            return false;
        }

        return true;
    }

    /// <summary>Win32 剪贴板只读查询（不打开剪贴板、不改变内容）。</summary>
    private static class NativeClipboard
    {
        internal const uint CF_BITMAP = 2;
        internal const uint CF_DIB = 8;
        internal const uint CF_ENHMETAFILE = 14;
        internal const uint CF_HDROP = 15;
        internal const uint CF_DIBV5 = 17;

        internal static uint GetSequenceNumber()
        {
            try
            {
                return GetClipboardSequenceNumber();
            }
            catch
            {
                return 0;
            }
        }

        internal static bool IsFormatAvailable(uint format)
        {
            try
            {
                return IsClipboardFormatAvailable(format);
            }
            catch
            {
                return false;
            }
        }

        internal static int CountFormats()
        {
            try
            {
                return CountClipboardFormats();
            }
            catch
            {
                return 0;
            }
        }

        internal static bool HasImage() =>
            IsFormatAvailable(CF_DIB) || IsFormatAvailable(CF_DIBV5) ||
            IsFormatAvailable(CF_BITMAP) || IsFormatAvailable(CF_ENHMETAFILE);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int CountClipboardFormats();
    }
}
