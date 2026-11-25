using System;
using System.Globalization;
using System.Text;

namespace StarPie.Plugin.PromptPaste;

/// <summary>
/// 参数键、默认值与取值规则。纯 BCL，供动作校验与单元测试共用，保证「声明 / 校验 / 执行」三处口径一致。
/// </summary>
internal static class ParameterRules
{
    internal const string TemplateKey = "template";
    internal const string RestoreClipboardKey = "restoreClipboard";
    internal const string RestoreDelayKey = "restoreDelayMs";
    internal const string AppendModeKey = "appendMode";

    /// <summary>模板 UTF-8 字节上限（StarPie 单参数 8 KiB 上限）。</summary>
    internal const int MaxTemplateUtf8Bytes = 8 * 1024;

    internal const bool DefaultRestoreClipboard = true;
    internal const int DefaultRestoreDelayMs = 200;
    internal const int MinRestoreDelayMs = 50;
    internal const int MaxRestoreDelayMs = 2000;

    /// <summary>模板是否在 UTF-8 字节上限之内。</summary>
    internal static bool IsTemplateWithinLimit(string template, out int utf8Bytes)
    {
        ArgumentNullException.ThrowIfNull(template);
        utf8Bytes = Encoding.UTF8.GetByteCount(template);
        return utf8Bytes <= MaxTemplateUtf8Bytes;
    }

    /// <summary>
    /// 解析「恢复延迟」。未填写视为默认值 200；必须是 50~2000 之间的整数。
    /// 用不变文化解析，并接受宿主可能写入的 <c>300.0</c> 这类整数值字面量。
    /// </summary>
    internal static bool TryParseRestoreDelay(string? raw, out int milliseconds)
    {
        milliseconds = DefaultRestoreDelayMs;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || !double.IsFinite(value)
            || value != Math.Floor(value)
            || value < MinRestoreDelayMs
            || value > MaxRestoreDelayMs)
        {
            return false;
        }

        milliseconds = (int)value;
        return true;
    }

    // ---- appendMode（粘贴时顺便附带）----------------------------------------

    /// <summary>「粘贴时顺便附带」的选项值（与 ParameterOption.Value 完全一致）。</summary>
    internal const string AppendModeNone = "none";
    internal const string AppendModeSelection = "selection";
    internal const string AppendModeClipboard = "clipboard";

    internal const AppendMode DefaultAppendMode = AppendMode.None;

    /// <summary>
    /// 解析「粘贴时顺便附带」。未填写视为 <see cref="AppendMode.None"/>；
    /// 只接受 none / selection / clipboard（忽略大小写与首尾空白），其余一律视为非法。
    /// </summary>
    internal static bool TryParseAppendMode(string? raw, out AppendMode mode)
    {
        mode = DefaultAppendMode;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case AppendModeNone:
                return true;
            case AppendModeSelection:
                mode = AppendMode.Selection;
                return true;
            case AppendModeClipboard:
                mode = AppendMode.Clipboard;
                return true;
            default:
                return false;
        }
    }

    /// <summary>解析「粘贴后恢复原剪贴板」。未填写视为默认值 true。</summary>
    internal static bool TryParseRestoreFlag(string? raw, out bool restore)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            restore = DefaultRestoreClipboard;
            return true;
        }

        return bool.TryParse(raw.Trim(), out restore);
    }
}

/// <summary>「粘贴时顺便附带」的三种模式。</summary>
internal enum AppendMode
{
    /// <summary>不附带，只粘贴提示词正文。</summary>
    None = 0,

    /// <summary>在正文末尾附带当前选中的文字。</summary>
    Selection = 1,

    /// <summary>在正文末尾附带触发前的剪贴板纯文本。</summary>
    Clipboard = 2,
}
