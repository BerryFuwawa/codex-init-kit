# Codex Init Kit 2.2.4

[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.4) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.4/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.4/SHA256SUMS.txt)

- 修复日志刷新后跳回顶部：默认跟随底部，上翻暂停，回到底部或点击“回到最新日志”恢复；初始化执行页同步生效。保留暂停时的位置、横向滚动和文本选择。
- 概览新增绿色圆角、白字提示：“本工具不会初始化数据与对话”。初始化会备份并重建配置，保留对话记录和项目文件；同时纠正概览遗留的 Terra 模型文案。
- 维护恢复新增本地历史列表：显示初始化配置和保护器 CLI 的类型、版本、备份 / 缓存时间；选中后点击列表下方回滚按钮，确认目标和影响范围后执行。旧配置备份未记录工具版本时如实标注，CLI 执行前校验官方签名、完整性和实际版本。Codex 仍运行或目标变化时停止，不强制结束进程。
- 修复 CUA 报告目录写死 D 盘的问题，使用指定目录或当前用户本地日志目录。独立 CUA 脚本同步修复；初始化、保护脚本组件版本保持现状。
- 补充正常初始化和 winget 输出的中文说明；解释微软商店连接错误 0x80072EFD（-2147012867），保留技术信息和原始日志。
- 桌面版自动更新失败时提示无需担心，可自行在微软商店或 Codex 内手动更新；仍保留未完成状态，组件不完整或等待重启时暂停依赖步骤。
- 同步说明与界面截图。父、子模型保持 gpt-6.1-sol / high；子代理默认关闭并位于功能列表最后，防止过度防御默认开启。

验证使用模拟多屏日志、隔离目录和模拟更新结果，覆盖滚动跟随、阅读位置、语言与页面切换、历史选择、旧备份、无效缓存、运行中拦截、CUA 报告路径及更新网络失败。没有执行真实初始化、更新或关闭当前 Codex。

微软商店网络错误说明参考 [微软 winget 故障文档](https://github.com/microsoft/winget-cli/blob/master/doc/troubleshooting/README.md)。历史列表不降级 Microsoft Store 桌面应用，也不恢复 CUA 组件。
