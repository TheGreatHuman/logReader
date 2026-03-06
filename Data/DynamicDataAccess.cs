using System.Data;
using Microsoft.Data.Sqlite;
using LogVision.Models;

namespace LogVision.Data;

public class DynamicDataAccess
{
    private readonly string _connectionString;

    public DynamicDataAccess(string connectionString)
    {
        _connectionString = connectionString;
    }

    private static string SanitizeIdentifier(string name)
    {
        return name.Replace("\"", "\"\"");
    }

    private static string SafeTableId(string logId)
    {
        return logId.Replace("-", "");
    }

    public async Task CreateRunDataTableAsync(string logId, List<string> columnNames)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var colDefs = string.Join(",\n    ",
            columnNames.Select(c => $"\"{SanitizeIdentifier(c)}\" REAL"));

        var sql = $"""
            CREATE TABLE IF NOT EXISTS "RunData_{tableId}" (
                RowId INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampMs REAL NOT NULL,
                {colDefs}
            );
            CREATE INDEX IF NOT EXISTS "IDX_RunTime_{tableId}" ON "RunData_{tableId}"(TimestampMs);
            """;

        await using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CreateOpEventsTableAsync(string logId)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"""
            CREATE TABLE IF NOT EXISTS "OpEvents_{tableId}" (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TimestampMs REAL NOT NULL,
                User TEXT,
                Source TEXT,
                Result TEXT,
                Description TEXT
            );
            CREATE INDEX IF NOT EXISTS "IDX_OpTime_{tableId}" ON "OpEvents_{tableId}"(TimestampMs);
            """;

        await using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task BulkInsertRunDataAsync(string logId, List<string> columnNames, List<double[]> rows, IProgress<int>? progress = null)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var colList = string.Join(", ", columnNames.Select(c => $"\"{SanitizeIdentifier(c)}\""));
        var paramList = string.Join(", ", Enumerable.Range(0, columnNames.Count).Select(i => $"@p{i}"));
        var sql = $"INSERT INTO \"RunData_{tableId}\" (TimestampMs, {colList}) VALUES (@ts, {paramList});";

        var totalRows = rows.Count;
        var batchSize = 10000;

        for (int batchStart = 0; batchStart < totalRows; batchStart += batchSize)
        {
            var batchEnd = Math.Min(batchStart + batchSize, totalRows);

            await using var transaction = conn.BeginTransaction();
            await using var cmd = new SqliteCommand(sql, conn, transaction);

            cmd.Parameters.Add(new SqliteParameter("@ts", SqliteType.Real));
            for (int i = 0; i < columnNames.Count; i++)
                cmd.Parameters.Add(new SqliteParameter($"@p{i}", SqliteType.Real));

            for (int r = batchStart; r < batchEnd; r++)
            {
                var row = rows[r];
                cmd.Parameters["@ts"].Value = row[0];
                for (int i = 0; i < columnNames.Count; i++)
                {
                    var v = (i + 1 < row.Length) ? row[i + 1] : double.NaN;
                    cmd.Parameters[$"@p{i}"].Value = double.IsNaN(v) ? DBNull.Value : v;
                }
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            progress?.Report((int)((double)batchEnd / totalRows * 100));
        }
    }

    public async Task BulkInsertOpEventsAsync(string logId, List<OperationEvent> events)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"INSERT INTO \"OpEvents_{tableId}\" (TimestampMs, User, Source, Result, Description) VALUES (@ts, @user, @source, @result, @desc);";

        await using var transaction = conn.BeginTransaction();
        await using var cmd = new SqliteCommand(sql, conn, transaction);
        cmd.Parameters.Add(new SqliteParameter("@ts", SqliteType.Real));
        cmd.Parameters.Add(new SqliteParameter("@user", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("@source", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("@result", SqliteType.Text));
        cmd.Parameters.Add(new SqliteParameter("@desc", SqliteType.Text));

        foreach (var evt in events)
        {
            cmd.Parameters["@ts"].Value = evt.TimestampMs;
            cmd.Parameters["@user"].Value = evt.User;
            cmd.Parameters["@source"].Value = evt.Source;
            cmd.Parameters["@result"].Value = evt.Result;
            cmd.Parameters["@desc"].Value = evt.Description;
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<(double[] timestamps, double[] values)> LoadColumnDataAsync(string logId, string columnName)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"SELECT TimestampMs, \"{SanitizeIdentifier(columnName)}\" FROM \"RunData_{tableId}\" ORDER BY TimestampMs;";
        await using var cmd = new SqliteCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var timestamps = new List<double>();
        var values = new List<double>();

        while (await reader.ReadAsync())
        {
            timestamps.Add(reader.GetDouble(0));
            values.Add(reader.IsDBNull(1) ? double.NaN : reader.GetDouble(1));
        }

        return (timestamps.ToArray(), values.ToArray());
    }

    public async Task<DataTable> LoadPagedDataAsync(string logId, List<string> columnNames, int offset = 0, int limit = 50000)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cols = "RowId, TimestampMs, " + string.Join(", ", columnNames.Select(c => $"\"{SanitizeIdentifier(c)}\""));
        var sql = $"SELECT {cols} FROM \"RunData_{tableId}\" ORDER BY TimestampMs LIMIT @limit OFFSET @offset;";

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync();

        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    public async Task<List<OperationEvent>> LoadOpEventsAsync(string logId)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"SELECT Id, TimestampMs, User, Source, Result, Description FROM \"OpEvents_{tableId}\" ORDER BY TimestampMs;";
        await using var cmd = new SqliteCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var events = new List<OperationEvent>();
        while (await reader.ReadAsync())
        {
            events.Add(new OperationEvent
            {
                Id = reader.GetInt64(0),
                TimestampMs = reader.GetDouble(1),
                User = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Source = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Result = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Description = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }

        return events;
    }

    public async Task<int> QueryRowByTimestampAsync(string logId, double timestampMs)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"SELECT RowId FROM \"RunData_{tableId}\" ORDER BY ABS(TimestampMs - @ts) LIMIT 1;";
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ts", timestampMs);

        var result = await cmd.ExecuteScalarAsync();
        return result is long rowId ? (int)rowId : -1;
    }

    public async Task<long> GetRowCountAsync(string logId)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"SELECT COUNT(*) FROM \"RunData_{tableId}\";";
        await using var cmd = new SqliteCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is long count ? count : 0;
    }

    public async Task DropTablesAsync(string logId)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd1 = new SqliteCommand($"DROP TABLE IF EXISTS \"RunData_{tableId}\";", conn);
        await cmd1.ExecuteNonQueryAsync();

        await using var cmd2 = new SqliteCommand($"DROP TABLE IF EXISTS \"OpEvents_{tableId}\";", conn);
        await cmd2.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasOpEventsTableAsync(string logId)
    {
        var tableId = SafeTableId(logId);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='OpEvents_{tableId}';";
        await using var cmd = new SqliteCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is long count && count > 0;
    }
}
