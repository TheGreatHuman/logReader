using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LogVision.Models;
using LogVision.ViewModels;

namespace LogVision;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.SetPlot(WpfPlot1);
        await _viewModel.InitializeAsync();

        // 时间线同步：图表交互后更新时间线标记
        WpfPlot1.SizeChanged += (s, ev) => UpdateTimeline();
        WpfPlot1.MouseUp += (s, ev) => UpdateTimeline();
        WpfPlot1.MouseWheel += (s, ev) =>
            Dispatcher.BeginInvoke(() => UpdateTimeline(),
                System.Windows.Threading.DispatcherPriority.Background);

        _viewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.OperationEvents))
                Dispatcher.BeginInvoke(() => UpdateTimeline(),
                    System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    private void WpfPlot1_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var position = e.GetPosition(WpfPlot1);
        var pixel = new ScottPlot.Pixel((float)position.X, (float)position.Y);
        var coordinates = WpfPlot1.Plot.GetCoordinates(pixel);

        _viewModel.OnChartClicked(coordinates.X);

        // 滚动 DataGrid 到选中行
        if (_viewModel.SelectedTableRow != null)
        {
            DataGridView.ScrollIntoView(_viewModel.SelectedTableRow);
        }
    }

    private void DataGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataGridView.SelectedItem is DataRowView row)
        {
            if (row.Row.Table.Columns.Contains("TimestampMs") &&
                row["TimestampMs"] is double ts)
            {
                _viewModel.OnTableRowSelected(ts);
            }
        }
    }

    private void OperationLogGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OperationLogGrid.SelectedItem is OperationEvent evt)
        {
            _viewModel.OnOperationEventSelected(evt);

            // 同步滚动数据详情表格
            if (_viewModel.SelectedTableRow != null)
            {
                DataGridView.ScrollIntoView(_viewModel.SelectedTableRow);
            }

            // 同步更新时间线
            UpdateTimeline();
        }
    }

    private void UpdateTimeline()
    {
        TimelineCanvas.Children.Clear();

        if (_viewModel.OperationEvents == null || _viewModel.OperationEvents.Count == 0)
            return;

        var (axisLeft, axisRight) = _viewModel.GetCurrentAxisLimits();
        var canvasWidth = TimelineCanvas.ActualWidth;
        var canvasHeight = TimelineCanvas.ActualHeight;

        if (canvasWidth <= 0 || axisRight <= axisLeft) return;

        bool isDark = App.ThemeService.EffectiveTheme == LogVision.Models.ThemeMode.Dark;
        var markerBrush = new SolidColorBrush(isDark
            ? (Color)ColorConverter.ConvertFromString("#F38BA8")
            : (Color)ColorConverter.ConvertFromString("#D32F2F"));
        var selectedBrush = new SolidColorBrush(isDark
            ? (Color)ColorConverter.ConvertFromString("#F9E2AF")
            : (Color)ColorConverter.ConvertFromString("#F57F17"));
        var strokeBrush = new SolidColorBrush(isDark
            ? (Color)ColorConverter.ConvertFromString("#CDD6F4")
            : (Color)ColorConverter.ConvertFromString("#1E1E2E"));

        double centerY = canvasHeight > 0 ? canvasHeight / 2.0 : 25;

        foreach (var evt in _viewModel.OperationEvents)
        {
            var ratio = (evt.TimestampMs - axisLeft) / (axisRight - axisLeft);
            if (ratio < -0.05 || ratio > 1.05) continue;

            var x = ratio * canvasWidth;
            bool isSelected = _viewModel.SelectedOperationEvent == evt;

            var marker = new Ellipse
            {
                Width = isSelected ? 14 : 10,
                Height = isSelected ? 14 : 10,
                Fill = isSelected ? selectedBrush : markerBrush,
                Stroke = strokeBrush,
                StrokeThickness = isSelected ? 2 : 1,
                Cursor = Cursors.Hand,
                ToolTip = $"{evt.FormattedTime}\n{evt.Result}\n{evt.Description}"
            };

            var capturedEvt = evt;
            marker.MouseDown += (s, e) =>
            {
                _viewModel.SelectedOperationEvent = capturedEvt;
                _viewModel.OnOperationEventSelected(capturedEvt);
                if (_viewModel.SelectedTableRow != null)
                    DataGridView.ScrollIntoView(_viewModel.SelectedTableRow);
                OperationLogGrid.ScrollIntoView(capturedEvt);
                UpdateTimeline();
                e.Handled = true;
            };

            Canvas.SetLeft(marker, x - marker.Width / 2);
            Canvas.SetTop(marker, centerY - marker.Height / 2);
            TimelineCanvas.Children.Add(marker);
        }
    }

    private void ColumnConfigToggle_Click(object sender, RoutedEventArgs e)
    {
        ColumnConfigPopup.IsOpen = ColumnConfigToggle.IsChecked == true;
    }

    private void ColumnConfigPopup_Closed(object sender, System.EventArgs e)
    {
        ColumnConfigToggle.IsChecked = false;
    }

    private void ColumnCheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Content is string columnName)
        {
            cb.IsChecked = _viewModel.SelectedColumns.Contains(columnName);
        }
    }

    private void ColumnCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Content is string columnName)
        {
            _viewModel.ToggleColumn(columnName);
        }
    }

    private void ColumnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var col in _viewModel.AvailableColumns)
        {
            if (!_viewModel.SelectedColumns.Contains(col))
                _viewModel.SelectedColumns.Add(col);
        }
        UpdateColumnCheckBoxes();
        _ = _viewModel.RefreshColumnsCommand.ExecuteAsync(null);
    }

    private void ColumnSelectNone_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedColumns.Clear();
        UpdateColumnCheckBoxes();
        _ = _viewModel.RefreshColumnsCommand.ExecuteAsync(null);
    }

    private void UpdateColumnCheckBoxes()
    {
        // 刷新 Popup 中所有 CheckBox 的选中状态
        if (ColumnConfigPopup.Child is Border border &&
            border.Child is DockPanel dock)
        {
            foreach (var element in LogicalTreeHelper.GetChildren(dock))
            {
                if (element is ScrollViewer sv && sv.Content is ItemsControl ic)
                {
                    for (int i = 0; i < ic.Items.Count; i++)
                    {
                        var container = ic.ItemContainerGenerator.ContainerFromIndex(i);
                        if (container != null)
                        {
                            var cb = FindChild<CheckBox>(container);
                            if (cb != null && cb.Content is string colName)
                            {
                                cb.IsChecked = _viewModel.SelectedColumns.Contains(colName);
                            }
                        }
                    }
                }
            }
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var found = FindChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
