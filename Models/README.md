# Models - 数据模型

本目录定义应用程序使用的所有数据模型和枚举类型。

## 文件说明

| 文件 | 说明 |
|------|------|
| `LogPackage.cs` | 日志包模型，继承自 `ObservableObject`（支持属性变更通知）。属性包括：Id（唯一标识）、Name（显示名称）、Description（描述）、OriginalFileName（原始 ZIP 文件名）、ImportDate（导入时间）、DataFolderPath（数据存储路径）、ColumnNames（RunData 表中的数据列名列表） |
| `CsvFileInfo.cs` | CSV 文件信息模型，用于导入对话框中展示和选择 CSV 文件。包含文件名、路径、列名列表、行数、是否选中、文件类型（RunData 运行数据 / OpEvent 操作事件）等属性。同时定义了 `CsvFileType` 枚举 |
| `OperationEvent.cs` | 操作事件模型，表示日志中的操作事件记录。包含 Id、TimestampMs（毫秒时间戳）、OperationType（操作类型）、Message（事件消息）。在图表上以虚线垂直标注显示 |
| `ThemeMode.cs` | 主题模式枚举，定义三种模式：`Dark`（深色）、`Light`（浅色）、`Auto`（跟随系统） |
