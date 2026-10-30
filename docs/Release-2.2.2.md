# Codex Init Kit 2.2.2

[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.2) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.2/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.2/SHA256SUMS.txt)

- 桌面版错误日志、弹窗、状态提示和修复结果使用中文，覆盖初始化、浏览器、桌面启动修复、CUA、CLI、配置检查与更新。
- 常见外部错误提供中文解释；无法识别的原因明确提示查看原始日志。保留路径、错误码和模型 ID；解释在本地完成，不上传日志。
- 日志默认显示中文，支持切换原文并复制当前内容。中文文件使用 `.zh-CN.log` 后缀，原始输出和历史日志继续保留；自身更新也分别记录中文和原文。
- 父模型和启用后的子模型默认均改为 `gpt-6.1-sol / high`，子代理仍默认关闭。桌面、独立初始化和启动保护默认值同步；组件版本更新为初始化 4.8.6、启动保护 5.2.4，文件名保持兼容。
- 同步使用说明和界面截图。保留 v2.2.1 合入的浏览器启动修复与新版运行时识别功能。

更新 EXE 本身不会改写现有 Codex 配置；新的模型默认值在后续执行初始化时生效。独立脚本的输出语言沿用现状。

验证涵盖中文消息、未知错误、中文路径与错误码、原文切换和复制、日志保存、诊断原文兼容、父子默认值，以及后台、浏览器启动、桌面修复和更新器回归。使用隔离测试，不关闭或修改正在运行的 Codex；本次未执行真实 Chrome 连接和退出后桌面启动验收。
