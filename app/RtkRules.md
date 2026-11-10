# RTK

每条终端命令都加 `rtk` 前缀，例如 `rtk git status`、`rtk cargo test`、`rtk npm run build`、`rtk ls src/`。连写命令时，每条也都加前缀，例如 `rtk git add . && rtk git commit -m "msg"`。没有专用过滤规则的命令会原样执行。

## 命令输出

RTK 压缩输出，保留有效信息并减少上下文占用。将过滤后的输出视为完整结果，照常运行命令，尽量在一次调用中批量执行相关命令。输出截断时按其中的提示查看详情。只有在输出异常为空、与退出码矛盾或出现乱码时，才用 `rtk proxy <命令>` 重新查看原始输出。

## RTK 用法

RTK 是压缩命令输出的命令行代理，命令行为与退出码保持原样。

- `rtk gain` 和 `rtk gain --history` 分别查看总体与逐条命令的节省量。
- `rtk proxy <命令>` 执行命令并显示未过滤的输出，仍记录用量。
- 为单次命令设置 `RTK_DISABLED=1` 可跳过 RTK。
- `rtk discover` 查找历史中本可压缩输出的命令。
