using System.IO;
using System.IO.Compression;
using LogVision.Models;

namespace LogVision.Services;

public class ZipService
{
    public async Task<string> ExtractAsync(string zipPath, IProgress<int>? progress = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LogVision", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);
            progress?.Report(100);
        });

        return tempDir;
    }

    public List<CsvFileInfo> ScanCsvFiles(string folderPath)
    {
        var csvFiles = Directory.GetFiles(folderPath, "*.csv", SearchOption.AllDirectories);
        var xlsxFiles = Directory.GetFiles(folderPath, "*.xlsx", SearchOption.AllDirectories);
        var allFiles = csvFiles.Concat(xlsxFiles);
        var result = new List<CsvFileInfo>();

        foreach (var filePath in allFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var fileType = GuessFileType(fileName);

            result.Add(new CsvFileInfo
            {
                FileName = fileName,
                FilePath = filePath,
                IsSelected = true,
                FileType = fileType
            });
        }

        return result;
    }

    private static CsvFileType GuessFileType(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.Contains("event") || lower.Contains("op") ||
            lower.Contains("cmd") || lower.Contains("operation") ||
            lower.Contains("log"))
        {
            return CsvFileType.OpEvent;
        }
        return CsvFileType.RunData;
    }

    public void Cleanup(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
