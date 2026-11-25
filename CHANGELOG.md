# Changelog

## v1.0.0

- 首个版本 / First release.
- 动作「粘贴提示词」`pastePrompt`：多行模板，单遍替换 `{clipboard}`、`{selection}`、`{date}`、`{time}`。
- 粘贴后按 `restoreDelayMs`（50～2000 ms，默认 200）恢复原剪贴板纯文本；非文本内容安全跳过。
- 空选区时发出非阻塞轻提醒；中英文词条与 SVG 图标。
