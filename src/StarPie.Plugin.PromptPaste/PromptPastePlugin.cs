using StarPie.Plugin;

namespace StarPie.Plugin.PromptPaste;

/// <summary>
/// 提示词快贴（Prompt Paste）插件入口。
/// <para>
/// 契约：public 无参构造且无副作用；<see cref="Initialize"/> 只做注册；
/// <see cref="Shutdown"/> 幂等、不抛异常，并释放全部注册凭据。不在静态字段中持有 <see cref="IPluginContext"/>。
/// </para>
/// </summary>
public sealed class PromptPastePlugin : IStarPiePlugin
{
    internal const string IconShortKey = "promptpaste";

    /// <summary>
    /// 剪贴板 + 闪电造型（24×24 视口，只用 M/H/V/L/Z 简单命令）。
    /// 外框与内框方向相反、闪电与外框方向相同，在 EvenOdd 与 NonZero 两种填充规则下都显示为
    /// 「镂空剪贴板 + 实心闪电」。
    /// </summary>
    internal const string IconPathData =
        "M5 4 H19 V22 H5 Z M7 7 V20 H17 V7 Z M9 2 H15 V4 H9 Z M13 8 L12 12 H15 L11 19 L12 14 H9 Z";

    private readonly List<IDisposable> _registrations = new();
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;

        I18nKeys.RegisterAll(context.I18n);

        string? iconKey = null;
        try
        {
            iconKey = context.Icons.RegisterSvg(IconShortKey, IconPathData);
        }
        catch (Exception ex)
        {
            context.Log.Warn($"图标注册失败，动作将使用宿主默认图标：{ex.Message}");
        }

        _registrations.Add(context.Actions.Register(new PastePromptAction(context, iconKey)));

        context.Log.Info($"提示词快贴 v{context.Me.Version} 初始化完成。");
    }

    public void Shutdown()
    {
        for (int i = _registrations.Count - 1; i >= 0; i--)
        {
            try
            {
                _registrations[i].Dispose();
            }
            catch
            {
                // 必须不抛异常；宿主也会兜底撤销贡献点
            }
        }

        _registrations.Clear();

        try
        {
            _context?.Log.Info("提示词快贴已停用。");
        }
        catch
        {
        }

        _context = null;
    }
}
