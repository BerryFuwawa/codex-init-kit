# Codex Init Kit 2.0.6

- 概览明确显示当前启动模式：桌面原生或官方独立 CLI，并单独显示启动保护是否已安装。路径缺失或其他覆盖会提示需要检查。
- 初始化全部成功后弹出“已完成初始化”，点击“确认”关闭。
- HTTP 代理增加 IP 输入：第一行端口，第二行 IP；IP 留空使用 `127.0.0.1`，支持填写软路由地址。
- 子代理多线程默认不勾选；文件夹管理仍默认勾选。
- 修正关闭子代理时对旧规则的配置校验，避免已有规则导致初始化失败。

下载 [CodexInitKit.exe](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.0.6/CodexInitKit.exe)，或通过程序右下角更新。校验值见 `SHA256SUMS.txt`。

已通过隔离回归和内置 CLI 自验；界面图以 192 DPI 离线渲染。验证没有执行真实初始化或切换当前电脑的 CLI。
