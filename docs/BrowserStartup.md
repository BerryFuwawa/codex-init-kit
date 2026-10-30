# 启动 Codex 前自动恢复浏览器代理

下载 `CodexInitKit.exe`，先保存工作并完全退出 Codex，在「浏览器连接」页点击「修复并启动 Codex」。按钮使用页面的代理开关、IP 和端口；原来的 Codex 图标不会自动经过此检查。GUI 应以普通用户权限启动，准备过程按需请求管理员授权，再由用户桌面会话请求启动 Codex。

也可从完整检出或解压后的源码包运行 `scripts/Codex_Browser_Start.cmd`。请保留同目录脚本和相邻 `app` 目录，不能只复制 CMD 文件。

独立脚本默认使用 `127.0.0.1:10808` 的 HTTP／混合代理。代理软件需已运行。脚本自定义方式：

```cmd
scripts\Codex_Browser_Start.cmd -Port 7890 -ProxyHost 127.0.0.1
scripts\Codex_Browser_Start.cmd -CheckOnly
scripts\Codex_Browser_Start.cmd -PrepareOnly
```

默认流程：检查当前已安装的官方 Store 包，解析主应用入口，计算该版本运行时 ID，必要时从当前官方源补齐缺失运行时，接管严格匹配的旧手工补丁，调用现有浏览器修复流程，最后通过 AppsFolder 启动官方应用。支持主程序名 `app/ChatGPT.exe` 和 `app/Codex.exe`；不会误启 `codex-command-runner.exe`。

这不是在线更新程序，不下载安装 Codex，不安装计划任务或服务，也不修改模型、工作目录、TUN、系统代理、扩展或沙箱权限。原来的官方启动图标不会经过此功能。正常启动前需保存并彻底退出 Codex（含托盘）；发现进程仍在运行时会停止，不强杀。

`-CheckOnly` 只检查，不修复、不启动，退出码 0 表示受管理补丁与组件就绪，2 表示需要修复／迁移；检查仍可能耗时数秒，因为会验证组件哈希。`-PrepareOnly` 完成准备但不启动。其余失败退出码为 1；正常模式会保留现有浏览器后端的失败码。

## 已知旧补丁迁移

只接受已知日期标记（20261020、20261023、20261026、20261028）或 `Codex local startup proxy v1` 的精确完整代码，且代理值须与命令参数一致。不是仅凭注释信任脚本，也不会执行目标入口里的未知代码。其余所有组件必须与当前官方源相符。不同代理值或任何其他手工改动会停止，需人工核查。

迁移使用现有 `%LOCALAPPDATA%\CodexInitKit\Desktop\BrowserConnection` 的管理记录、官方原件备份和哈希格式；迁移前的入口另存 `运行时ID-哈希.legacy.bin`，不覆盖旧备份。之后可通过现有「恢复原始文件」回到官方原件。恢复旧手工入口是另一种操作，请先核对记录和版本，不要将其他版本文件复制到当前运行时。

更新切换版本时重新定位新运行时，旧备份保留。入口结构或运行时算法变化、组件损坏、备份哈希不符、进程争用或应用在操作中更新时，停止覆盖并显示原因。

## 验证与限制

独立测试：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests\BrowserStartup.Tests.ps1
```

测试使用隔离目录，覆盖主应用识别、已知旧补丁接管、检查模式不写入、运行进程拦截、其他组件被篡改时拒绝迁移、原件保留、与现有 CUA 校验兼容，以及准备／失败时不启动应用。现有 Backend220 回归仍需执行。

`ExecutionPolicy Bypass` 仅用于当前 Windows PowerShell 进程，与仓库现有 CMD 入口一致，不改机器或用户的执行策略。使用 Windows PowerShell 是为了兼容 Appx API。

GUI 的代理关闭时会恢复本工具管理的补丁并请求启动；对于无法识别的修改会停止。已识别的旧手工补丁只有在其代理地址与当前设置一致时才会迁移。代理端口不可达会提示，Node 环境能力或文件校验失败时不会启动。

补丁准备和端口响应不等于真实 Chrome 连接成功，也不能保证所有 `Debugger unattached` 都解决。仍需在启动后的 Codex 中新建一次独立测试标签，读取页面，再单独检查调试器附着并关闭测试标签。
