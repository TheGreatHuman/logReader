using System.IO;
using Microsoft.Data.Sqlite;
using LogVision.Models;

namespace LogVision.Data;

public class LogDatabase
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public LogDatabase()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LogVision");
        Directory.CreateDirectory(appDataDir);
        _dbPath = Path.Combine(appDataDir, "logvision.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = """
            CREATE TABLE IF NOT EXISTS LogPackages (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT DEFAULT '',
                OriginalFileName TEXT,
                ImportDate TEXT NOT NULL,
                DataFolderPath TEXT,
                ColumnNames TEXT DEFAULT ''
            );
            """;

        await using var cmd = new SqliteCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task InsertLogPackageAsync(LogPackage package)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = """
            INSERT INTO LogPackages (Id, Name, Description, OriginalFileName, ImportDate, DataFolderPath, ColumnNames)
            VALUES (@Id, @Name, @Description, @OriginalFileName, @ImportDate, @DataFolderPath, @ColumnNames);
            """;

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", package.Id);
        cmd.Parameters.AddWithValue("@Name", package.Name);
        cmd.Parameters.AddWithValue("@Description", package.Description);
        cmd.Parameters.AddWithValue("@OriginalFileName", package.OriginalFileName);
        cmd.Parameters.AddWithValue("@ImportDate", package.ImportDate.ToString("O"));
        cmd.Parameters.AddWithValue("@DataFolderPath", package.DataFolderPath);
        cmd.Parameters.AddWithValue("@ColumnNames", string.Join("|", package.ColumnNames));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<LogPackage>> GetAllLogPackagesAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = "SELECT Id, Name, Description, OriginalFileName, ImportDate, DataFolderPath, ColumnNames FROM LogPackages ORDER BY ImportDate DESC;";
        await using var cmd = new SqliteCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var packages = new List<LogPackage>();
        while (await reader.ReadAsync())
        {
            var colNamesStr = reader.GetString(6);
            packages.Add(new LogPackage
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                OriginalFileName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ImportDate = DateTime.Parse(reader.GetString(4)),
                DataFolderPath = reader.IsDBNull(5) ? "" : reader.GetString(5),
                ColumnNames = string.IsNullOrEmpty(colNamesStr)
                    ? []
                    : [.. colNamesStr.Split('|', StringSplitOptions.RemoveEmptyEntries)]
            });
        }

        return packages;
    }

    public async Task UpdateLogPackageAsync(LogPackage package)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = """
            UPDATE LogPackages SET Name = @Name, Description = @Description
            WHERE Id = @Id;
            """;

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", package.Id);
        cmd.Parameters.AddWithValue("@Name", package.Name);
        cmd.Parameters.AddWithValue("@Description", package.Description);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteLogPackageAsync(string id)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var sql = "DELETE FROM LogPackages WHERE Id = @Id;";
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
