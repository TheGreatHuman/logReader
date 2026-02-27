using CommunityToolkit.Mvvm.ComponentModel;

namespace LogVision.Models;

public enum CsvFileType
{
    RunData,
    OpEvent
}

public partial class CsvFileInfo : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private List<string> _columnNames = [];

    [ObservableProperty]
    private long _rowCount;

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private CsvFileType _fileType = CsvFileType.RunData;
}
