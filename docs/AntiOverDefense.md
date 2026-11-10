# 防止过度防御

初始化“02 选择要启用的功能”中，“防止过度防御”放在文件夹管理之后，默认勾选。确认页会列出本次是否写入该规则。

勾选后，全局 `AGENTS.md` 会包含两部分内容：

- HERO-Anti-OverDefense 的中文完整版，保留规则、示例和结尾要求。[固定的上游版本](https://github.com/wanshuiyin/HERO-Anti-OverDefense/blob/cc79bd03a407eaabe5a0237274d3cdee25f79156/RULES.md#the-block)
- “实现与防御边界”“自主推进与交付”两节，约束重复校验、理论防御、异常处理、函数抽取，以及常规决策、完成和提问条件。

规则和许可均内嵌于 EXE，初始化时直接读取。全局 `AGENTS.md` 只写规则正文，来源和 MIT 许可保存在同目录的 `HERO-Anti-OverDefense.LICENSE.txt`。[规则正文](../app/AntiOverDefense.md) · [上游许可](../app/HERO-Anti-OverDefense.LICENSE.txt)

完整初始化会先备份原全局文件，再从本次选择重建。原文件保存在本次备份的 `Backups\AGENTS.md`，可通过该会话的 `Rollback.cmd` 恢复。取消勾选后，新生成的全局文件不包含该规则；重复初始化也只生成一份。项目内的 `AGENTS.md` 不在此操作范围内。

升级工具或改变勾选状态不会立即写入规则；执行初始化后生效。单独更新代理也不会重建全局规则。此选项属于桌面初始化，独立脚本的功能和组件版本保持现状。
