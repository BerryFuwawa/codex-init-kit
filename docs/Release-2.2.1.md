# Codex Init Kit 2.2.1

发布于 2026-10-30。[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.1) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.1/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.1/SHA256SUMS.txt)

- 浏览器连接页新增「修复并启动 Codex」，将 [@loslav 提交的 PR #2](https://github.com/BerryFuwawa/codex-init-kit/pull/2) 中的启动前检查与修复集成进 EXE。无需额外下载脚本；原 Codex 图标继续按原方式启动。
- 合入 [@loslav 提交的 PR #3](https://github.com/BerryFuwawa/codex-init-kit/pull/3)，识别新版 Codex 拆分的 `early-bootstrap` 运行时与分块文件哈希；新旧结构、来源和哈希流程均经校验，未知结构不修改文件。
- 启动前校验官方桌面包、当前运行时、Node 代理能力和补丁哈希，支持备份并安全迁移已识别的旧补丁；遇到未知修改、版本变化或校验失败时停止。代理关闭时只恢复本工具管理的补丁，不注入代理。
- 先保存工作并完全退出 Codex，再使用启动按钮。工具不会强制结束任务或关闭 Chrome；准备完成后由当前用户的桌面会话请求启动。代理端口不可达会提示，补丁准备与真实浏览器连接分开显示。
- 默认父模型和启用后的子代理均为 `gpt-6.1-terra / max`，子代理仍默认关闭。模型下拉框加入 `gpt-6.1-terra`，并按当前 Codex 模型元数据提供思考强度；旧版 `gpt-5.5` 保留为兼容选项，实际可用性取决于账号。
- GUI、独立初始化和启动保护的默认值同步，兼容旧版 Luna 规则标记。独立组件内部版本更新为初始化 4.8.5、启动保护 5.2.3，文件名保持兼容。
- 修正原后台超时回归对一秒启动时间的依赖，分别检查超时和已输出内容的保留。
- 「维护恢复」新增[桌面启动故障检查与修复](DesktopStartupRecovery.md)：针对 `EPERM / rename_staging` 导致启动文件无法复制的情况，按当前官方安装包识别缺失目录，复制四个组件并校验；已有异常目录和未知版本结构不会被覆盖。

更新 EXE 不会自动重置已有 Codex 模型配置；新默认值在后续执行初始化时写入。默认初始化仍先备份并重建相关配置。

验证包含新旧运行时结构与分块哈希、隔离的启动入口和桌面组件修复、模型默认值与旧规则迁移、后台和桌面 EXE 回归、脚本更新器及界面检查。开发测试不会强制关闭或更新当前 Codex。真实 Chrome 连接及真实桌面启动修复需退出 Codex 后重新启动验收；当前会话中的两项验收均待完成，不把文件准备成功当作连接或启动成功。
