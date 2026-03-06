# LogVision - 日志可视化分析平台

基于 WPF (.NET 10) 的桌面日志可视化分析工具，支持从 ZIP 压缩包导入 CSV 格式的日志数据，通过图表和表格进行交互式分析。

## 技术栈

- **框架**: WPF (.NET 10, Windows 10)
- **架构**: MVVM (CommunityToolkit.Mvvm)
- **图表**: ScottPlot 5
- **数据库**: SQLite (Microsoft.Data.Sqlite)
- **CSV 解析**: Sylvan.Data.Csv

## 项目结构

```
logReader/
├── App.xaml / App.xaml.cs        # 应用程序入口，初始化主题服务
├── AssemblyInfo.cs               # 程序集资源定位信息
├── MainWindow.xaml / .xaml.cs    # 主窗口，包含三栏布局（日志列表/图表/数据表格）
├── logReader.csproj              # 项目配置文件，定义目标框架和 NuGet 依赖
├── logReader.slnx                # 解决方案文件
├── Converters/                   # WPF 值转换器
├── Data/                         # 数据访问层（SQLite 数据库操作）
├── Models/                       # 数据模型定义
├── Services/                     # 业务逻辑服务层
├── Themes/                       # 主题资源字典（深色/浅色）
├── ViewModels/                   # MVVM ViewModel 层
└── Views/                        # 对话框等 UI 视图
```

## 根目录文件说明

| 文件 | 说明 |
|------|------|
| `App.xaml` | WPF 应用程序定义，配置启动窗口和全局主题资源字典 |
| `App.xaml.cs` | 应用程序后台代码，创建全局 `ThemeService` 实例并在启动时初始化 |
| `AssemblyInfo.cs` | 程序集元数据，配置主题资源字典的查找位置 |
| `MainWindow.xaml` | 主窗口 XAML 布局：顶部工具栏（导入/刷新/主题切换/列选择）、左侧日志列表、中间 ScottPlot 图表、右侧数据表格、底部状态栏 |
| `MainWindow.xaml.cs` | 主窗口代码隐藏：初始化 ViewModel、处理图表点击定位表格行、表格选中联动图表缩放、列切换按钮事件 |
| `logReader.csproj` | MSBuild 项目文件，目标框架 `net10.0-windows10.0.19041`，引用 ScottPlot、CommunityToolkit.Mvvm、SQLite、Sylvan 等 NuGet 包 |
| `logReader.slnx` | Visual Studio 解决方案文件 |
