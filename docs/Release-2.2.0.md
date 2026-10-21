# Codex Init Kit 2.2.0

发布于 2026-10-21。[正式 Release](https://github.com/BerryFuwawa/codex-init-kit/releases/tag/v2.2.0) · [下载 EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.0/CodexInitKit.exe) · [SHA256SUMS.txt](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.0/SHA256SUMS.txt)

新增浏览器连接检查与代理修复，并将桌面版和官方独立 CLI 更新接入初始化。

- 概览在已确认的桌面原生模式下显示红色「遇到了未知错误？尝试切换官方独立CLI」。点击跳到维护恢复，定位切换按钮并柔和闪动三次；关闭系统动画时静态高亮，不自动切换。实际切换会先检查并按需准备独立 CLI。
- 新增「浏览器连接」页面，提供检查、修复、恢复和复制测试指令。代理与初始化同步，分开显示组件、端口、环境继承探针与真实连接验证。
- 浏览器入口按当前官方运行时动态定位，保留原有启动和异常处理，合并四项代理变量与环境白名单。原始字节备份、前后哈希及原子替换支持重复执行和回滚；未知结构、第三方改动或哈希异常时拒绝覆盖。与 CUA 检查／修复兼容。
- 初始化新增默认勾选的「浏览器链接修复」「更新官方CLI」「更新桌面版（含原生CLI）」。按桌面更新 → 配置、目录及 CUA → 独立 CLI 更新 → 可选启动保护 → 浏览器修复执行。修改前要求退出 Codex，不强制结束任务。
- 新增步骤未完成时，在组件仍安全可用的前提下继续，最终展示部分完成及可单独重试的项目。桌面更新待完成或需重启时暂停依赖修复。
- 维护页分开更新官方独立 CLI 和桌面版。独立更新不要求安装启动保护，不新建登录任务，保留当前 CLI 模式。
- 桌面版通过 winget 精确更新官方 Store 包 `9PLM9XGG6VKS`，包括原生 CLI；核对包身份及更新后版本和 CLI，最长等待 20 分钟。缺失 winget 或更新失败时提供商店入口，不自动安装 winget，不覆盖 WindowsApps 二进制文件。

README、使用说明、初始化选项、浏览器故障处理及截图同步更新。保留默认 `gpt-6.1-sol / high`、子代理默认关闭、RTK 无交互处理和定向配置清理。

验证包含构建、后台与 EXE 内置回归、六页导航及布局、浏览器修复和 CUA 兼容、winget 各结果模拟、初始化顺序及部分完成、更新器的替换／失败回滚。开发测试未升级或关闭当前 Codex。Chrome 工具在当前会话返回 `nodeRepl.fetch request failed`，未能创建真实测试页；发布不将文件准备成功视为真实 Chrome 连接成功。实际使用需退出 Codex 修复，重启后运行连接测试。详见[浏览器连接说明](BrowserConnection.md)。
