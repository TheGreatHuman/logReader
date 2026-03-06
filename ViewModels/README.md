# ViewModels - MVVM ViewModel 层

本目录包含应用程序的 ViewModel，基于 CommunityToolkit.Mvvm 实现 MVVM 模式。

## 文件说明

| 文件 | 说明 |
|------|------|
| `MainViewModel.cs` | 主窗口的 ViewModel，是应用的核心控制器。主要职责包括：|

### MainViewModel.cs 详细功能

- **服务协调**: 持有并初始化 `LogDatabase`、`DynamicDataAccess`、`ZipService`、`CsvParsingService`、`ImportService` 等服务实例
- **日志包管理**: 维护 `LogPackages` 列表和 `SelectedLogPackage` 当前选中项，选中时自动加载对应的图表和表格数据
- **ZIP 导入流程** (`ImportZipCommand`): 打开文件对话框 -> 解压 ZIP -> 扫描 CSV -> 分析文件结构 -> 弹出导入对话框 -> 执行导入 -> 刷新列表
- **删除日志包** (`DeleteSelectedPackageCommand`): 确认对话框 -> 删除数据表 -> 删除元数据 -> 清理文件
- **图表渲染**: 使用 ScottPlot 绘制多列数据的折线图（`SignalXY`），支持深色/浅色两套配色方案。加载操作事件作为虚线垂直标注
- **图表-表格联动**: 点击图表定位到最近的表格行；选中表格行时图表缩放到对应时间戳并添加高亮线
- **列选择**: 支持通过 ToggleButton 动态选择/取消要在图表中显示的数据列，默认显示前 5 列
- **主题响应**: 监听 `ThemeService.ThemeChanged` 事件，主题切换时自动更新 ScottPlot 图表的配色和样式
- **状态管理**: 通过 `IsBusy`、`StatusMessage`、`ProgressValue` 属性驱动底部状态栏的加载状态和进度条显示
