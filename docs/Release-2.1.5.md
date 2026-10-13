# Codex Init Kit 2.1.5

发布于 2026-10-13。[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.1.5) · [下载 CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.1.5/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.1.5/SHA256SUMS.txt)

## 更新内容

- 「维护恢复」新增配置兼容检查及「清理已废弃配置」。覆盖用户配置、profile、旧式内嵌 profile 和明确选择的项目配置。
- 先保存原始文件并生成恢复脚本，再定向移除 `features.code_mode_tool_description_first`、`features.guardianv2.thread_context` 的可靠布尔赋值，保留 Guardian 其他设置。无需管理员权限，不重置配置或关闭 Codex。
- 区分文件旧配置与 `session-flags` 会话参数：文件没有残留时不会误报“全部修复”；内联表等复杂写法提示手工处理。
- 诊断助手可识别这两类原始警告，并给出对应来源的处理建议。初始化继续不写入这些配置项，并增加防回归检查。
- 同步 README、使用说明、故障处理文档和截图。内嵌独立脚本仍为初始化 4.8.4、启动保护 5.2.2。

## 使用与边界

先运行「检查配置」，需要时再「清理已废弃配置」。项目目录留空时只检查用户配置与 profile。详见 [配置兼容检查与恢复方法](https://github.com/BerryFuwawa/codex-init-kit/blob/main/docs/ConfigCompatibility.md)。

如果提示明确来自 `session-flags`，请检查官方桌面版与 CLI 的版本兼容性；删除 config.toml 中不存在的键不能解决这条警告。本工具不修改官方应用文件，不自动切换运行时，也不会打断当前任务。

## 验证

覆盖普通、点分、带引号与 profile 配置；验证注释、多行字符串、相似键及 Guardian 其他设置保留。还覆盖原始字节恢复、无匹配时不写文件、并发修改、读取锁定和写入失败。桌面构建、后台回归、内置验证、更新机制测试及界面渲染通过；本机定向检查没有发现需移除的旧项，未执行完整初始化。
