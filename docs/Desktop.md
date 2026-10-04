# Codex Init Kit Desktop 2.1.1

这个程序把初始化、电脑操作组件修复和启动维护放进一个窗口。下载 `CodexInitKit.exe` 就能使用，所需脚本已包含在程序里。

## 遇到问题，先选哪项？

| 你想做的事 | 建议操作 | 操作会带来什么变化 |
| --- | --- | --- |
| 先看看当前状态，不想改配置 | 「概览」→「刷新状态」 | 读取当前状态 |
| 电脑操作功能不可用，怀疑缺组件 | 「维护恢复」→ CUA 检查；需要时再修复 | 修复会对照官方应用文件恢复本地组件，并保留修复前备份 |
| 启动很慢，CUA 目录大量读写小文件，甚至拖慢电脑 | 保存工作并退出 Codex，检查 CUA；发现异常后「检查并修复」 | 提前准备并校验当前版本的完整组件，处理失败复制或反复准备引起的启动问题 |
| 启动不正常，想检查启动组件 | 「维护恢复」→ 安装／修复启动保护 | 检查内置与官方备用程序，必要时修复备用组件，安装登录维护任务 |
| 提示找不到 CLI、CLI 路径错误或组件损坏 | 先「诊断」，再选择「安装 / 修复保护」或「恢复桌面原生」 | 检查启动路径和组件，必要时准备官方独立 CLI 或恢复内置 CLI |
| 想切换桌面版自带 CLI／官方独立 CLI | 「恢复桌面原生」／「切换官方独立 CLI」 | 检查目标是否可用，再按所选方式设置桌面应用使用的 CLI |
| 之前换过启动程序，想恢复桌面版自带的程序 | 「维护恢复」→ 恢复 Desktop 原生兼容模式 | 内置组件完整时，清除本工具设置的启动路径覆盖 |
| 备用程序更新后不正常，想退回旧版 | 「维护恢复」→ 回滚运行时 | 验证并恢复启动保护记录的上一可用版本 |
| 本地代理换了端口，想改配置 | 在「初始化」页面填写端口，再到「维护恢复」点击「更新代理」 | 备份并重写代理设置；不会启动代理软件 |
| 想重新配置目录、模型和规则 | 「初始化」 | 备份并重建配置；开始前保存工作 |
| 想看哪里失败了 | 「日志」，或「维护恢复」→ Doctor | 显示执行记录或运行诊断 |

这里的 CUA 检查针对电脑操作功能使用的本地浏览器自动化组件；“运行时”指 Codex 启动和工作时使用的程序组件。这些功能能处理相应的本地配置或文件问题。账号权限、服务端故障和代理服务自身的问题，需要另行排查。完整初始化会改动多项配置，单项修复请从「维护恢复」进入。

桌面版自带 CLI 和官方独立 CLI 都来自 OpenAI。可以按需要切换；切换前保存工作并退出 Codex，目标组件需要通过检查。这里管理的是桌面应用使用的 CLI，终端找不到 `codex` 命令时仍需检查终端安装和 PATH。

CUA 组件目录为 `%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node`。有些 Windows 版本曾出现组件复制失败、逐文件重试或反复生成 `.staging-*` 目录的问题，使 Codex 启动很慢。修复会对照官方源补齐正确版本的组件，再完整校验；若整机卡顿源于这段读写，也可能改善。它不保证解决其他原因的卡顿，不修改官方复制逻辑，也不批量删除旧临时目录。相关现象见 [启动缓慢报告](https://github.com/openai/codex/issues/41822) 和 [组件复制失败报告](https://github.com/openai/codex/issues/42501)。

## 获取与启动

v2.1.1 已发布：[BerryFuwawa/codex-init-kit v2.1.1](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.1.1)。下载 [CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.1.1/CodexInitKit.exe) 即可启动，无需旁置 CMD 或自行编译。环境要求为 Windows x64、.NET Framework 4.8 和 Windows PowerShell 5.1；EXE 尚未做代码签名。

正式文件的 SHA-256 与大小可在发布页资产信息和 [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.1.1/SHA256SUMS.txt) 中核对。

首次使用可直接下载 EXE。已有版本可启动后检查更新，也可手动下载替换。

下载后双击 EXE，先在概览查看状态。需要配置时进入初始化向导，选择盘符与代理，查看变更摘要后确认执行；日常维护使用维护恢复页面。管理员授权只在修改操作开始时触发。自验可使用下文 CLI，命令中的 `$exe` 改为实际下载路径。

首次启动会读取当前状态，并自动检查 GitHub 更新。更新检查使用仓库 `BerryFuwawa/codex-init-kit` 的 `main` 最新提交，再从该提交读取 `desktop-update.json`，因此清单与文件来源绑定在同一个提交上。

## 页面

「初始化」支持代理开关（默认开启，端口 `10808`）、四项可选功能（子代理默认关闭，其余默认开启），以及推荐／手动选择模型。每次完整初始化都会先完整备份全局 `CODEX_HOME\AGENTS.md`，再用RTK 引用和基础工作规则重建；文件夹管理块、Luna 子代理块只在对应选项勾选时追加。子代理多线程未勾选时会将 `multi_agent` 设为 `false`，并且不写入 Luna 块或基础规则中的 `SUBAGENTS` 段落。旧的全局自定义内容只保存在本次会话备份中，项目目录中的 `AGENTS.md` 不受影响。基础配置仍会备份并重建。第一行填写代理端口，第二行填写 IP，留空使用 "127.0.0.1"。初始化全部成功后会弹出完成提示，点击「确认」关闭。概览会分别显示当前 CLI 模式和启动保护安装状态；模式依据当前启动配置判断，异常覆盖不会直接归类为官方独立 CLI。各选项的作用和填写方法见 [初始化选项](Settings.md)。

确认页会明确提示这是全局重置。原 `AGENTS.md` 的完整副本位于本次初始化会话的 `Backups\AGENTS.md`，由该会话已有的 `Rollback.cmd` 恢复；项目目录中的 `AGENTS.md` 不属于重置范围。

| 页面 | 用途 | 主要动作 |
| --- | --- | --- |
| 概览 | 查看最近状态、默认模型和当前环境 | 刷新只读状态、进入初始化 |
| 初始化 | 三步向导：选择工作盘与代理、确认变更、执行并查看结果 | 备份并重置配置与全局规则、按选择创建并绑定工作目录、检查／修复 CUA、按选择写入 Luna 规则；可选安装启动保护 |
| 维护恢复 | 独立维护 CUA、启动保护和代理 | CUA 检查／修复、安装或更新保护器、恢复桌面原生模式、切换或回滚官方独立 CLI、运行 Doctor、卸载保护器、回滚初始化 |
| 更新 | 查看版本、点击下载和重启 | 检查 GitHub、下载并校验新 EXE、退出后替换并重启 |
| 日志 | 查看执行输出、失败原因和备份位置 | 打开日志目录、复制当前日志 |

初始化向导第二步会展示实际工作盘、代理端口、模型和启动保护选择，并在执行前弹出确认；确认内容会说明全局 `CODEX_HOME\AGENTS.md` 将完整备份并重建，旧的全局自定义内容只保留在备份中。修改操作在独立 worker 中运行，页面关闭会被阻止到当前操作结束；管理员授权取消时不会开始执行。

## 默认值与文件位置

| 项目 | 默认值或位置 |
| --- | --- |
| 版本 | `2.1.1` |
| 内嵌初始化组件 | `4.8.3` |
| 内嵌启动保护组件 | `5.2.1` |
| 工作目录 | `<盘符>:\Codex`；默认选 `D`，不可用时选择第一个可用固定盘 |
| 本地代理 | `http://127.0.0.1:10808` |
| 父模型 | `gpt-6.1-sol`，`medium` |
| 子代理 | 默认关闭；启用时为 `gpt-5.6-luna`，`max`；最多 6 个，委派深度 1 |
| 桌面数据根目录 | `%LOCALAPPDATA%\CodexInitKit\Desktop` |
| 日志目录 | `%LOCALAPPDATA%\CodexInitKit\Desktop\Logs` |
| 内嵌 payload | `init.cmd`、`guard.cmd`、`cua.cmd`、`Backend.ps1` |

需要管理员权限的 worker 会检查当前 Windows 用户 SID；授权账户与启动 GUI 的账户不一致时，操作会停止。状态、CUA 检查和 Doctor 不需要管理员权限。所有实际配置变更仍以 Backend 和原有脚本的行为为准。

## 更新流程

1. 启动或更新页面调用 GitHub API，获得 `main` 的完整提交 SHA。
2. 从该提交读取 `desktop-update.json`，校验仓库、清单版本、GitHub release URL 和 64 位 SHA-256。
3. 每次启动时右下角显示自动检查状态；发现新版后右下角显示红色亮点和「检测到更新，点击更新」。点击直接下载到当前 EXE 旁的 `.CodexInitKit-update-<guid>.exe` 临时文件。
4. 再次校验 SHA-256 和 Windows EXE 的 `MZ` 标记；失败时删除临时文件并保留当前版本。
5. GUI 退出后由独立 PowerShell 进程执行 `File.Replace`，保持原 EXE 的目录和文件名，并启动新版。成功后删除临时旧版文件；失败时尝试恢复原可用版本。更新日志保存在桌面日志目录。

## 构建与验证边界

Logo 素材位于 `assets/`：`logo.svg` 为矢量版本，`logo.png` 为透明背景图片，`app.ico` 包含 16、24、32、48、64、128 和 256 像素图标。可通过 `powershell -Sta -NoProfile -ExecutionPolicy Bypass -File tools/Build-BrandAssets.ps1` 重新生成 PNG 与 ICO。

`tools/Build-Desktop.ps1` 使用 .NET Framework 4.8 的 `Framework64\v4.0.30319\csc.exe`，以 WPF、Win64、`winexe` 目标编译，并把 payload 作为资源写入 EXE：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-Desktop.ps1
.\build\desktop\CodexInitKit.exe --self-test
```

源码入口的职责分别是：`app/Core.cs` 负责资源提取、worker、哈希和更新；`app/MainWindow.cs` 负责五页界面、向导、确认和日志；`app/Program.cs` 负责 WPF 与 CLI 入口。已发布 EXE 通过正式编译、内嵌资源与后台模拟回归、无窗口五页构建、只读状态、更新清单校验和临时文件原子替换测试。发布后再次通过 GitHub 清单在线查询，且发布资产校验值与清单一致。真实初始化、运行时切换和完整升级重启未在用户正在使用的 Codex 上执行。

### 无窗口 CLI 验收

`--verify` 不启动主窗口，检查内嵌资源的长度与 SHA-256、Backend PowerShell 语法和内嵌回归测试、WPF 五页重建与导航、非法 worker 参数拒绝、更新清单来源／HTTPS／SHA-256 拒绝，以及临时文件原子替换和备份；`--status-json` 只读调用 `status` 并把状态和日志路径写入报告；`--check-update-json` 只读查询 GitHub 并校验清单，默认使用代理端口 `10808`，不下载或替换 EXE。GUI EXE 请用 `Start-Process -Wait -PassThru` 取得退出码，再读取 JSON：

```powershell
$exe = (Resolve-Path .\build\desktop\CodexInitKit.exe).Path
$verifyReport = Join-Path $env:TEMP 'CodexInitKit-verify.json'
$verify = Start-Process -FilePath $exe -ArgumentList @('--verify', "`"$verifyReport`"") -WindowStyle Hidden -Wait -PassThru
$verify.ExitCode
Get-Content -Raw $verifyReport | ConvertFrom-Json

$statusReport = Join-Path $env:TEMP 'CodexInitKit-status.json'
$status = Start-Process -FilePath $exe -ArgumentList @('--status-json', "`"$statusReport`"") -WindowStyle Hidden -Wait -PassThru
$status.ExitCode
Get-Content -Raw $statusReport | ConvertFrom-Json

$updateReport = Join-Path $env:TEMP 'CodexInitKit-update-check.json'
$update = Start-Process -FilePath $exe -ArgumentList @('--check-update-json', "`"$updateReport`"") -WindowStyle Hidden -Wait -PassThru
$update.ExitCode
Get-Content -Raw $updateReport | ConvertFrom-Json
```

`--self-test` 只验证资源可以提取且操作名没有重复，也应通过同样的 `Start-Process -Wait -PassThru` 读取退出码。上述命令不会执行初始化、运行时切换或真实更新，也不需要桌面控制或 UI 自动化。

## 实际变更与回滚边界

`--render-preview <目录>` 在内存中渲染五页预览 PNG，不打开窗口；可配合 CLI 报告检查布局。`tests/Desktop.Tests.ps1` 可自动运行无窗口验证与只读状态检测，传入 `-CheckLiveUpdate` 时额外查询 GitHub 清单。

需要管理员权限的脚本提取到 ProgramData 下使用随机名称和受保护 ACL 的工作目录；仅 Administrators 与 SYSTEM 可读写，执行后清理。普通只读 worker 在 LocalAppData 下执行。日志单独保留。

- 初始化会真实备份并重置当前用户 Codex 配置，可能关闭或停止 Codex 进程，写入 `.env`、全局 `CODEX_HOME\AGENTS.md`、工作目录和用户 PATH。全局规则会从RTK 引用和基础规则重建，只追加本次勾选的文件夹管理块或 Luna 块；旧全局自定义内容只在本次会话 `Backups\AGENTS.md` 中保留，项目目录内的 `AGENTS.md` 不变。开始前保存工作并关闭 Codex。
- 初始化回滚只恢复最近一次初始化备份；它不恢复 CUA 运行时备份，也不删除初始化期间已创建的标准工作目录。
- CUA 修复保留自己的运行时备份。启动保护的安装、更新、切换、回滚和卸载使用保护器自己的状态与备份，不等同于初始化回滚。
- 更新器只替换当前桌面 EXE，成功后删除旧版；更新清单或下载校验失败时不改动当前版本。脚本独立更新器仍按各自规则保留脚本备份。

## 视觉参考

README 中的界面图片直接从当前 EXE 离线渲染，使用 192 DPI（200% 缩放）。维护截图可运行 `tools/Render-Documentation.ps1 -Executable <EXE路径> -Dpi 192`；普通页面输出为 2240 × 1600 像素，完整设置图为 2240 × 2800 像素，不打开窗口或执行初始化。

界面布局与交互取 [WPF UI](https://github.com/lepoco/wpfui) 和 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 作为视觉参考。它们不属于运行时依赖；本项目未复制其代码或组件。
