using System.IO;
using ClosedXML.Excel;
using Sylvan.Data.Csv;
using LogVision.Models;

namespace LogVision.Services;

public class CsvParsingService
{
    private static bool IsExcel(string filePath) =>
        Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".xls", StringComparison.OrdinalIgnoreCase);

    public async Task<CsvFileInfo> AnalyzeAsync(string filePath)
    {
        return IsExcel(filePath)
            ? await AnalyzeXlsxAsync(filePath)
            : await AnalyzeCsvAsync(filePath);
    }

    private async Task<CsvFileInfo> AnalyzeCsvAsync(string csvPath)
    {
        var info = new CsvFileInfo
        {
            FileName = Path.GetFileName(csvPath),
            FilePath = csvPath,
            FileType = CsvFileType.RunData
        };

        await Task.Run(() =>
        {
            using var reader = CsvDataReader.Create(csvPath);
            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                columns.Add(string.IsNullOrWhiteSpace(name) ? $"Column{i}" : name);
            }
            info.ColumnNames = columns;

            long count = 0;
            while (reader.Read()) count++;
            info.RowCount = count;
        });

        return info;
    }

    private async Task<CsvFileInfo> AnalyzeXlsxAsync(string xlsxPath)
    {
        var info = new CsvFileInfo
        {
            FileName = Path.GetFileName(xlsxPath),
            FilePath = xlsxPath,
            FileType = CsvFileType.RunData
        };

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook(xlsxPath);
            var worksheet = workbook.Worksheet(1);
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                info.ColumnNames = [];
                info.RowCount = 0;
                return;
            }

            var headerRow = usedRange.FirstRow();
            var columns = new List<string>();
            for (int c = 1; c <= usedRange.ColumnCount(); c++)
            {
                var val = headerRow.Cell(c).GetString();
                columns.Add(string.IsNullOrWhiteSpace(val) ? $"Column{c}" : val);
            }
            info.ColumnNames = columns;
            info.RowCount = Math.Max(0, usedRange.RowCount() - 1);
        });

        return info;
    }

    /// <summary>
    /// 解析运行数据。
    /// 返回 (列名列表(不含第一列时间戳), 数据行列表)。
    /// 每行 double[]：[TimestampMs, col1, col2, ...]
    /// </summary>
    public async Task<(List<string> columnNames, List<double[]> rows)> ParseRunDataAsync(
        string filePath, IProgress<int>? progress = null)
    {
        return IsExcel(filePath)
            ? await ParseRunDataFromXlsxAsync(filePath, progress)
            : await ParseRunDataFromCsvAsync(filePath, progress);
    }

    private async Task<(List<string> columnNames, List<double[]> rows)> ParseRunDataFromCsvAsync(
        string csvPath, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var reader = CsvDataReader.Create(csvPath);

            var allColumns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                allColumns.Add(string.IsNullOrWhiteSpace(name) ? $"Column{i}" : name);
            }

            // 第一列视为时间戳
            var dataColumns = allColumns.Skip(1).ToList();
            var rows = new List<double[]>();
            int totalEstimate = 0;

            while (reader.Read())
            {
                // 跳过时间戳为空的行
                if (reader.IsDBNull(0))
                    continue;

                var tsStr = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(tsStr))
                    continue;

                var row = new double[allColumns.Count];
                row[0] = TryParseTimestamp(tsStr);

                for (int i = 1; i < allColumns.Count; i++)
                {
                    if (!reader.IsDBNull(i))
                    {
                        var raw = reader.GetString(i);
                        if (!string.IsNullOrWhiteSpace(raw) && double.TryParse(raw, out var val))
                            row[i] = val;
                        else
                            row[i] = double.NaN;
                    }
                    else
                    {
                        row[i] = double.NaN;
                    }
                }

                rows.Add(row);
                totalEstimate++;

                if (totalEstimate % 5000 == 0)
                    progress?.Report(Math.Min(95, totalEstimate / 100));
            }

            progress?.Report(100);
            return (dataColumns, rows);
        });
    }

    private async Task<(List<string> columnNames, List<double[]> rows)> ParseRunDataFromXlsxAsync(
        string xlsxPath, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook(xlsxPath);
            var worksheet = workbook.Worksheet(1);
            var usedRange = worksheet.RangeUsed();

            if (usedRange == null)
            {
                progress?.Report(100);
                return (new List<string>(), new List<double[]>());
            }

            int colCount = usedRange.ColumnCount();
            int rowCount = usedRange.RowCount();

            // 第一行为列头
            var allColumns = new List<string>();
            var headerRow = usedRange.Row(1);
            for (int c = 1; c <= colCount; c++)
            {
                var val = headerRow.Cell(c).GetString();
                allColumns.Add(string.IsNullOrWhiteSpace(val) ? $"Column{c}" : val);
            }

            var dataColumns = allColumns.Skip(1).ToList();
            var rows = new List<double[]>();

            for (int r = 2; r <= rowCount; r++)
            {
                var xlRow = usedRange.Row(r);
                var tsCell = xlRow.Cell(1);

                if (tsCell.IsEmpty())
                    continue;

                var tsStr = tsCell.GetString();
                if (string.IsNullOrWhiteSpace(tsStr))
                    continue;

                var row = new double[colCount];
                row[0] = TryParseTimestamp(tsStr);

                for (int c = 2; c <= colCount; c++)
                {
                    var cell = xlRow.Cell(c);
                    if (!cell.IsEmpty())
                    {
                        var raw = cell.GetString();
                        if (!string.IsNullOrWhiteSpace(raw) && double.TryParse(raw, out var val))
                            row[c - 1] = val;
                        else
                            row[c - 1] = double.NaN;
                    }
                    else
                    {
                        row[c - 1] = double.NaN;
                    }
                }

                rows.Add(row);

                if ((r - 1) % 5000 == 0)
                    progress?.Report(Math.Min(95, (r - 1) * 100 / rowCount));
            }

            progress?.Report(100);
            return (dataColumns, rows);
        });
    }

    /// <summary>
    /// 解析操作事件。
    /// 预期格式：时间戳, 操作类型, 消息 (至少 2 列)
    /// </summary>
    public async Task<List<OperationEvent>> ParseOpEventsAsync(
        string filePath, IProgress<int>? progress = null)
    {
        return IsExcel(filePath)
            ? await ParseOpEventsFromXlsxAsync(filePath, progress)
            : await ParseOpEventsFromCsvAsync(filePath, progress);
    }

    private async Task<List<OperationEvent>> ParseOpEventsFromCsvAsync(
        string csvPath, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var reader = CsvDataReader.Create(csvPath);
            var events = new List<OperationEvent>();

            while (reader.Read())
            {
                // 跳过时间戳为空的行
                if (reader.IsDBNull(0))
                    continue;

                var tsStr = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(tsStr))
                    continue;

                var evt = new OperationEvent
                {
                    TimestampMs = TryParseTimestamp(tsStr),
                    User = (reader.FieldCount > 1 && !reader.IsDBNull(1))
                        ? reader.GetString(1) ?? "" : "",
                    Source = (reader.FieldCount > 2 && !reader.IsDBNull(2))
                        ? reader.GetString(2) ?? "" : "",
                    Result = (reader.FieldCount > 4 && !reader.IsDBNull(4))
                        ? reader.GetString(4) ?? "" : "",
                    Description = (reader.FieldCount > 5 && !reader.IsDBNull(5))
                        ? reader.GetString(5) ?? "" : ""
                };
                events.Add(evt);
            }

            progress?.Report(100);
            return events;
        });
    }

    private async Task<List<OperationEvent>> ParseOpEventsFromXlsxAsync(
        string xlsxPath, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var workbook = new XLWorkbook(xlsxPath);
            var worksheet = workbook.Worksheet(1);
            var usedRange = worksheet.RangeUsed();

            if (usedRange == null)
            {
                progress?.Report(100);
                return new List<OperationEvent>();
            }

            int colCount = usedRange.ColumnCount();
            int rowCount = usedRange.RowCount();
            var events = new List<OperationEvent>();

            // 跳过标题行
            for (int r = 2; r <= rowCount; r++)
            {
                var xlRow = usedRange.Row(r);
                var tsCell = xlRow.Cell(1);

                if (tsCell.IsEmpty())
                    continue;

                var tsStr = tsCell.GetString();
                if (string.IsNullOrWhiteSpace(tsStr))
                    continue;

                var evt = new OperationEvent
                {
                    TimestampMs = TryParseTimestamp(tsStr),
                    User = (colCount > 1 && !xlRow.Cell(2).IsEmpty())
                        ? xlRow.Cell(2).GetString() : "",
                    Source = (colCount > 2 && !xlRow.Cell(3).IsEmpty())
                        ? xlRow.Cell(3).GetString() : "",
                    Result = (colCount > 4 && !xlRow.Cell(5).IsEmpty())
                        ? xlRow.Cell(5).GetString() : "",
                    Description = (colCount > 5 && !xlRow.Cell(6).IsEmpty())
                        ? xlRow.Cell(6).GetString() : ""
                };
                events.Add(evt);
            }

            progress?.Report(100);
            return events;
        });
    }

    private static double TryParseTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0.0;

        // 直接数值（毫秒时间戳）
        if (double.TryParse(value, out var numericTs))
            return numericTs;

        // DateTime 格式
        if (DateTime.TryParse(value, out var dateTime))
            return dateTime.ToOADate() * 86400000.0; // 转为毫秒级

        return 0.0;
    }
}
