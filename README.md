# Codex Init Kit

面向 Windows 的 Codex 初始化与维护工具。用一个 EXE 完成工作区配置、CUA 检查与修复、Luna 子代理规则设置，以及启动运行时的日常维护。

**[下载桌面版](https://github.com/BerryFuwawa/codex-init-kit/releases/latest/download/CodexInitKit.exe)** · [版本发布](https://github.com/BerryFuwawa/codex-init-kit/releases) · [使用说明](docs/Desktop.md) · [脚本说明](docs/Scripts.md)

## 快速开始

1. 下载 `CodexInitKit.exe`，放到方便访问的目录。无需另放 CMD 文件或自行编译。
2. 双击启动，在「概览」查看当前环境。
3. 需要初始化时，先保存工作并关闭 Codex，再进入「初始化」，选择工作盘与代理端口。
4. 查看变更摘要，确认后执行。需要时可同时安装启动保护。
5. 日常检查、修复或回滚，使用「维护恢复」；执行结果和备份位置可在「日志」查看。

环境要求：**Windows x64、.NET Framework 4.8、Windows PowerShell 5.1**。修改操作按需请求管理员授权。发布 EXE 尚未签名，文件校验值见发布页的 `SHA256SUMS.txt`。

> 初始化会备份并重建当前用户的 Codex 配置，包括代理、规则和用户 PATH，并停止相关 Codex 进程。已有自定义配置请先确认备份需求。

## 主要功能

| 功能 | 可以做什么 |
| --- | --- |
| 工作区初始化 | 创建标准工作目录，绑定无项目任务目录，写入模型、代理与规则配置 |
| CUA 检查与修复 | 对照官方桌面应用文件进行校验，完整则跳过，修复前保留独立备份 |
| Luna 子代理规则 | 配置子代理模型、并行数量和委派深度，安装并校验规则 |
| 启动保护 | 检查桌面原生与官方后备运行时，管理登录自动维护 |
| 维护与恢复 | 更新、切换或回滚运行时，运行诊断，恢复初始化备份 |
| GitHub 更新 | 自动发现新版本，点击下载、校验并替换，成功后删除旧版 |

桌面版提供「概览、初始化、维护恢复、更新、日志」五个页面。也保留独立脚本入口，适合已有批处理使用习惯的用户。

### 初始化顺序

```text
选择工作盘 → 备份并重建配置 → 创建目录并验证绑定
           → 检查／按需修复 CUA → 安装并校验 Luna 规则
           → 可选安装启动保护
```

CUA 在文件夹管理完成、绑定校验通过后执行；CUA 修复失败时停止后续 Luna 安装。

### 默认设置

| 设置 | 默认值 |
| --- | --- |
| 工作根目录 | `<盘符>:\Codex`；优先选择可用的 `D` 盘 |
| 父模型 | `gpt-6.1-sol` · `medium` |
| 子代理 | `gpt-5.6-luna` · `max` |
| 并行与深度 | 最多 6 个子代理，委派深度 1 |
| 本地 HTTP 代理 | `http://127.0.0.1:10808`；端口可在向导中修改 |

工作根目录包含 `Conversations`、`Projects`、`OUT`、`Temp`、`Shared`、`Archive`、`Config` 和 `Docs` 八个子目录。已有工作文件不会自动搬迁。

## 更新与恢复

**工具更新**来自本 GitHub 仓库。每次启动时，右下角显示自动检查状态；发现新版后，右下角显示带红色亮点的「检测到更新，点击更新」。点击即下载并验证 SHA-256，退出后在原目录以原文件名替换并重启；成功后自动删除旧版，只保留新版。失败时保留原可用文件。

**官方运行时更新**在「维护恢复」页面执行，更新的是保护器管理的 OpenAI 官方 CLI。执行前应关闭 Codex。它与工具自身的 GitHub 更新是两个独立操作。

初始化、CUA 和启动保护分别保留备份，恢复范围如下：

| 恢复操作 | 范围 |
| --- | --- |
| 回滚初始化 | 恢复最新初始化会话中记录的配置、规则及环境项目；不恢复 CUA，不删除创建的工作目录 |
| CUA 备份 | 修复前保存的运行时文件，独立于初始化备份 |
| 回滚启动保护 | 验证并恢复保护器记录的上一可用运行时 |
| 卸载启动保护 | 移除登录自动维护并恢复相关覆盖配置，保留用户数据与官方后备文件 |

桌面执行日志位于 `%LOCALAPPDATA%\CodexInitKit\Desktop\Logs`。初始化备份位于 Windows 实际 Documents 目录下的 `Codex初始化备份`。详细路径和操作边界见 [桌面版说明](docs/Desktop.md)。

## 脚本入口

| 入口 | 用途 | 内部版本 |
| --- | --- | --- |
| [第 1 步：初始化管理](scripts/第1步-Codex初始化管理-V4.7.0-GPT5.6-LUNA-MAX.cmd) | 配置初始化、目录管理、CUA、RTK 与 Luna 规则 | `4.8.1` |
| [第 2 步：启动保护与更新管理](scripts/第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd) | 运行时保护、登录维护、更新、诊断、切换与回滚 | `5.2.1` |
| [CUA 独立入口](scripts/Codex_CUA_Repair.cmd) | 单独检查或修复 CUA | — |

两个主脚本保留原文件名，版本以内部标记为准。完整菜单、执行细节及备份位置见 [脚本使用说明](docs/Scripts.md)。

## CLI 验证与源码构建

EXE 内置无窗口验证入口，可生成 JSON 报告，无需控制桌面：

| 参数 | 用途 |
| --- | --- |
| `--verify <report.json>` | 检查资源、后台模拟回归、页面构建、参数与更新校验 |
| `--status-json <report.json>` | 读取当前环境状态 |
| `--check-update-json <report.json>` | 查询并校验 GitHub 更新清单 |
| `--render-preview <目录>` | 在内存中渲染五页预览 PNG |

命令示例和报告读取方式见 [CLI 使用说明](docs/Desktop.md#无窗口-cli-验收)。验证覆盖只读状态及模拟操作；真实配置重置、运行时切换和完整升级重启未在正在使用的 Codex 上执行。

在仓库根目录执行以下命令构建桌面版：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-Desktop.ps1
```

输出为 `build\desktop\CodexInitKit.exe`。修改共同脚本更新器后，先运行 `tools/Build.ps1` 重新嵌入脚本，再构建 EXE。准备桌面发布清单使用 `tools/Prepare-DesktopRelease.ps1`。

```text
app/                     桌面界面、worker 与 CLI
scripts/                 独立批处理入口
src/GitHubUpdate.ps1      脚本更新模块
tools/                   构建和发布准备工具
tests/                   脚本更新与后台回归测试
docs/                    使用说明与版本说明
desktop-update.json      桌面版更新清单
update-manifest.json     脚本组件更新清单
```

第 1 步脚本使用 GBK，第 2 步使用 UTF-8，均保留 CRLF。仓库不提交用户密钥、机器配置、运行日志或备份文件。
