# Codex Init Kit 2.0.7

2.0.7 修复了初始化时旧子代理指令残留的问题：初始化时未勾选子代理多线程，会明确关闭 `multi_agent`，并清理 `$CODEX_HOME\AGENTS.md` 中本工具托管且起止标记完整的旧 Luna 指令块。

- 清理前把 `AGENTS.md` 的完整原文件备份为 `$CODEX_HOME\AGENTS.md.before-luna-cleanup-<GUID>.bak`。
- 用户自己的内容、文件夹规则和其他规则会保留。
- 标记嵌套、缺失、未配对或不完整时停止清理，不会宽泛删除或改写 `AGENTS.md`。

已通过隔离回归和内置 CLI 自验；验证未执行真实初始化，也未修改使用中的 Codex 配置。
