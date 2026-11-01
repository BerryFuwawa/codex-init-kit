# Codex Init Kit 2.2.5

[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.5) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.5/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.5/SHA256SUMS.txt)

- 默认父模型和启用后的子模型改为 **gpt-6.1-sol / medium**。取消“使用推荐模型”模式栏，直接更换模型与思考强度；子模型仍在勾选子代理后可编辑，子代理默认关闭并位于功能列表最后。
- 思考强度下拉框、选中值及摘要改为中文显示，默认“中”，写入配置仍为 medium 等原始标识。
- 概览的数据保留说明改为无背景的单行 12px 白字，位于“开始初始化”下方，悬停可查看配置重建与数据保留范围。
- 初始化新增默认勾选的“启用 Codex 记忆”，同步控制记忆生成和使用；取消后关闭，保留已有记忆文件与对话。
- 新增默认开启的 GitHub 自动镜像：官方优先，失败后串行尝试自定义 HTTPS 前缀和五个内置站点。更新页可保存设置，EXE 更新和初始化 RTK 下载共用；保留现有下载校验，错误页、超时和校验不符会切换下一站。[使用说明](https://github.com/BerryFuwawa/codex-init-kit/blob/v2.2.5/docs/GitHubMirrors.md)
- 修复取消 CUA 等可选步骤后最终汇总仍要求通过的问题，未选择项目记为已跳过，所选步骤真实失败仍会报告。
- 区分微软商店没有更新与更新失败。内置 CLI 版本探针因 Windows 错误码 5 被拒绝启动时，官方包和组件签名正常即可继续；版本运行验证单独显示为未完成。更新仍进行、组件不完整或签名异常时继续暂停依赖步骤。
- 补齐权限错误中文提示，更新说明和截图。防止过度防御仍默认开启；独立脚本单独运行的功能和组件版本保持现状。

验证覆盖可选阶段组合、真实配置写入和记忆保留、版本探针权限与待完成状态、串行切换和校验失败、真实回环 HTTP 传输及 RTK 下载验证、最小宽度和 100% / 200% 缩放。测试使用隔离目录与模拟组件，没有执行真实初始化、更新或关闭当前 Codex。
