# Services - 业务逻辑服务层

本目录包含应用程序的核心业务逻辑服务，处理 ZIP 解压、CSV 解析、数据导入和主题管理。

## 文件说明

| 文件 | 说明 |
|------|------|
| `ZipService.cs` | ZIP 压缩包处理服务。`ExtractAsync` 将 ZIP 解压到临时目录（`%TEMP%/LogVision/`）；`ScanCsvFiles` 递归扫描目录下所有 CSV 文件并根据文件名关键词（event/op/cmd/operation/log）自动猜测文件类型；`Cleanup` 清理临时目录 |
| `CsvParsingService.cs` | CSV 文件解析服务（基于 Sylvan.Data.Csv）。`AnalyzeAsync` 快速分析文件结构（列名和行数）；`ParseRunDataAsync` 解析运行数据 CSV，第一列作为时间戳（支持数值和 DateTime 格式），其余列解析为 double 数值；`ParseOpEventsAsync` 解析操作事件 CSV（时间戳/操作类型/消息三列格式）。支持进度回调 |
| `ImportService.cs` | 导入编排服务，协调整个导入流程。接收 ZIP 路径和用户选择的文件列表，依次：为日志包生成唯一 ID 和数据目录、解析运行数据 CSV 并写入 SQLite 动态表、解析操作事件并写入、复制源文件到数据目录、保存日志包元数据。全程通过 `IProgress` 报告阶段和进度百分比 |
| `ThemeService.cs` | 主题管理服务。支持深色/浅色/跟随系统三种模式。通过切换 WPF `ResourceDictionary` 实现主题切换。主题偏好持久化到 `%AppData%/LogVision/settings.json`。跟随系统模式通过读取 Windows 注册表 `AppsUseLightTheme` 值判断，并监听 `SystemEvents.UserPreferenceChanged` 实现实时响应 |
