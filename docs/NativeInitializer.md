# 原生初始化 CLI（本地迁移构建）

原生 CLI 使用 C#／.NET Framework 4.8 实现原第 1 步独立初始化的功能。初始化本身不再通过 CMD 启动 PowerShell，不使用编码命令、动态脚本求值或执行策略覆盖。当前版本只在本地交付，GitHub 仍保持撤回状态。

## 文件与使用

- `CodexInitKit.Cli.exe`：中文命令行与菜单。
- `CodexInitKit.Updater.exe`：独立的原生更新组件，放在同一目录。
- 两份同名 `.exe.config`：.NET 4.8 运行时设置。
- `LICENSE`、校验文件与验证报告。

运行需要 .NET Framework 4.8，不依赖 Python、PowerShell 或第三方运行库。构建源码时才需要 Python 和 Windows 自带的 .NET Framework 编译器。

```text
CodexInitKit.Cli.exe --help
CodexInitKit.Cli.exe status
CodexInitKit.Cli.exe preflight
```

不带命令进入中文菜单。`status` 与 `preflight` 只读，不关闭程序。已有 Codex 仍在运行时，修改操作默认停止；只有用户先保存工作并显式增加 `--allow-close`，才会请求关闭本次识别并核验的相关进程。

| 命令 | 用途 |
| --- | --- |
| `init --drive D --confirm` | 一键初始化：备份、重建基础配置与代理、RTK、全局规则、文件夹、子代理和 CUA |
| `reset --confirm` | 仅执行基础配置／代理／RTK／规则重建，跳过目录、子代理与 CUA |
| `proxy --proxy-port 3067 --confirm` | 仅备份并重建代理，其他配置保留 |
| `rollback --session 备份目录 --confirm` | 回滚指定初始化；不指定目录则使用最近一次 |
| `cua-check` | 只读检查当前官方 CUA |
| `cua-repair --confirm` | 检查并按需修复；原目录备份保留 |
| `check-update` | 查询原生 CLI 专用更新清单 |
| `update --confirm` | 下载、校验后，由独立更新组件等待当前 CLI 退出再替换 |

可选参数包括 `--home`、`--workspace`、`--proxy-host`、`--no-proxy`、`--no-folder`、`--no-subagents`、`--no-cua` 及父／子模型和思考强度。默认沿用旧独立脚本的 Sol／high 和可选功能行为；现有桌面向导的模型继承及勾选默认值未改。代理端口可以从已有 `.env` 读取。

此流程属于完整配置重建，会清理旧的用户级 CLI 路径覆盖；不是只修某一字段。对话、记忆和项目文件不被清除。仅需改代理或检查组件时，使用对应独立命令。

## 备份与恢复

修改前保存 `config.toml`、`.env`、`AGENTS.md`、`RTK.md`、`hooks.json`、RTK 程序和设置、指定旧 CLI 副本，以及用户 PATH 和两项 CLI 覆盖的原值与类型。任一备份失败时停止重建。

每次会话保存 `session.json`、兼容元数据、结果报告和 `Rollback.cmd`。原生回滚支持旧 `session.meta`，恢复原始字节和原来不存在的状态。已创建的工作目录继续保留，CUA 的目录备份属于独立组件修复范围，不纳入初始化文件回滚。

新 RTK 集成先检查正式 `dry-run` 声明的写入位置；未知位置停止，已有文件先备份。只有声明的产物存在，且集成位置与本次 `CODEX_HOME` 一致时才算完成。组件超时会结束本次启动的进程组，并保留已经输出的日志。

CUA 同时支持旧直接读取算法和当前分块读取算法。按官方源检查完整文件树、长度和 SHA-256，复制失败时恢复原目录。已识别的旧浏览器代理补丁恢复为官方原始文件。遇到未知修改或版本变化时停止覆盖。

## 桌面 EXE 中的原生命令

本地桌面 EXE 也内置同一份核心，可通过 `CodexInitKit.exe --native status` 等形式调用，不创建界面。图形初始化和已迁移的维护按钮也已接入原生后台，详见[单文件桌面说明](NativeDesktop.md)。其他维护入口及旧独立第 2 步脚本保留原有实现。桌面 EXE 不会被 CLI 更新命令替换。

## 更新与当前发布范围

原生 CLI 使用单独的 `native-update.json` 协议，只接受本仓库、`native-init-cli` 组件、版本及 SHA-256 均匹配的文件，不把旧单 CMD 清单或桌面 EXE 当作 CLI 使用。

GitHub 当前尚未发布这个原生组件和清单。更新的校验、替换、启动验证、失败恢复及真实工作进程均在隔离目录验收；公开通道的端到端更新仍待后续正式发布验证。当前不要用现有桌面更新清单分发本地 EXE。

## 验证边界

隔离回归覆盖功能开关组合、备份、重复执行、精确回滚、RTK 协议和失败结果、CUA 两类算法、受控进程、更新及失败恢复。真实环境只执行了状态／前置检查、官方 CUA 检查、RTK 官方下载／校验／版本和 dry-run。

未对当前用户执行真实初始化、真实 RTK 集成、注册表写入或 CUA 修复；没有关闭当前 Codex。完整修改验收需在可退出 Codex 的独立账号或测试环境进行。火绒保持运行，防护日志另行核对；未出现新记录不代表所有病毒库和环境都不会拦截。

## 构建

```text
python tools/Build-Native.py 输出目录
python tools/Build-Native.py 输出目录 --tests
NativeInitializer.Tests.exe 测试目录 输出目录/CodexInitKit.Cli.exe
```

规则正文作为可读 Markdown 资源维护。详细逐项标准见 [验收计划](../native/验收计划.md)。RTK 的统计关闭方式参见[上游说明](https://github.com/rtk-ai/rtk/blob/develop/docs/guide/resources/telemetry.md)。

[本地逐项验证报告](NativeValidation.md)
