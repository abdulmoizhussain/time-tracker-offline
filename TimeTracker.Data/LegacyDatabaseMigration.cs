using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TimeTracker.Data;

public static class LegacyDatabaseMigration
{
    /// <summary>Copies a consistent SQLite snapshot; never deletes the source or replaces a destination.</summary>
    public static Task<bool> MigrateAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
        => Task.Run(() => Migrate(sourcePath, destinationPath, cancellationToken), cancellationToken);

    private static bool Migrate(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        destinationPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase)) return false;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(destinationPath.ToUpperInvariant())));
        using var admission = new Mutex(false, "Local\\TimeTracker.StorageMigration." + identity);
        var acquired = false;
        string? temporary = null;
        try
        {
            try { acquired = admission.WaitOne(TimeSpan.FromSeconds(30)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new TimeoutException("Another TimeTracker copy is migrating this database. Please try again.");
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destinationPath) || !File.Exists(sourcePath)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            temporary = destinationPath + $".{Guid.NewGuid():N}.migration";
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString()))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = temporary, Pooling = false
            }.ToString()))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
                using var check = destination.CreateCommand();
                check.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.Ordinal))
                    throw new IOException("The old database failed its integrity check. It has been left untouched.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Preserve list preferences before publishing the database, so retries cannot skip them.
            var preferences = sourcePath + ".view.json";
            if (File.Exists(preferences) && !File.Exists(destinationPath + ".view.json"))
                File.Copy(preferences, destinationPath + ".view.json", overwrite: false);
            File.Move(temporary, destinationPath, overwrite: false);
            return true;
        }
        finally
        {
            try
            {
                if (temporary is not null && File.Exists(temporary)) File.Delete(temporary);
            }
            finally
            {
                if (acquired) admission.ReleaseMutex();
            }
        }
    }
}
