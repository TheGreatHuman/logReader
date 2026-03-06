using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogVision.Data;
using LogVision.Models;
using LogVision.Services;
using Microsoft.Win32;
using ScottPlot;

namespace LogVision.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LogDatabase _db;
    private readonly DynamicDataAccess _dynamicAccess;
    private readonly ZipService _zipService;
    private readonly CsvParsingService _csvParser;
    private readonly ImportService _importService;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private ObservableCollection<LogPackage> _logPackages = [];

    [ObservableProperty]
    private LogPackage? _selectedLogPackage;

    [ObservableProperty]
    private DataTable? _tableData;

    [ObservableProperty]
    private ObservableCollection<string> _availableColumns = [];

    [ObservableProperty]
    private ObservableCollection<string> _selectedColumns = [];

    [ObservableProperty]
    private DataRowView? _selectedTableRow;

    [ObservableProperty]
    private ObservableCollection<OperationEvent> _operationEvents = [];

    [ObservableProperty]
    private OperationEvent? _selectedOperationEvent;

    [ObservableProperty]
    private ThemeMode _themeMode;

    // ScottPlot WpfPlot reference (set from code-behind)
    private ScottPlot.WPF.WpfPlot? _wpfPlot;

    // Events for chart-table linkage
    public event Action<double>? ChartPointClicked;
    public event Action<double>? TableRowSelected;

    public MainViewModel()
    {
        _db = new LogDatabase();
        _dynamicAccess = new DynamicDataAccess(_db.ConnectionString);
        _zipService = new ZipService();
        _csvParser = new CsvParsingService();
        _importService = new ImportService(_zipService, _csvParser, _db, _dynamicAccess);

        _themeMode = App.ThemeService.CurrentMode;
        App.ThemeService.ThemeChanged += OnThemeChanged;
    }

    partial void OnThemeModeChanged(ThemeMode value)
    {
        App.ThemeService.ApplyTheme(value);
    }

    private void OnThemeChanged()
    {
        ConfigurePlotStyle();
        // Re-render chart data if a package is loaded
        if (SelectedLogPackage != null && SelectedColumns.Count > 0)
        {
            _ = RefreshColumnsAsync();
        }
    }

    public void SetPlot(ScottPlot.WPF.WpfPlot wpfPlot)
    {
        _wpfPlot = wpfPlot;
        ConfigurePlotStyle();
    }

    private void ConfigurePlotStyle()
    {
        if (_wpfPlot == null) return;
        var plot = _wpfPlot.Plot;

        bool isDark = App.ThemeService.EffectiveTheme == ThemeMode.Dark;

        if (isDark)
        {
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E2E");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#2B2B3D");
            plot.Axes.Bottom.Label.ForeColor = ScottPlot.Color.FromHex("#CDD6F4");
            plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#CDD6F4");
            plot.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#A6ADC8");
            plot.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#A6ADC8");
            plot.Axes.Bottom.MajorTickStyle.Color = ScottPlot.Color.FromHex("#585B70");
            plot.Axes.Left.MajorTickStyle.Color = ScottPlot.Color.FromHex("#585B70");
            plot.Axes.Bottom.MinorTickStyle.Color = ScottPlot.Color.FromHex("#45475A");
            plot.Axes.Left.MinorTickStyle.Color = ScottPlot.Color.FromHex("#45475A");
            plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromHex("#585B70");
            plot.Axes.Left.FrameLineStyle.Color = ScottPlot.Color.FromHex("#585B70");
            plot.Axes.Right.FrameLineStyle.Color = ScottPlot.Color.FromHex("#585B70");
            plot.Axes.Top.FrameLineStyle.Color = ScottPlot.Color.FromHex("#585B70");
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#313244");
        }
        else
        {
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#F5F5F5");
            plot.Axes.Bottom.Label.ForeColor = ScottPlot.Color.FromHex("#1E1E2E");
            plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#1E1E2E");
            plot.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#5C5C5C");
            plot.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#5C5C5C");
            plot.Axes.Bottom.MajorTickStyle.Color = ScottPlot.Color.FromHex("#C0C0C0");
            plot.Axes.Left.MajorTickStyle.Color = ScottPlot.Color.FromHex("#C0C0C0");
            plot.Axes.Bottom.MinorTickStyle.Color = ScottPlot.Color.FromHex("#D9D9D9");
            plot.Axes.Left.MinorTickStyle.Color = ScottPlot.Color.FromHex("#D9D9D9");
            plot.Axes.Bottom.FrameLineStyle.Color = ScottPlot.Color.FromHex("#C0C0C0");
            plot.Axes.Left.FrameLineStyle.Color = ScottPlot.Color.FromHex("#C0C0C0");
            plot.Axes.Right.FrameLineStyle.Color = ScottPlot.Color.FromHex("#C0C0C0");
            plot.Axes.Top.FrameLineStyle.Color = ScottPlot.Color.FromHex("#C0C0C0");
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#E8E8E8");
        }

        _wpfPlot.Refresh();
    }

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        await LoadLogPackagesAsync();
    }

    private async Task LoadLogPackagesAsync()
    {
        var packages = await _db.GetAllLogPackagesAsync();
        LogPackages = new ObservableCollection<LogPackage>(packages);
    }

    [RelayCommand]
    private async Task ImportZipAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择日志压缩包",
            Filter = "ZIP 文件|*.zip|所有文件|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusMessage = "正在解压...";
        ProgressValue = 0;

        string? tempDir = null;

        try
        {
            // 1. 解压
            tempDir = await _zipService.ExtractAsync(dialog.FileName,
                new Progress<int>(p => ProgressValue = p / 4));

            // 2. 扫描 CSV
            var csvFiles = _zipService.ScanCsvFiles(tempDir);

            if (csvFiles.Count == 0)
            {
                StatusMessage = "ZIP 包中未发现 CSV/XLSX 文件";
                return;
            }

            // 3. 分析每个文件
            StatusMessage = "正在分析文件结构...";
            foreach (var file in csvFiles)
            {
                var info = await _csvParser.AnalyzeAsync(file.FilePath);
                file.ColumnNames = info.ColumnNames;
                file.RowCount = info.RowCount;
            }

            // 4. 显示导入对话框
            var importDialog = new Views.ImportDialog(csvFiles, Path.GetFileNameWithoutExtension(dialog.FileName));
            importDialog.Owner = System.Windows.Application.Current.MainWindow;

            if (importDialog.ShowDialog() != true) return;

            var selectedFiles = importDialog.SelectedFiles;
            var logName = importDialog.LogName;
            var logDescription = importDialog.LogDescription;

            if (selectedFiles.Count == 0) return;

            // 5. 执行导入
            StatusMessage = "正在导入数据...";
            var progress = new Progress<(string stage, int percent)>(p =>
            {
                StatusMessage = p.stage;
                ProgressValue = p.percent;
            });

            var package = await _importService.ImportAsync(
                dialog.FileName, selectedFiles, logName, logDescription, progress);

            // 6. 刷新列表并选中
            await LoadLogPackagesAsync();
            SelectedLogPackage = LogPackages.FirstOrDefault(p => p.Id == package.Id);
            StatusMessage = $"导入完成: {package.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
            if (tempDir != null) _zipService.Cleanup(tempDir);
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedPackageAsync()
    {
        if (SelectedLogPackage == null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定删除日志包 \"{SelectedLogPackage.Name}\"？\n此操作不可恢复。",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            StatusMessage = "正在删除...";

            var id = SelectedLogPackage.Id;
            var dataPath = SelectedLogPackage.DataFolderPath;

            // 删除数据表
            await _dynamicAccess.DropTablesAsync(id);

            // 删除元数据
            await _db.DeleteLogPackageAsync(id);

            // 删除文件
            if (!string.IsNullOrEmpty(dataPath) && Directory.Exists(dataPath))
            {
                try { Directory.Delete(dataPath, recursive: true); }
                catch { /* best effort */ }
            }

            // 清空当前显示
            ClearCurrentView();

            await LoadLogPackagesAsync();
            StatusMessage = "删除完成";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedLogPackageChanged(LogPackage? value)
    {
        if (value != null)
        {
            _ = LoadLogPackageDataAsync(value);
        }
        else
        {
            ClearCurrentView();
        }
    }

    private async Task LoadLogPackageDataAsync(LogPackage package)
    {
        try
        {
            IsBusy = true;
            StatusMessage = $"正在加载: {package.Name}...";

            // 更新可用列
            AvailableColumns = new ObservableCollection<string>(package.ColumnNames);

            // 默认选择前 5 列
            var defaultCols = package.ColumnNames.Take(5).ToList();
            SelectedColumns = new ObservableCollection<string>(defaultCols);

            // 加载图表
            await LoadChartDataAsync(package.Id, defaultCols);

            // 加载表格
            await LoadTableDataAsync(package.Id, package.ColumnNames);

            // 加载操作事件标注
            await LoadOpEventsAsync(package.Id);

            StatusMessage = $"已加载: {package.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static readonly ScottPlot.Color[] DarkChartColors =
    [
        ScottPlot.Color.FromHex("#89B4FA"),
        ScottPlot.Color.FromHex("#A6E3A1"),
        ScottPlot.Color.FromHex("#F9E2AF"),
        ScottPlot.Color.FromHex("#F38BA8"),
        ScottPlot.Color.FromHex("#CBA6F7"),
        ScottPlot.Color.FromHex("#74C7EC"),
        ScottPlot.Color.FromHex("#FAB387"),
        ScottPlot.Color.FromHex("#94E2D5"),
    ];

    private static readonly ScottPlot.Color[] LightChartColors =
    [
        ScottPlot.Color.FromHex("#1A73E8"),
        ScottPlot.Color.FromHex("#2E7D32"),
        ScottPlot.Color.FromHex("#F57F17"),
        ScottPlot.Color.FromHex("#D32F2F"),
        ScottPlot.Color.FromHex("#7B1FA2"),
        ScottPlot.Color.FromHex("#0097A7"),
        ScottPlot.Color.FromHex("#E65100"),
        ScottPlot.Color.FromHex("#00897B"),
    ];

    private ScottPlot.Color[] GetCurrentChartColors()
    {
        return App.ThemeService.EffectiveTheme == ThemeMode.Dark ? DarkChartColors : LightChartColors;
    }

    private async Task LoadChartDataAsync(string logId, List<string> columns)
    {
        if (_wpfPlot == null) return;

        var plot = _wpfPlot.Plot;
        plot.Clear();
        ConfigurePlotStyle();

        plot.Axes.Bottom.Label.Text = "时间";

        // 自定义X轴刻度标签：将毫秒时间戳转为日期显示
        var tickGen = new ScottPlot.TickGenerators.NumericAutomatic
        {
            LabelFormatter = value =>
            {
                try
                {
                    return DateTime.FromOADate(value / 86400000.0).ToString("yyyy/MM/dd\nHH:mm:ss.fff");
                }
                catch
                {
                    return value.ToString("F0");
                }
            }
        };
        plot.Axes.Bottom.TickGenerator = tickGen;

        bool isDark = App.ThemeService.EffectiveTheme == ThemeMode.Dark;
        var chartColors = GetCurrentChartColors();

        int colorIndex = 0;
        foreach (var col in columns)
        {
            var (timestamps, values) = await _dynamicAccess.LoadColumnDataAsync(logId, col);
            if (timestamps.Length == 0) continue;

            var sig = plot.Add.SignalXY(timestamps, values);
            sig.LegendText = col;
            sig.Color = chartColors[colorIndex % chartColors.Length];
            sig.LineWidth = 1.5f;

            colorIndex++;
        }

        plot.Legend.IsVisible = true;
        plot.Legend.Alignment = Alignment.UpperRight;
        plot.Legend.FontColor = isDark
            ? ScottPlot.Color.FromHex("#CDD6F4")
            : ScottPlot.Color.FromHex("#1E1E2E");
        plot.Legend.BackgroundColor = isDark
            ? ScottPlot.Color.FromHex("#313244")
            : ScottPlot.Color.FromHex("#F5F5F5");
        plot.Legend.OutlineColor = isDark
            ? ScottPlot.Color.FromHex("#585B70")
            : ScottPlot.Color.FromHex("#C0C0C0");

        plot.Axes.AutoScale();
        _wpfPlot.Refresh();
    }

    private async Task LoadOpEventsAsync(string logId)
    {
        var hasEvents = await _dynamicAccess.HasOpEventsTableAsync(logId);
        if (!hasEvents)
        {
            OperationEvents = [];
            return;
        }

        var events = await _dynamicAccess.LoadOpEventsAsync(logId);

        // 填充操作事件集合供右侧面板和时间线使用
        OperationEvents = new ObservableCollection<OperationEvent>(events);
    }

    private async Task LoadTableDataAsync(string logId, List<string> columns)
    {
        TableData = await _dynamicAccess.LoadPagedDataAsync(logId, columns);
    }

    [RelayCommand]
    private async Task RefreshColumnsAsync()
    {
        if (SelectedLogPackage == null || SelectedColumns.Count == 0) return;
        await LoadChartDataAsync(SelectedLogPackage.Id, SelectedColumns.ToList());
        await LoadOpEventsAsync(SelectedLogPackage.Id);
    }

    public void ToggleColumn(string columnName)
    {
        if (SelectedColumns.Contains(columnName))
            SelectedColumns.Remove(columnName);
        else
            SelectedColumns.Add(columnName);

        _ = RefreshColumnsAsync();
    }

    public void OnChartClicked(double timestampMs)
    {
        ChartPointClicked?.Invoke(timestampMs);

        // 在表格中定位到对应行
        if (TableData == null) return;

        DataRow? closestRow = null;
        double minDiff = double.MaxValue;

        foreach (DataRow row in TableData.Rows)
        {
            if (row["TimestampMs"] is double ts)
            {
                var diff = Math.Abs(ts - timestampMs);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestRow = row;
                }
            }
        }

        if (closestRow != null)
        {
            SelectedTableRow = TableData.DefaultView[TableData.Rows.IndexOf(closestRow)];
        }
    }

    public void OnTableRowSelected(double timestampMs)
    {
        TableRowSelected?.Invoke(timestampMs);
        ZoomToTimestamp(timestampMs);
    }

    public void OnOperationEventSelected(OperationEvent evt)
    {
        ZoomToTimestamp(evt.TimestampMs);
        OnChartClicked(evt.TimestampMs);
    }

    public (double Left, double Right) GetCurrentAxisLimits()
    {
        if (_wpfPlot == null) return (0, 0);
        var limits = _wpfPlot.Plot.Axes.GetLimits();
        return (limits.Left, limits.Right);
    }

    public void ZoomToTimestamp(double timestampMs)
    {
        if (_wpfPlot == null) return;

        var limits = _wpfPlot.Plot.Axes.GetLimits();
        var xSpan = (limits.Right - limits.Left) / 4;

        _wpfPlot.Plot.Axes.SetLimitsX(timestampMs - xSpan, timestampMs + xSpan);

        // 添加临时高亮线
        var marker = _wpfPlot.Plot.Add.VerticalLine(timestampMs);
        bool isDark = App.ThemeService.EffectiveTheme == ThemeMode.Dark;
        marker.Color = (isDark
            ? ScottPlot.Color.FromHex("#F9E2AF")
            : ScottPlot.Color.FromHex("#F57F17")).WithAlpha(200);
        marker.LineWidth = 2;

        _wpfPlot.Refresh();
    }

    private void ClearCurrentView()
    {
        if (_wpfPlot != null)
        {
            _wpfPlot.Plot.Clear();
            ConfigurePlotStyle();
            _wpfPlot.Refresh();
        }

        TableData = null;
        AvailableColumns.Clear();
        SelectedColumns.Clear();
        OperationEvents = [];
        SelectedLogPackage = null;
    }
}
