BEGIN CODEX FOLDER MANAGEMENT PROMPT V1.0

# Codex 文件夹管理提示词 V1.0

Codex 工作区根目录：<SELECTED_DRIVE>:\Codex

在此根目录下管理 Codex 的对话、项目、临时文件、共享资源、归档、文档和最终交付物。

进行中的对话工作和项目交付物请放在对应子目录中，避免直接放在 Codex 根目录。

建议目录：
- Conversations：对话相关工作、笔记、附件和导出
- Projects：长期项目源码及工作文件
- OUT：最终交付物
- Temp：可清理的中间文件和临时工作
- Shared：可复用的模板及资源
- Archive：已完成或冻结的工作
- Config：工作区约定和配置
- Docs：工作区文档

命名约定：
- Conversations\YYYYMMDD-topic-name
- Projects\project-name
- OUT\YYYYMMDD-project-or-task-name
- Temp\YYYYMMDD-short-purpose

开始任务时，主动判断工作类型，选择对应子目录，并按需创建任务目录。将新文件放在适当位置，安全清理本次任务产生的临时文件。

优先使用用户指定的路径、仓库和项目位置。外部仓库留在原位置。

历史文件的整理需要明确授权。移动、覆盖或删除现有用户文件前，先取得明确授权。用户要求整理历史文件时，先检查并提出调整方案，再执行获准的改动。

交付前，确认进行中的工作和最终产物都放在对应目录，保持 Codex 根目录整洁。

END CODEX FOLDER MANAGEMENT PROMPT V1.0
