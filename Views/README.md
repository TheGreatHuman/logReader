# Views - UI 视图

本目录包含除主窗口以外的其他 WPF 窗口和对话框。

## 文件说明

| 文件 | 说明 |
|------|------|
| `ImportDialog.xaml` | 导入对话框的 XAML 布局。包含：日志名称输入框、描述输入框、CSV 文件选择列表（ListView + GridView，显示文件名/行数/列数，支持勾选和类型选择）、文件统计摘要、进度条、取消/确认按钮。使用动态资源绑定支持主题 |
| `ImportDialog.xaml.cs` | 导入对话框的代码隐藏。接收 CSV 文件列表和默认名称作为构造参数。`Confirm_Click` 验证名称非空和至少选择一个文件后设置 `DialogResult=true`，通过 `SelectedFiles`、`LogName`、`LogDescription` 属性向调用方返回用户的选择结果 |
