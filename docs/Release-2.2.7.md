# Codex Init Kit 2.2.7

[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.7) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.7/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.7/SHA256SUMS.txt)

- 初始化模型选择默认继承当前全局设置；父模型或子代理的模型、思考强度未设置时，各自使用 `gpt-6.1-sol / medium` 中对应的默认值。
- 子代理读取 `[agents]` 默认值及本工具已有的托管规则；初始化会保留子模型设置，勾选子代理后同时写入托管规则。现有值不在内置列表时保留并标明尚未验证。
- 移除模型设置页的固定推荐文案，改为说明读取现有设置。子代理仍默认关闭，手动选择不会被状态刷新覆盖。

已使用隔离配置测试继承、默认值、不同模型、手动更换及初始化写入，并完成桌面 EXE 回归和界面检查。
