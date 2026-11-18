# v2.3.3 · 保存代理后，照常打开 Codex

[下载 Windows EXE](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.3.3/CodexInitKit.exe) · [SHA-256 校验](https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.3.3/SHA256SUMS.txt)

- **代理保存一次，继续用原来的图标。** 在「连接与修复 → 连接状态」保存现有代理，完全退出后从原图标重开 Codex。桌面连接和官方 CLI 会读取保存的配置，Init Kit 可以关闭。
- **模型设置加入 GPT-6.2 Sol 和 Astra。** 父模型和启用后的子模型都能选择这两款，提供 `low` 到 `ultra` 六档。已有模型设置会照常载入。
- **需要退出 Codex 时，先核对再关闭。** 完整初始化只关闭本次确认的 Codex 进程，确认退出后再修改配置。浏览器组件修复会按当前官方版本恢复文件，网页读取通过独立测试页验证。

代理保存到当前 Windows 用户环境时，也会被同一账号下读取这些代理变量的其他程序采用。[代理设置与回退](PersistentNetwork.md)

已在 Windows 10 x64 验证启动、只读预检和实际连接。138 项桌面隔离回归、91 项共享核心回归及 71 项桌面综合检查通过。Windows 11 真机和真实账号的完整初始化仍待验证。[验证范围](Validation-2.3.3.md)
