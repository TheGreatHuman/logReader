# Converters - WPF 值转换器

本目录包含 WPF 数据绑定所需的 `IValueConverter` 实现，用于在 XAML 绑定中将数据类型转换为 UI 控件所需的类型。

## 文件说明

| 文件 | 说明 |
|------|------|
| `BoolToVisibilityConverter.cs` | 包含两个转换器：`BoolToVisibilityConverter` 将 `bool` 转为 `Visibility`（true=Visible, false=Collapsed）；`InverseBoolToVisibilityConverter` 为其反向逻辑（true=Collapsed, false=Visible），用于控制空状态提示的显示 |
| `ThemeModeConverter.cs` | 将 `ThemeMode` 枚举（Dark/Light/Auto）与 ComboBox 的 `int` 索引（0/1/2）进行双向转换，支持主题切换下拉框的数据绑定 |
