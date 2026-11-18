# 持久网络配置

初始化和“保存代理配置”同时保存原生运行时及桌面 Node 连接使用的代理。保存工作并重新打开原来的 Codex 图标后生效。

## 使用

1. 填写现有 HTTP／混合代理的 IP 和端口。
2. 保存代理或执行初始化。工具备份原值，合并 CODEX_HOME/.env，并保存当前 Windows 用户的 HTTP_PROXY、HTTPS_PROXY、NO_PROXY 和 NODE_USE_ENV_PROXY=1。
3. 保存工作并退出 Codex，再用原来的普通图标打开。新进程继承用户环境；原生 CLI 还会自行读取 .env。当前进程环境不会随保存操作更新。
4. 分别核对模型 HTTP／WebSocket、Work 在线和桌面连接。模型检查使用已保存的来源，界面草稿不参与探针。

用户代理也会被当前 Windows 用户下其他遵循这些变量的程序读取。原有 NO_PROXY 排除项保留，并补齐 localhost、127.0.0.1 和 ::1。Windows 系统代理、TUN 和安全设置不由该操作修改。

取消代理并保存会移除 .env 中的 HTTP／HTTPS 代理声明，以及当前用户的 HTTP／HTTPS 代理和 Node 代理开关；直连排除项保留。机器级环境、已有 ALL_PROXY、系统代理和受管策略仍可能影响实际路由。

## 连接范围

| 连接 | 配置来源 | 验收 |
| --- | --- | --- |
| 原生 CLI HTTP 与模型 WebSocket | CLI 启动读取 .env，覆盖同名继承变量 | 官方 CLI 模型线路检查 |
| Work 本机 exec-server WSS／Noise | 同一原生 CLI 的网络工厂与代理配置 | Work 在线、连接日志及真实使用 |
| 桌面 Node WebSocket，包括 durable | 普通图标启动继承用户代理，官方连接器按 Node 代理开关使用代理 Agent | 当前桌面握手日志与 Karing 源端口 |
| 桌面 HTTP | Electron 系统网络栈 | 桌面实际请求 |
| 浏览器、插件与其他工具 | 各自网络实现 | 分别测试实际连接 |

features.respect_system_proxy 只适用于原生运行时，工具保留其已有配置。单个 CLI 检查或本地代理 TCP 连接不能代替整个桌面的 WebSocket 验收。进入 Karing 规则端口也不等于最终选用了代理节点；上游节点采用 WebSocket 传输是另一层配置。

## 备份与回滚

代理变更使用现有初始化备份。用户变量的值、注册表类型和原先不存在的状态均保存到会话，回滚恢复原值。重复保存相同配置不会重复写入或备份。关闭代理时保留直连排除项。

## 浏览器与诊断

浏览器修复恢复当前官方版本的原文件，保留未知修改并报告原因。探针直接运行官方 doctor --network-only --json，使用正常继承环境；启动失败会清除旧结果并显示错误。HTTP 与 WebSocket 结果分别报告，未进行握手时明确显示。

依据：[官方原生系统代理范围](https://learn.chatgpt.com/docs/enterprise/managed-configuration#use-the-system-proxy-for-native-client-requests)、[官方 CLI 配置加载](https://github.com/openai/codex/blob/rust-v0.169.0/codex-rs/arg0/src/lib.rs#L276)。桌面 Node 代理分支已核对官方包 26.1111.2228.0；应用升级后仍应核对实际连接。
