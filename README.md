# StarPie-PromptPaste（提示词快贴）

[中文](#中文) | [English](#english)

StarPie 社区插件：在轮盘扇区上绑定一段提示词，右键划出、选中扇区后立即粘贴到当前窗口。
A StarPie community plugin: bind a prompt to a wheel slice, and it is pasted into the active window as soon as you pick that slice.

| | |
| --- | --- |
| 插件 ID / Plugin ID | `io.github.yukinomon.promptpaste` |
| 动作 ID / Action ID | `io.github.yukinomon.promptpaste.pastePrompt` |
| 目标框架 / Target | `net8.0-windows` · `win-x64` |
| SDK API | `1.13`（StarPie ≥ 2.0.0） |
| 能力 / Capabilities | `Clipboard` · `InputSimulation` · `Ui` |
| 许可证 / License | MIT |

---

## 中文

### 功能

- **单动作直贴**：在 StarPie 设置页把「粘贴提示词」绑定到一级或二级扇区，填好模板即可使用。
- **4 个免弹窗变量**：

  | 变量 | 含义 |
  | --- | --- |
  | `{clipboard}` | 触发瞬间原剪贴板里的纯文本（没有文本时为空） |
  | `{selection}` | 当前选中的文本（插件会模拟一次 `Ctrl+C` 读取；没选中时为空，并弹出一条非阻塞轻提醒） |
  | `{date}` | 当前日期，`yyyy-MM-dd` |
  | `{time}` | 当前时间，`HH:mm` |

  变量只替换一次：剪贴板 / 选区内容里刚好出现的 `{date}` 等字样会原样保留。未识别的 `{xxx}` 按字面输出，变量区分大小写。
- **剪贴板保护**：粘贴后（默认 200 ms）自动把原剪贴板纯文本写回。原剪贴板是图片、文件等非文本内容时，安全跳过恢复并记录日志，不报错。

### 参数

| 参数 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| 提示词模板 `template` | 多行文本（必填） | 空 | 支持多行与上面 4 个变量，UTF-8 不超过 8 KiB |
| 粘贴后恢复原剪贴板 `restoreClipboard` | 开关 | 开 | 只恢复纯文本 |
| 恢复延迟 `restoreDelayMs` | 数字 | 200 | 50～2000 ms；目标程序粘贴较慢时调大 |

模板示例：

```text
请把下面这段内容翻译成地道的英文，保留原有格式：

{selection}
```

```text
[{date} {time}] 参考资料：
{clipboard}

问题：{selection}
```

### 安装

1. 从 [Releases](https://github.com/yukinomon/StarPie-PromptPaste/releases) 下载 `StarPie-PromptPaste-v1.0.0.zip` 并解压（可用发布说明里的 SHA256 校验）。
2. 打开 StarPie 设置 → **插件与扩展** → **手动安装社区插件**，选择解压目录中的 `StarPie.Plugin.PromptPaste.dll`。
3. 在安装确认页核对 ID、版本、能力与目标框架，完成安装后启用插件。
4. 在动作编辑器中选择「插件动作」→「粘贴提示词」，填写模板并保存。

### 已知行为与限制

- 本插件**不会**在粘贴后自动按 Enter 发送，**不**提供搜索面板或弹窗填参，**不**读取外部文件，也不联网。
- 模板含 `{selection}` 时会模拟一次 `Ctrl+C`。部分程序在没有选中内容时也会复制（例如 VS Code 默认复制当前整行），此时 `{selection}` 会是那一行。
- 模板含 `{selection}` 且原剪贴板是图片时，模拟的 `Ctrl+C` 会覆盖剪贴板，原图片无法还原。
- 原剪贴板同时含文本和富格式（例如从 Excel、Word 复制）时，只能还原纯文本部分。
- 如果在恢复延迟期间你（或其它程序）又复制了新内容，插件会跳过恢复，以免覆盖新内容。
- 日志位置：`%LOCALAPPDATA%\StarPie\logs\plugins\io.github.yukinomon.promptpaste_yyyy-MM-dd.log`。

### 从源码构建

环境：Windows 10/11 x64、Git、.NET 8 SDK。

```powershell
mkdir C:\Dev\StarPieWorkspace
cd C:\Dev\StarPieWorkspace
git clone --branch v2.0.0 https://github.com/Star-Pie/StarPie.git
git clone https://github.com/yukinomon/StarPie-PromptPaste.git
cd StarPie-PromptPaste

# 单元测试（纯 BCL，零 NuGet）
dotnet run --project tests/StarPie.Plugin.PromptPaste.Tests -c Release

# 构建
dotnet build src/StarPie.Plugin.PromptPaste/StarPie.Plugin.PromptPaste.csproj -c Release
```

SDK 工程默认按「与本仓库并列的 `StarPie` 目录」查找；放在别处时传入：

```powershell
dotnet build src/StarPie.Plugin.PromptPaste/StarPie.Plugin.PromptPaste.csproj -c Release `
  -p:StarPieAbstractionsProject=D:\path\to\StarPie.Plugin.Abstractions.csproj
```

构建产物 `src\StarPie.Plugin.PromptPaste\bin\Release\net8.0-windows\` 只应包含 `StarPie.Plugin.PromptPaste.dll` 与 `plugin.json`。如果出现 `StarPie.Plugin.Abstractions.dll`，构建会直接报错。

无界面自检：

```powershell
.\StarPie.exe --plugin-selftest "C:\Dev\StarPieWorkspace\StarPie-PromptPaste\src\StarPie.Plugin.PromptPaste\bin\Release\net8.0-windows\StarPie.Plugin.PromptPaste.dll" --skip-invoke
```

一键测试 + 构建 + 红线检查 + 自检 + 打包：

```powershell
.\scripts\package-release.ps1 -StarPieExe "C:\path\to\StarPie.exe"
```

产物：`dist\StarPie-PromptPaste-v1.0.0.zip`、`.sha256` 与 `RELEASE_NOTES.md`。

### GitHub Actions 云端编译

仓库自带 [`.github/workflows/build.yml`](.github/workflows/build.yml)，不需要本机安装任何东西：

- **每次 push 到 `main` / 提交 PR**：在 `windows-latest` 上拉取 StarPie `v2.0.0` 的 SDK，跑单元测试 → Release 编译 → 红线检查 → 打 ZIP。运行完成后在该次运行页面底部 **Artifacts** 下载 `StarPie-PromptPaste-build-<编号>`。
- **推送 `v*` 标签**（如 `git tag v1.0.0 && git push origin v1.0.0`）：先校验标签与 `plugin.json` 的 `version` 一致，再自动创建 GitHub Release，附带 ZIP 与 `.sha256`。
- 也可以在 Actions 页面手动点 **Run workflow**。

StarPie 官方自检需要真实宿主，CI 里不跑，请下载产物后在本机执行。

> 仓库根目录的 `global.json` 把 SDK 固定在 .NET 8（`rollForward: latestFeature`）。本机只装了 .NET 9 及以上 SDK 时，请先安装 .NET 8 SDK。

---

## English

### Features

- **One action, direct paste**: bind **Paste Prompt** to a first- or second-level wheel slice in StarPie settings and fill in your template.
- **4 built-in variables (no pop-ups)**:

  | Variable | Value |
  | --- | --- |
  | `{clipboard}` | Plain text that was on the clipboard when you triggered the action (empty if none) |
  | `{selection}` | Currently selected text (read with one simulated `Ctrl+C`; empty if nothing is selected, with a non-blocking notification) |
  | `{date}` | Local date, `yyyy-MM-dd` |
  | `{time}` | Local time, `HH:mm` |

  Variables are replaced in a single pass, so a literal `{date}` inside your clipboard or selection is kept as is. Unknown `{xxx}` stays literal; variables are case-sensitive.
- **Clipboard protection**: after pasting (200 ms by default) your original plain-text clipboard is restored. Images, files and other non-text content are skipped safely and logged, without errors.

### Parameters

| Parameter | Type | Default | Notes |
| --- | --- | --- | --- |
| Prompt template `template` | Multiline text (required) | empty | Multi-line, supports the 4 variables, up to 8 KiB in UTF-8 |
| Restore clipboard after pasting `restoreClipboard` | Toggle | on | Plain text only |
| Restore delay `restoreDelayMs` | Number | 200 | 50–2000 ms; increase it for apps that paste slowly |

### Install

1. Download `StarPie-PromptPaste-v1.0.0.zip` from [Releases](https://github.com/yukinomon/StarPie-PromptPaste/releases) and extract it (verify it with the SHA256 in the release notes).
2. StarPie settings → **Plugins & Extensions** → **Install community plugin manually**, then pick `StarPie.Plugin.PromptPaste.dll`.
3. Review the ID, version, capabilities and target framework, finish installing, then enable the plugin.
4. In the action editor choose **Plugin action** → **Paste Prompt**, fill in the template and save.

### Known behavior and limits

- It does **not** press Enter after pasting, has **no** search panel or pop-up forms, does **not** read external files and makes **no** network requests.
- With `{selection}` the plugin simulates `Ctrl+C`. Some apps copy even when nothing is selected (VS Code copies the current line by default).
- With `{selection}` and an image on the clipboard, the simulated `Ctrl+C` replaces the image, which cannot be restored.
- If the original clipboard holds text plus rich formats (Excel, Word), only the plain text is restored.
- If you (or another app) copy something during the restore delay, the restore is skipped so the new content is not overwritten.
- Log: `%LOCALAPPDATA%\StarPie\logs\plugins\io.github.yukinomon.promptpaste_yyyy-MM-dd.log`.

### Build from source

See the commands in the Chinese section above: clone `Star-Pie/StarPie` (tag `v2.0.0`) next to this repository, then run the tests, the build, the self-test, or `scripts\package-release.ps1` to do everything at once.

---

## License

[MIT](LICENSE) © 2026 yukinomon
