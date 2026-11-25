using System;
using System.Globalization;
using System.Text;

namespace StarPie.Plugin.PromptPaste;

/// <summary>
/// 提示词模板渲染引擎。纯 BCL、无副作用、不依赖宿主，可直接被单元测试工程链接编译。
/// <para>
/// 采用<b>单遍扫描</b>：从左到右找 <c>{</c>，命中 4 个内置占位符之一就写入替换值并跳过整个占位符；
/// 替换值本身永远不会再被扫描，因此用户剪贴板 / 选区里恰好出现的 <c>{date}</c> 等字样会原样保留。
/// 未识别的 <c>{xxx}</c> 与孤立花括号按字面输出。占位符区分大小写。
/// </para>
/// </summary>
internal static class TemplateRenderer
{
    internal const string ClipboardToken = "{clipboard}";
    internal const string SelectionToken = "{selection}";
    internal const string DateToken = "{date}";
    internal const string TimeToken = "{time}";

    internal const string DateFormat = "yyyy-MM-dd";
    internal const string TimeFormat = "HH:mm";

    /// <summary>列表副标题 / 预览截取的字数（按用户感知字符计，不会切断 emoji）。</summary>
    internal const int DefaultPreviewLength = 20;

    private const string Ellipsis = "…";

    private static readonly char[] LineBreakChars = { '\r', '\n' };

    /// <summary>模板是否包含 <c>{selection}</c>。</summary>
    internal static bool ContainsSelection(string? template) =>
        template is not null && template.Contains(SelectionToken, StringComparison.Ordinal);

    /// <summary>模板是否包含 <c>{clipboard}</c>。</summary>
    internal static bool ContainsClipboard(string? template) =>
        template is not null && template.Contains(ClipboardToken, StringComparison.Ordinal);

    /// <summary>
    /// 这次执行是否需要模拟 Ctrl+C 抓取选区：模板里写了 <c>{selection}</c>，
    /// 或者用户选了「末尾附带：当前选中的文字」。
    /// </summary>
    internal static bool RequiresSelection(string? template, AppendMode appendMode) =>
        appendMode == AppendMode.Selection || ContainsSelection(template);

    /// <summary>
    /// 在正文末尾拼接一段附带内容。空串 / 纯空白视为「没有内容」，原样返回，不产生空行；
    /// 拼接前先去掉附带内容开头的换行，避免与分隔换行叠成空行。
    /// 返回的文本尚未做行尾统一（由 <see cref="NormalizeLineEndings"/> 负责）。
    /// </summary>
    internal static string AppendTail(string body, string? chunk)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (string.IsNullOrWhiteSpace(chunk))
        {
            return body;
        }

        string tail = chunk.TrimStart('\r', '\n');
        if (tail.Length == 0)
        {
            return body;
        }

        // 正文已经以换行结尾时不再多插一个空行
        bool bodyEndsWithBreak =
            body.EndsWith("\r\n", StringComparison.Ordinal) ||
            body.EndsWith("\n", StringComparison.Ordinal) ||
            body.EndsWith("\r", StringComparison.Ordinal);

        return bodyEndsWithBreak ? body + tail : body + "\r\n" + tail;
    }

    /// <summary>
    /// 渲染 + 按 <paramref name="appendMode"/> 附带 + 统一 CRLF 的完整流程（纯函数，便于单元测试）。
    /// <para>
    /// 正文里已经手写了 <c>{selection}</c> / <c>{clipboard}</c> 时，同一份内容已经进入正文，
    /// 不再在末尾重复拼接，避免同一段文字出现两次。
    /// </para>
    /// </summary>
    internal static string ComposeBody(
        string template, string? clipboard, string? selection, DateTime now, AppendMode appendMode)
    {
        ArgumentNullException.ThrowIfNull(template);

        string body = Render(template, clipboard, selection, now);

        if (appendMode == AppendMode.Selection && !ContainsSelection(template))
        {
            body = AppendTail(body, selection);
        }
        else if (appendMode == AppendMode.Clipboard && !ContainsClipboard(template))
        {
            body = AppendTail(body, clipboard);
        }

        return NormalizeLineEndings(body);
    }

    /// <summary>
    /// 一次性替换全部内置占位符。
    /// </summary>
    /// <param name="template">模板正文。</param>
    /// <param name="clipboard">触发瞬间快照的原剪贴板纯文本；无文本时传空串。</param>
    /// <param name="selection">抓取到的选中文本；未抓到时传空串。</param>
    /// <param name="now">本地当前时间（由调用方传入，便于测试）。</param>
    internal static string Render(string template, string? clipboard, string? selection, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(template);
        clipboard ??= string.Empty;
        selection ??= string.Empty;

        int brace = template.IndexOf('{');
        if (brace < 0)
        {
            return template;
        }

        string date = now.ToString(DateFormat, CultureInfo.InvariantCulture);
        string time = now.ToString(TimeFormat, CultureInfo.InvariantCulture);

        var builder = new StringBuilder(template.Length + clipboard.Length + selection.Length + 16);
        int cursor = 0;

        while (brace >= 0)
        {
            builder.Append(template, cursor, brace - cursor);

            string? value = null;
            int consumed = 0;

            if (StartsWithAt(template, brace, ClipboardToken))
            {
                value = clipboard;
                consumed = ClipboardToken.Length;
            }
            else if (StartsWithAt(template, brace, SelectionToken))
            {
                value = selection;
                consumed = SelectionToken.Length;
            }
            else if (StartsWithAt(template, brace, DateToken))
            {
                value = date;
                consumed = DateToken.Length;
            }
            else if (StartsWithAt(template, brace, TimeToken))
            {
                value = time;
                consumed = TimeToken.Length;
            }

            if (value is null)
            {
                // 不是内置占位符：花括号按字面输出，从下一个字符继续找
                builder.Append('{');
                cursor = brace + 1;
            }
            else
            {
                builder.Append(value);
                cursor = brace + consumed;
            }

            brace = cursor < template.Length ? template.IndexOf('{', cursor) : -1;
        }

        if (cursor < template.Length)
        {
            builder.Append(template, cursor, template.Length - cursor);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 把 <c>\n</c>、<c>\r</c>、<c>\r\n</c> 统一为 Windows 剪贴板标准的 <c>\r\n</c>，
    /// 保证记事本等传统控件里换行正确。
    /// </summary>
    internal static string NormalizeLineEndings(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.IndexOfAny(LineBreakChars) < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 16);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                builder.Append("\r\n");
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (c == '\n')
            {
                builder.Append("\r\n");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 生成列表副标题：截取模板前 <paramref name="maxTextElements"/> 个字符并把换行 / 连续空白压成一个空格。
    /// 纯内存操作，且只扫描模板开头的有限长度，满足宿主对 <c>Preview()</c> 微秒级的要求。
    /// 模板为空白时返回空串（由调用方换成本地化的占位文案）。
    /// </summary>
    internal static string BuildPreview(string? template, int maxTextElements = DefaultPreviewLength)
    {
        if (string.IsNullOrWhiteSpace(template) || maxTextElements <= 0)
        {
            return string.Empty;
        }

        // 一个用户感知字符最多由若干 UTF-16 单元组成，扫描到这里足够截出 maxTextElements 个
        int scanLimit = Math.Min(template.Length, (maxTextElements * 8) + 8);
        var flat = new StringBuilder(Math.Min(scanLimit, 256));
        bool pendingSpace = false;

        for (int i = 0; i < scanLimit; i++)
        {
            char c = template[i];
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = flat.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                flat.Append(' ');
                pendingSpace = false;
            }

            flat.Append(c);
        }

        bool inputTruncated = scanLimit < template.Length;
        if (inputTruncated && flat.Length > 0 && char.IsHighSurrogate(flat[flat.Length - 1]))
        {
            flat.Length--;
        }

        string collapsed = flat.ToString();
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(collapsed);
        int count = 0;
        while (elements.MoveNext())
        {
            if (count == maxTextElements)
            {
                return collapsed.Substring(0, elements.ElementIndex).TrimEnd() + Ellipsis;
            }

            count++;
        }

        return inputTruncated ? collapsed + Ellipsis : collapsed;
    }

    private static bool StartsWithAt(string text, int index, string token) =>
        text.Length - index >= token.Length &&
        string.CompareOrdinal(text, index, token, 0, token.Length) == 0;
}
