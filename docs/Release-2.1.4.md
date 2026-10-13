# Codex Init Kit 2.1.4

发布于 2026-10-13。[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.1.4) · [下载 CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.1.4/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.1.4/SHA256SUMS.txt)

## 更新内容

- 默认父模型统一为 `gpt-6.1-sol / high`，覆盖桌面界面、后台、独立初始化脚本和启动保护组件。
- 未勾选「子代理多线程」时，自定义模型界面的子模型和子思考强度置灰且不可选。重新勾选后恢复可选并保留选择，切回推荐模式则恢复默认组合。
- 修复 RTK 在第 2/5 阶段等待 `Enable anonymous telemetry? [y/N]` 导致初始化卡住的问题。集成前显式执行 `rtk telemetry disable`，为 RTK 初始化命令关闭标准输入，并为所有 GUI 后台命令立即传入 EOF。关闭统计失败时停止初始化，提示检查 `rtk-telemetry.log`。
- 内嵌初始化组件更新至 4.8.4，启动保护组件更新至 5.2.2；同步文档、截图和更新清单。

## 升级与旧版卡点处理

已有版本可通过程序的更新提示升级，也可下载新版 EXE 后替换旧文件。

如果旧版初始化已经卡在第 2/5 阶段，先停止对应的初始化任务，避免重复点击。在 PowerShell 终端运行 `rtk telemetry disable`，再使用 v2.1.4 重新初始化。升级不会自动终止旧版残留任务；完整初始化会再次备份并重建配置和全局规则。

## 验证与文件校验

后台回归、最终 EXE 内置验证、页面状态切换、只读状态读取和更新机制测试均通过。RTK 测试使用隔离的模拟命令，覆盖首次统计提示、关闭失败和无换行隐藏提示；未执行真实完整初始化或重建本机配置。

- EXE 大小：562,688 字节。
- SHA-256：`6e5d0507da5a7692681c2f476f759cefb0e8695e0eff6574cba2fb5e7605a7a2`。
