# Codex Init Kit 2.2.3

[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.3) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.3/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.3/SHA256SUMS.txt)

- 初始化新增默认勾选的“防止过度防御”，位于文件夹管理之后，确认页展示本次选择。
- “子代理多线程”移至功能勾选列表最后，紧接“更新桌面版（含原生 CLI）”下方，仍默认关闭。
- 写入 [HERO-Anti-OverDefense 中文规则精简版](https://github.com/wanshuiyin/HERO-Anti-OverDefense/blob/cc79bd03a407eaabe5a0237274d3cdee25f79156/RULES.md#the-short-version)，以及“实现与防御边界”“自主推进与交付”两节。规则资源内嵌在 EXE，保留上游来源、署名和完整 MIT 许可。
- 沿用全局 `AGENTS.md` 的备份、重建和回滚流程。取消勾选不写入，重复初始化只生成一份；项目规则及单独更新代理不受影响。
- 同步使用说明和初始化截图。父、子模型保持 `gpt-6.1-sol / high`，子代理仍默认关闭；独立脚本和组件版本保持现状。

升级 EXE 或切换勾选状态本身不会修改现有全局规则，执行完整初始化后生效。完整内容和操作说明见[防止过度防御](https://github.com/BerryFuwawa/codex-init-kit/blob/v2.2.3/docs/AntiOverDefense.md)。

验证涵盖默认勾选和取消、选项传递、规则内容、重复重建、原文备份与回滚，以及桌面构建和初始化回归；测试使用隔离目录，没有对正在运行的 Codex 执行真实初始化。
