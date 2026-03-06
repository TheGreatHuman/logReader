# Themes - 主题资源字典

本目录包含应用程序的 WPF 主题资源字典，定义了深色和浅色两套完整的 UI 样式。

## 文件说明

| 文件 | 说明 |
|------|------|
| `DarkTheme.xaml` | 深色主题资源字典（Catppuccin Mocha 风格）。定义了完整的颜色系统（背景色 #1E1E2E、强调色 #89B4FA 等）和所有 WPF 控件的样式模板：Window、TextBlock、Button、TextBox、ListBox、DataGrid、ComboBox、ProgressBar、CheckBox、GridSplitter、ToolTip 等。包含圆角、悬停高亮、选中状态等交互效果 |
| `LightTheme.xaml` | 浅色主题资源字典。与深色主题结构完全对应，使用白色系背景（#FFFFFF）和蓝色强调色（#1A73E8），确保两套主题可无缝切换。所有控件样式键名与深色主题一致 |

## 主题系统工作原理

应用启动时通过 `ThemeService` 将其中一个资源字典加载到 `Application.Resources.MergedDictionaries`。切换主题时移除旧字典、加入新字典，配合 `DynamicResource` 绑定实现实时切换。
