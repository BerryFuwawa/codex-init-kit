# Codex Init Kit 2.0.4

- 本地 HTTP 代理增加开关，默认开启、端口 `10808`，附 V2RayN、Clash 等填写提示；关闭后可移除 Codex 代理配置。
- 文件夹管理、子代理多线程、CLI 启动保护与修复、CUA 检查与修复可分别勾选，默认全部开启。
- 支持填写自定义父模型和子代理模型 ID，确认页展示实际选择；默认仍为 `gpt-6.1-sol / medium` 和 `gpt-5.6-luna / max`。
- 下拉框、勾选框和输入框使用统一的深色圆角样式，补充键盘焦点提示。

基础初始化仍会备份并重建配置。选项说明见 [初始化选项](https://github.com/BerryFuwawa/codex-init-kit/blob/main/docs/Settings.md)。

下载 [CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.4/CodexInitKit.exe)，校验值见 `SHA256SUMS.txt`。

CLI 模拟回归、只读状态检查和离线界面验证通过；未在正在使用的 Codex 上执行真实配置重置。
