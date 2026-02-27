using System.IO;
using LogVision.Data;
using LogVision.Models;

namespace LogVision.Services;

public class ImportService
{
    private readonly ZipService _zipService;
    private readonly CsvParsingService _csvParser;
    private readonly LogDatabase _db;
    private readonly DynamicDataAccess _dynamicAccess;

    public ImportService(ZipService zipService, CsvParsingService csvParser, LogDatabase db, DynamicDataAccess dynamicAccess)
    {
        _zipService = zipService;
        _csvParser = csvParser;
        _db = db;
        _dynamicAccess = dynamicAccess;
    }

    public async Task<LogPackage> ImportAsync(
        string zipPath,
        List<CsvFileInfo> selectedFiles,
        string name,
        string description,
        IProgress<(string stage, int percent)>? progress = null)
    {
        var logId = Guid.NewGuid().ToString("N");

        // 创建数据目录
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LogVision", "Data", logId);
        Directory.CreateDirectory(dataDir);

        var package = new LogPackage
        {
            Id = logId,
            Name = name,
            Description = description,
            OriginalFileName = Path.GetFileName(zipPath),
            ImportDate = DateTime.Now,
            DataFolderPath = dataDir,
            ColumnNames = []
        };

        var allColumnNames = new List<string>();

        // 处理运行数据文件
        var runDataFiles = selectedFiles.Where(f => f.FileType == CsvFileType.RunData).ToList();
        var opEventFiles = selectedFiles.Where(f => f.FileType == CsvFileType.OpEvent).ToList();

        int fileIndex = 0;
        int totalFiles = selectedFiles.Count;

        foreach (var file in runDataFiles)
        {
            fileIndex++;
            progress?.Report(($"解析运行数据: {file.FileName}", fileIndex * 100 / totalFiles / 2));

            var parseProgress = new Progress<int>(p =>
                progress?.Report(($"解析: {file.FileName}", p / 2)));

            var (columnNames, rows) = await _csvParser.ParseRunDataAsync(file.FilePath, parseProgress);

            if (columnNames.Count == 0 || rows.Count == 0) continue;

            // 合并列名（多个 CSV 可能有不同列）
            foreach (var col in columnNames.Where(c => !allColumnNames.Contains(c)))
                allColumnNames.Add(col);

            // 创建表并插入数据
            await _dynamicAccess.CreateRunDataTableAsync(logId, columnNames);

            var insertProgress = new Progress<int>(p =>
                progress?.Report(($"写入数据库: {file.FileName}", 50 + p / 2)));

            await _dynamicAccess.BulkInsertRunDataAsync(logId, columnNames, rows, insertProgress);

            // 复制源文件到数据目录
            var destPath = Path.Combine(dataDir, file.FileName);
            File.Copy(file.FilePath, destPath, overwrite: true);
        }

        // 处理操作事件文件
        foreach (var file in opEventFiles)
        {
            fileIndex++;
            progress?.Report(($"解析操作事件: {file.FileName}", 80));

            await _dynamicAccess.CreateOpEventsTableAsync(logId);
            var events = await _csvParser.ParseOpEventsAsync(file.FilePath);
            await _dynamicAccess.BulkInsertOpEventsAsync(logId, events);

            var destPath = Path.Combine(dataDir, file.FileName);
            File.Copy(file.FilePath, destPath, overwrite: true);
        }

        package.ColumnNames = allColumnNames;

        // 保存元数据
        await _db.InsertLogPackageAsync(package);
        progress?.Report(("导入完成", 100));

        return package;
    }
}
