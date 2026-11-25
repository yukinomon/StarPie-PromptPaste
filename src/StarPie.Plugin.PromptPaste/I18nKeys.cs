using StarPie.Plugin;

namespace StarPie.Plugin.PromptPaste;

/// <summary>
/// 插件词条短键与中英文文案。宿主会自动加上 <c>plugin.io.github.yukinomon.promptpaste.</c> 前缀。
/// 只含常量与注册方法，不持有任何宿主对象。
/// </summary>
internal static class I18nKeys
{
    internal const string ActionName = "paste.name";
    internal const string ActionDescription = "paste.description";

    internal const string TemplateLabel = "param.template.label";
    internal const string TemplateHelp = "param.template.help";
    internal const string RestoreLabel = "param.restore.label";
    internal const string RestoreHelp = "param.restore.help";
    internal const string DelayLabel = "param.delay.label";
    internal const string DelayHelp = "param.delay.help";

    internal const string ErrorTemplateEmpty = "error.template.empty";
    internal const string ErrorTemplateTooLarge = "error.template.toolarge";
    internal const string ErrorRestoreInvalid = "error.restore.invalid";
    internal const string ErrorDelayInvalid = "error.delay.invalid";
    internal const string ErrorCapability = "error.capability";
    internal const string ErrorClipboardWrite = "error.clipboard.write";
    internal const string ErrorPaste = "error.paste";
    internal const string ErrorCancelled = "error.cancelled";
    internal const string ErrorUnexpected = "error.unexpected";

    internal const string NotifyTitle = "notify.title";
    internal const string NotifySelectionEmpty = "notify.selection.empty";

    internal const string PreviewEmpty = "preview.empty";

    // 中文兜底文案（宿主找不到词条时使用），同时作为 Register 的中文值
    internal const string ZhActionName = "粘贴提示词";
    internal const string ZhActionDescription = "把预设的提示词模板一键粘贴到当前窗口，支持 {clipboard}、{selection}、{date}、{time} 变量。";
    internal const string ZhTemplateLabel = "提示词模板";
    internal const string ZhTemplateHelp = "可用变量：{clipboard} 触发前的剪贴板文本；{selection} 当前选中的文本；{date} 日期（yyyy-MM-dd）；{time} 时间（HH:mm）。模板不超过 8 KiB（UTF-8）。";
    internal const string ZhRestoreLabel = "粘贴后恢复原剪贴板";
    internal const string ZhRestoreHelp = "只恢复纯文本；原剪贴板是图片、文件等内容时会安全跳过。";
    internal const string ZhDelayLabel = "恢复延迟";
    internal const string ZhDelayHelp = "模拟 Ctrl+V 后等待多久再恢复原剪贴板，范围 50～2000 毫秒。目标程序粘贴较慢时可调大。";
    internal const string ZhErrorTemplateEmpty = "提示词模板不能为空。";
    internal const string ZhErrorTemplateTooLarge = "提示词模板过长：UTF-8 编码 {0} 字节，上限 {1} 字节。";
    internal const string ZhErrorRestoreInvalid = "「粘贴后恢复原剪贴板」只接受 true 或 false。";
    internal const string ZhErrorDelayInvalid = "恢复延迟必须是 {0}～{1} 之间的整数（毫秒）。";
    internal const string ZhErrorCapability = "插件缺少「剪贴板」或「模拟输入」能力声明，无法粘贴。请重新安装插件。";
    internal const string ZhErrorClipboardWrite = "写入剪贴板失败，未执行粘贴。";
    internal const string ZhErrorPaste = "发送 Ctrl+V 失败，未能粘贴。";
    internal const string ZhErrorCancelled = "操作已取消。";
    internal const string ZhErrorUnexpected = "粘贴提示词时出错，详情见插件日志。";
    internal const string ZhNotifyTitle = "提示词快贴";
    internal const string ZhNotifySelectionEmpty = "未检测到选中文本，已跳过 {selection}";
    internal const string ZhPreviewEmpty = "（未填写提示词模板）";

    internal static void RegisterAll(II18nRegistry i18n)
    {
        i18n.Register(ActionName, ZhActionName, "Paste Prompt");
        i18n.Register(ActionDescription, ZhActionDescription,
            "Pastes a preset prompt template into the active window. Supports {clipboard}, {selection}, {date} and {time}.");

        i18n.Register(TemplateLabel, ZhTemplateLabel, "Prompt template");
        i18n.Register(TemplateHelp, ZhTemplateHelp,
            "Variables: {clipboard} clipboard text before triggering; {selection} currently selected text; {date} date (yyyy-MM-dd); {time} time (HH:mm). Up to 8 KiB (UTF-8).");
        i18n.Register(RestoreLabel, ZhRestoreLabel, "Restore clipboard after pasting");
        i18n.Register(RestoreHelp, ZhRestoreHelp,
            "Only plain text is restored; images, files and other non-text content are skipped safely.");
        i18n.Register(DelayLabel, ZhDelayLabel, "Restore delay");
        i18n.Register(DelayHelp, ZhDelayHelp,
            "How long to wait after Ctrl+V before restoring the clipboard, 50–2000 ms. Increase it if the target app pastes slowly.");

        i18n.Register(ErrorTemplateEmpty, ZhErrorTemplateEmpty, "The prompt template cannot be empty.");
        i18n.Register(ErrorTemplateTooLarge, ZhErrorTemplateTooLarge,
            "The prompt template is too long: {0} bytes in UTF-8, the limit is {1} bytes.");
        i18n.Register(ErrorRestoreInvalid, ZhErrorRestoreInvalid,
            "\"Restore clipboard after pasting\" only accepts true or false.");
        i18n.Register(ErrorDelayInvalid, ZhErrorDelayInvalid,
            "Restore delay must be a whole number between {0} and {1} ms.");
        i18n.Register(ErrorCapability, ZhErrorCapability,
            "The plugin is missing the Clipboard or InputSimulation capability, so it cannot paste. Please reinstall it.");
        i18n.Register(ErrorClipboardWrite, ZhErrorClipboardWrite, "Failed to write to the clipboard; nothing was pasted.");
        i18n.Register(ErrorPaste, ZhErrorPaste, "Failed to send Ctrl+V; nothing was pasted.");
        i18n.Register(ErrorCancelled, ZhErrorCancelled, "The operation was cancelled.");
        i18n.Register(ErrorUnexpected, ZhErrorUnexpected,
            "Something went wrong while pasting the prompt. See the plugin log for details.");

        i18n.Register(NotifyTitle, ZhNotifyTitle, "Prompt Paste");
        i18n.Register(NotifySelectionEmpty, ZhNotifySelectionEmpty, "No selected text detected; skipped {selection}");

        i18n.Register(PreviewEmpty, ZhPreviewEmpty, "(No prompt template yet)");
    }
}
