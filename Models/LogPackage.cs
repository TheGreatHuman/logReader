using CommunityToolkit.Mvvm.ComponentModel;

namespace LogVision.Models;

public partial class LogPackage : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _originalFileName = string.Empty;

    [ObservableProperty]
    private DateTime _importDate = DateTime.Now;

    [ObservableProperty]
    private string _dataFolderPath = string.Empty;

    /// <summary>
    /// RunData 表中除 TimestampMs 外的列名列表
    /// </summary>
    [ObservableProperty]
    private List<string> _columnNames = [];
}
