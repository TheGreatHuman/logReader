using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void ColumnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton toggle &&
            toggle.Content is string columnName)
        {
            _viewModel.ToggleColumn(columnName);
        }
    }
}
