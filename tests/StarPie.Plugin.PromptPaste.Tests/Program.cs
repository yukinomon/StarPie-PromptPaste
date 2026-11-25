using System.Text;
using StarPie.Plugin.PromptPaste;

Console.OutputEncoding = Encoding.UTF8;

int passed = 0;
int failed = 0;

void Check(string name, bool condition, string? detail = null)
{
    if (condition)
    {
        passed++;
        Console.WriteLine($"  PASS  {name}");
    }
    else
    {
        failed++;
        Console.WriteLine($"  FAIL  {name}{(detail is null ? "" : " — " + detail)}");
    }
}

void Equal(string name, string expected, string actual) =>
    Check(name, string.Equals(expected, actual, StringComparison.Ordinal),
        $"expected «{Show(expected)}» but got «{Show(actual)}»");

static string Show(string s) => s.Replace("\r", "\\r").Replace("\n", "\\n");

var now = new DateTime(2026, 1, 5, 9, 7, 30);
const string Date = "2026-01-05";
const string Time = "09:07";

Console.WriteLine("[TemplateRenderer.Render]");

Equal("纯文本模板原样输出",
    "请把下面内容翻译成英文：\n第二行",
    TemplateRenderer.Render("请把下面内容翻译成英文：\n第二行", "X", "Y", now));

Equal("4 个占位符全部替换",
    $"C=背景A S=文本B D={Date} T={Time}",
    TemplateRenderer.Render("C={clipboard} S={selection} D={date} T={time}", "背景A", "文本B", now));

Equal("同一占位符多次出现",
    $"{Date}|{Date}",
    TemplateRenderer.Render("{date}|{date}", "", "", now));

Equal("相邻占位符",
    $"{Date}{Time}",
    TemplateRenderer.Render("{date}{time}", "", "", now));

Equal("替换值里的 {date} 不会被二次替换（clipboard）",
    "前缀 {date} {selection} 后缀",
    TemplateRenderer.Render("前缀 {clipboard} 后缀", "{date} {selection}", "SEL", now));

Equal("替换值里的 {clipboard} 不会被二次替换（selection）",
    "<{clipboard}{time}>",
    TemplateRenderer.Render("<{selection}>", "CLIP", "{clipboard}{time}", now));

Equal("未知占位符与孤立花括号按字面保留",
    "{foo} { } {} {Date} {",
    TemplateRenderer.Render("{foo} { } {} {Date} {", "", "", now));

Equal("占位符区分大小写",
    "{Clipboard}{SELECTION}",
    TemplateRenderer.Render("{Clipboard}{SELECTION}", "x", "y", now));

Equal("双花括号包裹",
    $"{{{Date}}}",
    TemplateRenderer.Render("{{date}}", "", "", now));

Equal("末尾半截占位符",
    "abc{dat",
    TemplateRenderer.Render("abc{dat", "", "", now));

Equal("空选区替换为空串",
    "总结：",
    TemplateRenderer.Render("总结：{selection}", "", "", now));

Equal("null 值按空串处理",
    "[][]",
    TemplateRenderer.Render("[{clipboard}][{selection}]", null, null, now));

Equal("emoji 与中文混排",
    "🚀 你好 🚀",
    TemplateRenderer.Render("🚀 {selection} 🚀", "", "你好", now));

Equal("日期格式 yyyy-MM-dd（个位月日补零）", Date, TemplateRenderer.Render("{date}", "", "", now));
Equal("时间格式 HH:mm（24 小时制补零）", "21:05",
    TemplateRenderer.Render("{time}", "", "", new DateTime(2026, 12, 31, 21, 5, 59)));

Console.WriteLine("[TemplateRenderer.ContainsSelection]");
Check("包含 {selection}", TemplateRenderer.ContainsSelection("a{selection}b"));
Check("不包含 {selection}", !TemplateRenderer.ContainsSelection("a{clipboard}b"));
Check("大小写不同不算", !TemplateRenderer.ContainsSelection("{Selection}"));
Check("null 安全", !TemplateRenderer.ContainsSelection(null));

Console.WriteLine("[TemplateRenderer.NormalizeLineEndings]");
Equal("LF/CR/CRLF 统一为 CRLF", "a\r\nb\r\nc\r\nd", TemplateRenderer.NormalizeLineEndings("a\nb\r\nc\rd"));
Equal("连续空行保留", "a\r\n\r\nb", TemplateRenderer.NormalizeLineEndings("a\n\nb"));
Equal("无换行原样返回", "abc", TemplateRenderer.NormalizeLineEndings("abc"));

Console.WriteLine("[TemplateRenderer.BuildPreview]");
Equal("短模板完整显示", "你好 世界", TemplateRenderer.BuildPreview("你好\r\n\r\n  世界"));
Equal("超长截取前 20 字并加省略号",
    "一二三四五六七八九十一二三四五六七八九十…",
    TemplateRenderer.BuildPreview("一二三四五六七八九十一二三四五六七八九十多余的内容"));
Equal("恰好 20 字不加省略号",
    "一二三四五六七八九十一二三四五六七八九十",
    TemplateRenderer.BuildPreview("一二三四五六七八九十一二三四五六七八九十"));
Equal("换行与制表压成单个空格", "a b c", TemplateRenderer.BuildPreview("\n a\n\n\tb   c \n"));
Equal("emoji 不被切断", "😀😀😀…", TemplateRenderer.BuildPreview("😀😀😀😀", 3));
Equal("空白模板返回空串", "", TemplateRenderer.BuildPreview("  \r\n "));
Equal("null 返回空串", "", TemplateRenderer.BuildPreview(null));
string huge = new string('字', 8000);
Check("8K 模板预览只截前 20 字",
    TemplateRenderer.BuildPreview(huge) == new string('字', 20) + "…");

Console.WriteLine("[ParameterRules]");
Check("模板 8192 字节以内通过", ParameterRules.IsTemplateWithinLimit(new string('a', 8192), out _));
Check("模板 8193 字节被拒", !ParameterRules.IsTemplateWithinLimit(new string('a', 8193), out int bytesA) && bytesA == 8193);
Check("中文按 UTF-8 字节计（2731 字 = 8193 字节被拒）",
    !ParameterRules.IsTemplateWithinLimit(new string('中', 2731), out int bytesB) && bytesB == 8193);
Check("中文 2730 字 = 8190 字节通过", ParameterRules.IsTemplateWithinLimit(new string('中', 2730), out _));

Check("延迟未填写 → 默认 200", ParameterRules.TryParseRestoreDelay(null, out int d0) && d0 == 200);
Check("延迟 200", ParameterRules.TryParseRestoreDelay("200", out int d1) && d1 == 200);
Check("延迟下限 50", ParameterRules.TryParseRestoreDelay("50", out int d2) && d2 == 50);
Check("延迟上限 2000", ParameterRules.TryParseRestoreDelay("2000", out int d3) && d3 == 2000);
Check("延迟 300.0 视为整数", ParameterRules.TryParseRestoreDelay("300.0", out int d4) && d4 == 300);
Check("延迟 49 被拒", !ParameterRules.TryParseRestoreDelay("49", out _));
Check("延迟 2001 被拒", !ParameterRules.TryParseRestoreDelay("2001", out _));
Check("延迟 150.5 被拒（非整数）", !ParameterRules.TryParseRestoreDelay("150.5", out _));
Check("延迟 abc 被拒", !ParameterRules.TryParseRestoreDelay("abc", out _));

Check("恢复开关未填写 → 默认 true", ParameterRules.TryParseRestoreFlag(null, out bool r0) && r0);
Check("恢复开关 False", ParameterRules.TryParseRestoreFlag("False", out bool r1) && !r1);
Check("恢复开关 true", ParameterRules.TryParseRestoreFlag("true", out bool r2) && r2);
Check("恢复开关 yes 被拒", !ParameterRules.TryParseRestoreFlag("yes", out _));

Console.WriteLine();
Console.WriteLine($"Result: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;
