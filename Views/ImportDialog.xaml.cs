using System.Windows;
using LogVision.Models;

namespace LogVision.Views;

public partial class ImportDialog : Window
{
    public List<CsvFileInfo> SelectedFiles { get; private set; } = [];
    public string LogName { get; private set; } = string.Empty;
    public string LogDescription { get; private set; } = string.Empty;

    public ImportDialog(List<CsvFileInfo> files, string defaultName)
    {
        InitializeComponent();
        FileListView.ItemsSource = files;
        TxtLogName.Text = defaultName;
        UpdateSummary(files);
    }

    private void UpdateSummary(List<CsvFileInfo> files)
    {
        var totalRows = files.Where(f => f.IsSelected).Sum(f => f.RowCount);
        var selectedCount = files.Count(f => f.IsSelected);
        TxtSummary.Text = $"已选择 {selectedCount}/{files.Count} 个文件，共计约 {totalRows:N0} 行数据";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtLogName.Text))
        {
            MessageBox.Show("请输入日志名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtLogName.Focus();
            return;
        }

        var files = FileListView.ItemsSource as List<CsvFileInfo>;
        SelectedFiles = files?.Where(f => f.IsSelected).ToList() ?? [];

        if (SelectedFiles.Count == 0)
        {
            MessageBox.Show("请至少选择一个文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LogName = TxtLogName.Text.Trim();
        LogDescription = TxtDescription.Text?.Trim() ?? "";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
