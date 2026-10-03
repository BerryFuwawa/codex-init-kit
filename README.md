# codex-init-kit

面向 Windows 的 Codex 初始化、目录准备、CUA 修复和 Luna 多线程辅助脚本集合。脚本保留原有中文文件名，方便已有使用习惯和旧快捷方式继续工作。

## 组件与版本约定

- Step1 的脚本文件名 `第1步-Codex初始化管理-V4.7.0-GPT5.6-LUNA-MAX.cmd` 保留不改，脚本内部版本为 `4.8.1`。
- Step2 的脚本文件名 `第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd` 保留不改，脚本内部版本为 `5.2.1`。
- `scripts/Codex_CUA_Repair.cmd` 是独立的 CUA 修复入口。
- `src/GitHubUpdate.ps1` 是共同维护模块，由 `tools/Build.ps1` 嵌入生成的脚本；修改更新器逻辑后应重新构建脚本。
- `update-manifest.json` 位于 `main` 分支根目录，用于描述可更新组件及其 SHA-256。

## 桌面版（GUI 优先）

v2.0.1 已发布单文件 Windows GUI 入口 `CodexInitKit.exe`。它把 Step1、Step2、CUA 修复脚本和 Backend worker 嵌入同一个 EXE，首次使用可从 [v2.0.1 发布页](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.0.1) 下载，或直接下载 [CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.1/CodexInitKit.exe)。无需把 CMD 放在 EXE 旁，也无需自行编译。

发布文件为 Windows x64，使用系统 .NET Framework 4.8 和 Windows PowerShell 5.1，尚未做代码签名。[SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.1/SHA256SUMS.txt) 提供校验值。正式发布文件已通过内置 CLI、后台模拟回归、页面构建及只读状态检查；GitHub 更新清单在线查询也已通过。真实配置重置、运行时切换和完整升级重启未在正在使用的 Codex 上执行。

`2.0.1` 更新下载与更新配置，并完善说明文档。初始化和保护脚本内部版本分别为 `4.8.1`、`5.2.1`。使用旧版时，请手动下载此版本替换；后续可通过更新页面检查新版。

GUI 包含 **概览、初始化、维护恢复、更新、日志** 五个页面。启动时会先读取只读状态，并自动从 GitHub 发现新版本；确认下载后会校验固定提交中的清单与 SHA-256，下载到当前 EXE 旁的临时文件，退出后原子替换、保留旧版本备份并自动重启。网络检查失败不会阻止本地页面和只读状态功能。

桌面版实际默认值为：父模型 `gpt-6.1-sol` / `medium`，子代理 `gpt-5.6-luna` / `max`，最多 6 个子代理且委派深度为 1；默认本地代理端口 `10808`，工作根目录为所选盘符下的 `Codex`。需要修改配置、CUA 或运行时的操作会由同一 Windows 用户上下文启动 UAC worker；状态、CUA 检查和 Doctor 属于只读操作。

初始化会真实重置当前用户的 Codex 配置并可能停止正在运行的 Codex 进程，同时写入工作目录、规则、代理和用户 PATH；开始前请保存工作并关闭 Codex。初始化回滚只恢复初始化备份，不恢复 CUA 运行时备份，也不会删除已创建的标准工作目录；启动保护的运行时回滚使用独立备份。完整页面与边界说明见 [docs/Desktop.md](docs/Desktop.md)。

验收桌面 EXE 时使用内置的 `--verify <report.json>` 和 `--status-json <report.json>` 无窗口入口；Windows GUI EXE 的退出码应通过 `Start-Process -Wait -PassThru` 读取，示例见桌面文档。

## 两个主脚本分别做什么

| 脚本 | 主要用途 | 适合什么时候运行 |
| --- | --- | --- |
| [第 1 步：初始化管理](scripts/第1步-Codex初始化管理-V4.7.0-GPT5.6-LUNA-MAX.cmd) | 备份并重建 Codex 配置，准备工作目录，检查／修复 CUA，安装 RTK 与 Luna 子代理规则 | 首次配置，或需要重新初始化配置时 |
| [第 2 步：启动保护与更新管理](scripts/第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd) | 检查 Desktop 与官方后备运行时，安装登录自动维护，更新、诊断、切换或回滚运行时 | 第 1 步完成后，以及日常启动故障排查与维护时 |

### 第 1 步：初始化管理

一键初始化按下面五个阶段执行，文件夹管理完成且绑定校验通过后才执行 CUA：

1. **确认工作盘符。** 选择工作目录所在盘符，后续根目录为 `<盘符>:\Codex`。
2. **备份与完整初始化。** 请求管理员权限和操作确认；备份配置、代理文件、用户级运行时覆盖状态、自定义 CLI 副本、AGENTS／RTK 文件及用户 PATH，再关闭 Codex，清理用户级 `CODEX_CLI_PATH`、`CODEX_CODE_MODE_HOST_PATH` 覆盖及指定自定义副本，重建配置。
3. **安装文件夹管理。** 创建缺失的 `Conversations`、`Projects`、`OUT`、`Temp`、`Shared`、`Archive`、`Config`、`Docs` 标准目录；将无项目任务目录绑定到 `<盘符>:\Codex\Conversations`，写入目录管理规则并校验绑定。
4. **检查／按需修复 CUA。** 从当前已注册的 Codex 应用定位官方 `cua_node` 源，检查版本算法与目标目录，逐文件比较路径、数量、长度和 SHA-256；完整则跳过修复，需要修复时保留原运行时备份并再次校验。若相关进程仍在运行，提示退出并等待；修复失败会停止后续 Luna 安装。
5. **安装并校验 Luna 配置。** 默认父模型为 `gpt-6.1-sol`，思考强度 `medium（中）`；子代理规则指定 `gpt-5.6-luna / max`，最多 6 个并行子代理、委派深度 1。更新全局 AGENTS 规则，检查最终配置并生成结果报告和回滚入口。

完整初始化还会写入 `config.toml` 中的推理档位列表、多代理与上下文管理设置；将本地代理 `http://127.0.0.1:10808` 写入 `.env`；安装／集成 RTK 到用户工具目录和 PATH，并生成对应使用规则。已有配置和 `.env` 会被备份后重建，因此这一步属于配置重置。

| 菜单 | 实际操作 |
| --- | --- |
| `1` 一键初始化 | 执行上述五个阶段 |
| `2` 查看状态与诊断 | 查看配置、代理、RTK、AGENTS 和用户／机器级运行时覆盖状态 |
| `3` 修复／重建代理配置 | 备份并重写 `.env` 中的本地代理配置 |
| `4` 回滚最近一次初始化 | 使用最近的初始化会话备份恢复已记录的项目 |
| `5` 检查 GitHub 脚本更新 | 立即检查第 1 步脚本新版，确认后更新自身 |
| `0` 退出 | 关闭本次菜单流程 |

初始化备份与报告存放在 Windows 实际 Documents 目录下的 `Codex初始化备份\Codex_Full_Reset_<时间戳>`，每次会话包含备份、`Reset_Result.txt` 和 `Rollback.cmd`。CUA 报告位于所选盘符的 `Codex\Temp\codex-cua-recovery`，CUA 运行时备份保留在运行时目录旁。**初始化回滚不会恢复 CUA 运行时备份，也不会删除已创建的标准工作目录。**

### 第 2 步：启动保护与更新管理

此脚本管理启动运行时和维护任务；模型配置只做状态检查，需要修改默认模型时使用第 1 步。

- **优先使用 Desktop 原生运行时。** 检查当前 Desktop 应用及其内置运行时是否完整；原生组件正常时，清除本工具管理的 CLI 覆盖，保持 Desktop 原生兼容模式。遇到非本工具管理的外部覆盖，会显示状态并保留它。
- **准备官方 standalone 后备运行时。** 检查 `<CODEX_HOME>\packages\standalone\current` 的组件、签名和版本；不完整时调用 OpenAI 官方安装器安装／修复，在线失败时尝试已验证的本机缓存。Desktop 内置组件不完整时才按检查结果启用后备路径。
- **安装登录自动维护。** 复制管理器到 Documents 下的 `Codex启动保护管理器`，生成 PowerShell／VBS 维护入口，注册当前用户登录触发的隐藏任务 `CodexGuardAuto`。自动维护检查运行时状态，并对在线更新检查设置 24 小时间隔；Desktop 正在运行时跳过运行时切换／更新。
- **支持修复、切换、回滚和诊断。** 更新失败时尝试上一可用版本或本机缓存；手动回滚前验证旧版本。提供官方 Current 强制切换、恢复 Desktop 原生模式，以及 `codex doctor --summary --ascii` 诊断。

| 菜单 | 实际操作 |
| --- | --- |
| `1` 安装／修复启动保护 | 检查／修复官方后备组件，并安装管理器副本与登录维护任务 |
| `2` 立即在线更新保护组件 | 调用官方安装器更新 standalone 组件；失败时尝试已验证的后备版本 |
| `3` 强制切换官方 Current | 确认后设置 standalone Current 为活动 CLI，用于故障排查 |
| `4` 卸载启动保护 | 移除登录自动维护，恢复安装前仍有效的 CLI 路径，或清除失效覆盖；保留官方 standalone 文件和用户数据 |
| `5` 回滚到上一可用版本 | 验证并确认后切换到已记录的旧 standalone 版本 |
| `6` 运行 Codex Doctor | 运行诊断并保存输出 |
| `7` 打开日志目录 | 在资源管理器中打开实际日志路径 |
| `8` 恢复 Desktop 原生兼容模式 | 内置组件完整时，清除本工具的 CLI 覆盖，恢复原生模式 |
| `0` 退出 | 退出管理器 |

菜单 `3` 和 `5` 会主动设置 standalone CLI 覆盖；日常原生兼容模式可通过菜单 `8` 恢复。安装、运行时更新、切换、回滚和卸载前需要退出 ChatGPT／Codex，脚本会检查运行状态。维护状态保存在 `Codex启动保护管理器\State\guard-state.json`；日志目录优先使用已存在的 `D:\ChatGPT\Temp` 下的日志子目录，否则使用系统临时目录，可从菜单 `7` 查看。

卸载启动保护后，管理器安装目录中的 CMD、状态文件和日志会保留，便于查看记录或再次安装；卸载不会删除整个安装目录。

**注意两种“更新”的区别：** 第 2 步菜单 `2` 更新的是 OpenAI 官方 standalone 运行时；两个主脚本从本 GitHub 仓库发现并更新的是管理脚本自身。第 2 步脚本自身的立即检查入口为 `--check-update`，与菜单 `5` 的运行时回滚是不同操作。

### 推荐使用顺序

先保存工作，运行第 1 步菜单 `1` 完成配置和 CUA 检查；随后运行第 2 步菜单 `1` 安装启动保护。日常查看状态或运行诊断时使用对应菜单，重新初始化配置时再运行第 1 步。

## 目录

```text
scripts/                 保留原始中文文件名的入口脚本及 Codex_CUA_Repair.cmd
src/GitHubUpdate.ps1     共同更新器模块
tools/Build.ps1          将共同模块嵌入入口脚本
tools/Build-Desktop.ps1  使用 .NET Framework 4.8 Framework64 编译 WPF Win64 桌面 EXE
app/                     桌面版 Core、MainWindow、Program、manifest 与内嵌 Backend
docs/Desktop.md          桌面版页面、更新、验收和回滚边界
update-manifest.json     main 分支的组件版本和校验信息
```

## 使用

在 PowerShell 或命令提示符中进入仓库目录，按上述原始中文文件名运行对应入口。Step1 的菜单项 `5` 可手动检查更新；Step2 支持：

```text
第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd --check-update
第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd --auto
```

`--auto` 的 GitHub 脚本更新检查只读，不显示确认提示，也不下载、替换脚本；原有自动维护仍正常运行。启动与登录维护中的自动检查最多每 24 小时执行一次；网络不可用、GitHub 暂时不可达或清单读取失败时，脚本仍继续提供初始化、目录、CUA、Luna 等主要功能。

## 更新器行为

更新器先查询 GitHub `main` 的最新提交，再从该提交的不可变 raw URL 读取 `update-manifest.json`。它会按组件比较当前内部版本和清单版本；需要更新时询问 `Y/N`。

只有清单中明确发布的较高组件版本才会触发更新，单独提交文档不会触发。SHA-256 用于验证传输完整性，更新来源以此 GitHub 仓库为信任边界。

用户确认后，更新器会把新文件下载到临时阶段路径，校验清单中的 SHA-256，再先生成当前文件的 `.bak` 备份，并使用 `File.Replace` 原子替换目标文件。替换完成后进程退出并提示重新运行入口，以便新代码在全新进程中加载。任一步骤失败都保留当前可用文件并提示重新运行检查。

`update-manifest.json` 使用提交固定的 raw 地址，避免检查过程中 `main` 分支继续移动造成清单和文件不一致。脚本自身更新不修改 Codex 配置、持久环境变量或运行时；更新检查缓存写入 `%LOCALAPPDATA%\CodexInitKit\update-checks`，确认更新后还会保留旧脚本备份。

## 构建

需要修改共同更新逻辑时，编辑 `src/GitHubUpdate.ps1`，然后运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build.ps1
```

构建会把共同模块嵌入脚本入口，并重新生成清单及 SHA-256。发布新版本时先修改 `tools/Build.ps1` 中对应的组件版本，再重新构建。第 1 步为 GBK，第 2 步为 UTF-8，均使用 CRLF；`.gitattributes` 禁止 Git 自动转换批处理字节。

桌面版使用 `tools/Build-Desktop.ps1` 编译 WPF Win64 单文件：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-Desktop.ps1
```

输出为 `build\desktop\CodexInitKit.exe`。桌面 EXE 的 `--self-test`、`--verify`、`--status-json` 和 `--check-update-json` 验收命令，以及 GUI EXE 退出码和 JSON 报告读取方式，见 [docs/Desktop.md](docs/Desktop.md)。

第 2 步自更新后，再运行菜单中的安装／修复启动保护，把新版同步到已安装的入口及登录维护 PowerShell 副本。

## 仓库边界

仓库只保存可复现的脚本、构建工具和更新清单。用户密钥、机器专属配置、环境文件、运行日志、备份文件、依赖目录和临时工作目录不应提交。安装器二进制 `Installer.exe` 也不纳入版本库；需要安装时使用项目约定的外部分发渠道。
