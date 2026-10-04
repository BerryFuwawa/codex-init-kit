# Codex Init Kit 2.0.5

修复一键初始化中“各阶段已通过，最后却提示失败”的问题。

- 修正最终规则校验使用的标记，避免把已安装的子代理规则误判成缺失。
- 取消勾选文件夹管理或子代理后，校验会按实际选择跳过对应项目。
- 统一生成的批处理换行，修复结束时出现 `PASS"`、`_RESULT` 等“不是内部或外部命令”的错误。
- 修复初始化日志中文乱码，避免显示 PowerShell 的 XML 日志；真实错误和失败退出码仍会保留。

下载 [CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.5/CodexInitKit.exe)，或使用程序右下角的更新提示。校验值见 `SHA256SUMS.txt`。

已通过真实五阶段批处理流程的隔离回归、8 种功能组合、失败停止检查、中文输出检查及 EXE 内置 CLI 自验。验证使用临时配置和模拟修复步骤，没有执行真实配置重置。
