# Codex Init Kit Desktop 2.0.1

桌面版是两个主脚本的 GUI 入口。`CodexInitKit.exe` 将 Step1、Step2、CUA 修复脚本和 Backend worker 嵌入单个 Windows EXE；界面负责确认范围、启动 worker、显示日志和处理更新。

## 获取与启动

v2.0.1 已发布：[BerryFuwawa/codex-init-kit v2.0.1](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.0.1)。下载 [CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.1/CodexInitKit.exe) 即可启动，无需旁置 CMD 或自行编译。环境要求为 Windows x64、.NET Framework 4.8 和 Windows PowerShell 5.1；EXE 尚未做代码签名。

正式文件的 SHA-256 与大小可在发布页资产信息和 [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.1/SHA256SUMS.txt) 中核对。

使用旧版时，请手动下载此版本替换原 EXE；后续可通过更新页面检查新版。

下载后双击 EXE，先在概览查看状态。需要配置时进入初始化向导，选择盘符与代理，查看变更摘要后确认执行；日常维护使用维护恢复页面。管理员授权只在修改操作开始时触发。自验可使用下文 CLI，命令中的 `$exe` 改为实际下载路径。

首次启动会读取当前状态，并自动检查 GitHub 更新。更新检查使用仓库 `BerryFuwawa/codex-init-kit` 的 `main` 最新提交，再从该提交读取 `desktop-update.json`，因此清单与文件来源绑定在同一个提交上。

## 页面

| 页面 | 用途 | 主要动作 |
| --- | --- | --- |
| 概览 | 查看最近状态、默认模型和当前环境 | 刷新只读状态、进入初始化 |
| 初始化 | 三步向导：选择工作盘与代理、确认变更、执行并查看结果 | 备份并重置配置、创建并绑定工作目录、检查／修复 CUA、写入 Luna 规则；可选安装启动保护 |
| 维护恢复 | 独立维护 CUA、启动保护和代理 | CUA 检查／修复、安装或更新保护器、恢复桌面原生模式、切换或回滚官方独立 CLI、运行 Doctor、卸载保护器、回滚初始化 |
| 更新 | 查看版本、确认下载和重启 | 检查 GitHub、下载并校验新 EXE、退出后替换并重启 |
| 日志 | 查看执行输出、失败原因和备份位置 | 打开日志目录、复制当前日志 |

初始化向导第二步会展示实际工作盘、代理端口、模型和启动保护选择，并在执行前弹出确认。修改操作在独立 worker 中运行，页面关闭会被阻止到当前操作结束；管理员授权取消时不会开始执行。

## 默认值与文件位置

| 项目 | 默认值或位置 |
| --- | --- |
| 版本 | `2.0.1` |
| 工作目录 | `<盘符>:\Codex`；默认选 `D`，不可用时选择第一个可用固定盘 |
| 本地代理 | `http://127.0.0.1:10808` |
| 父模型 | `gpt-6.1-sol`，`medium` |
| 子代理 | `gpt-5.6-luna`，`max`；最多 6 个，委派深度 1 |
| 桌面数据根目录 | `%LOCALAPPDATA%\CodexInitKit\Desktop` |
| 日志目录 | `%LOCALAPPDATA%\CodexInitKit\Desktop\Logs` |
| 内嵌 payload | `init.cmd`、`guard.cmd`、`cua.cmd`、`Backend.ps1` |

需要管理员权限的 worker 会检查当前 Windows 用户 SID；授权账户与启动 GUI 的账户不一致时，操作会停止。状态、CUA 检查和 Doctor 不需要管理员权限。所有实际配置变更仍以 Backend 和原有脚本的行为为准。

## 更新流程

1. 启动或更新页面调用 GitHub API，获得 `main` 的完整提交 SHA。
2. 从该提交读取 `desktop-update.json`，校验仓库、清单版本、GitHub release URL 和 64 位 SHA-256。
3. 用户在更新页面确认后，下载到当前 EXE 旁的 `.CodexInitKit-update-<guid>.exe` 临时文件。
4. 再次校验 SHA-256 和 Windows EXE 的 `MZ` 标记；失败时删除临时文件并保留当前版本。
5. GUI 退出后由独立 PowerShell 进程执行 `File.Replace` 原子替换，备份为 `.before-update-<时间戳>.bak`，随后启动新 EXE。替换失败时原版本保留。

## 构建与验证边界

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

- 初始化会真实备份并重置当前用户 Codex 配置，可能关闭或停止 Codex 进程，写入 `.env`、规则、工作目录和用户 PATH。开始前保存工作并关闭 Codex。
- 初始化回滚只恢复最近一次初始化备份；它不恢复 CUA 运行时备份，也不删除初始化期间已创建的标准工作目录。
- CUA 修复保留自己的运行时备份。启动保护的安装、更新、切换、回滚和卸载使用保护器自己的状态与备份，不等同于初始化回滚。
- 更新器只替换当前桌面 EXE，并留下替换前备份；更新清单或下载校验失败时不改动当前版本。

## 视觉参考

界面布局与交互取 [WPF UI](https://github.com/lepoco/wpfui) 和 [Microsoft PowerToys](https://github.com/microsoft/PowerToys) 作为视觉参考。它们不属于运行时依赖；本项目未复制其代码或组件。
