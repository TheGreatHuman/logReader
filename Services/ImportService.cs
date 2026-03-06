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

        // ── 1. 解析所有运行数据文件 ──
        var parsedRunData = new List<(List<string> columnNames, List<double[]> rows)>();

        for (int fi = 0; fi < runDataFiles.Count; fi++)
        {
            var file = runDataFiles[fi];
            progress?.Report(($"解析运行数据: {file.FileName}", (fi + 1) * 30 / runDataFiles.Count));

            var parseProgress = new Progress<int>(p =>
                progress?.Report(($"解析: {file.FileName}", p * 30 / 100)));

            var (columnNames, rows) = await _csvParser.ParseRunDataAsync(file.FilePath, parseProgress);

            if (columnNames.Count == 0 || rows.Count == 0) continue;

            parsedRunData.Add((columnNames, rows));

            // 收集所有列名（去重保序）
            foreach (var col in columnNames.Where(c => !allColumnNames.Contains(c)))
                allColumnNames.Add(col);

            // 复制源文件到数据目录
            var destPath = Path.Combine(dataDir, file.FileName);
            File.Copy(file.FilePath, destPath, overwrite: true);
        }

        // ── 2. 合并多个运行数据文件（Full Outer Join on Timestamp） ──
        if (parsedRunData.Count > 0 && allColumnNames.Count > 0)
        {
            progress?.Report(("合并运行数据...", 35));

            var mergedData = new SortedDictionary<double, double[]>();

            foreach (var (columnNames, rows) in parsedRunData)
            {
                // 建立列映射：文件列索引 → 合并后列索引
                var colMapping = new int[columnNames.Count];
                for (int i = 0; i < columnNames.Count; i++)
                    colMapping[i] = allColumnNames.IndexOf(columnNames[i]);

                foreach (var row in rows)
                {
                    var ts = row[0];
                    if (!mergedData.TryGetValue(ts, out var mergedRow))
                    {
                        mergedRow = new double[allColumnNames.Count + 1]; // [ts, col1, col2, ...]
                        mergedRow[0] = ts;
                        for (int k = 1; k < mergedRow.Length; k++)
                            mergedRow[k] = double.NaN;
                        mergedData[ts] = mergedRow;
                    }

                    for (int i = 0; i < columnNames.Count; i++)
                        mergedRow[colMapping[i] + 1] = row[i + 1];
                }
            }

            // ── 3. 建表并批量写入合并后的数据 ──
            progress?.Report(("写入数据库...", 50));

            await _dynamicAccess.CreateRunDataTableAsync(logId, allColumnNames);

            var insertProgress = new Progress<int>(p =>
                progress?.Report(($"写入数据库", 50 + p * 30 / 100)));

            var mergedRows = mergedData.Values.ToList();
            await _dynamicAccess.BulkInsertRunDataAsync(logId, allColumnNames, mergedRows, insertProgress);
        }

        // 处理操作事件文件
        foreach (var file in opEventFiles)
        {
            progress?.Report(($"解析操作事件: {file.FileName}", 85));

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
