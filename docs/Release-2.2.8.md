# Codex Init Kit 2.2.8

[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.8) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.8/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.8/SHA256SUMS.txt)

- 初始化功能列表下方可选择“桌面版原生 CLI”或“官方独立 CLI”，默认前者；独立组件缺失时会按需准备。确认页显示选择，执行后核验实际模式，失败时尝试恢复原有 CLI 路径设置。
- 已安装启动保护时，登录自动维护和桌面工具的保护更新会保留明确选中的官方独立 CLI；组件不可用时仍可使用完整的桌面原生组件恢复。
- 初始化运行中允许切换菜单；结束后自动返回结果页，并明确显示成功、部分完成或失败。摘要缺失时提示查看日志，保留日志阅读位置。
- 从概览进入正在运行的初始化会返回进度页；完成弹窗关闭后可以正常导航、查看日志及重新设置。

使用模拟进程、隔离组件和配置测试运行中切页、结果弹窗、CLI 切换与失败恢复；未初始化或关闭当前 Codex。
