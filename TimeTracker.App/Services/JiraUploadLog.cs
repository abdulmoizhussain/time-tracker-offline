using System.Globalization;
using System.IO;
using System.Text;

namespace TimeTracker.App.Services;

public sealed class JiraUploadLog(string databasePath)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string DirectoryPath { get; } = Path.GetDirectoryName(Path.GetFullPath(databasePath))!;

    public async Task WriteAsync(string level, string message)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTime.Now;
            var line = $"{now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} worker [{level}]: {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(Path.Combine(DirectoryPath, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log"),
                line, Encoding.UTF8).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
