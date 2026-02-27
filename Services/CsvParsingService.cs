using System.IO;
using Sylvan.Data.Csv;
using LogVision.Models;

namespace LogVision.Services;

public class CsvParsingService
{
    public async Task<CsvFileInfo> AnalyzeAsync(string csvPath)
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
                columns.Add(reader.GetName(i));
            info.ColumnNames = columns;

            long count = 0;
            while (reader.Read()) count++;
            info.RowCount = count;
        });

        return info;
    }

    /// <summary>
    /// 解析运行数据 CSV。
    /// 返回 (列名列表(不含第一列时间戳), 数据行列表)。
    /// 每行 double[]：[TimestampMs, col1, col2, ...]
    /// </summary>
    public async Task<(List<string> columnNames, List<double[]> rows)> ParseRunDataAsync(
        string csvPath, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var reader = CsvDataReader.Create(csvPath);

            var allColumns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
                allColumns.Add(reader.GetName(i));

            // 第一列视为时间戳
            var dataColumns = allColumns.Skip(1).ToList();
            var rows = new List<double[]>();
            int totalEstimate = 0;

            while (reader.Read())
            {
                var row = new double[allColumns.Count];

                // 尝试解析第一列为时间戳
                row[0] = TryParseTimestamp(reader.GetString(0));

                for (int i = 1; i < allColumns.Count; i++)
                {
                    if (!reader.IsDBNull(i) && double.TryParse(reader.GetString(i), out var val))
                        row[i] = val;
                    else
                        row[i] = double.NaN;
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

    /// <summary>
    /// 解析操作事件 CSV。
    /// 预期格式：时间戳, 操作类型, 消息 (至少 2 列)
    /// </summary>
    public async Task<List<OperationEvent>> ParseOpEventsAsync(
        string csvPath, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            using var reader = CsvDataReader.Create(csvPath);
            var events = new List<OperationEvent>();

            while (reader.Read())
            {
                var evt = new OperationEvent
                {
                    TimestampMs = TryParseTimestamp(reader.GetString(0)),
                    OperationType = reader.FieldCount > 1 ? reader.GetString(1) : "",
                    Message = reader.FieldCount > 2 ? reader.GetString(2) : ""
                };
                events.Add(evt);
            }

            progress?.Report(100);
            return events;
        });
    }

    private static double TryParseTimestamp(string value)
    {
        // 直接数值（毫秒时间戳）
        if (double.TryParse(value, out var numericTs))
            return numericTs;

        // DateTime 格式
        if (DateTime.TryParse(value, out var dateTime))
            return dateTime.ToOADate() * 86400000.0; // 转为毫秒级

        return 0.0;
    }
}
