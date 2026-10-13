using System.IO;
using Microsoft.Data.Sqlite;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Configuration;
using TimeTracker.Data;
using Xunit;

namespace TimeTracker.Tests;

public sealed class AppStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TimeTracker.StorageTests", Guid.NewGuid().ToString("N"));
    private string Install => Path.Combine(_root, "install");
    private string Local => Path.Combine(_root, "local");
    private string Source => Path.Combine(Install, "Data", "timetracker.db");
    private string Destination => Path.Combine(Local, "TimeTracker", "Data", "timetracker.db");

    [Fact]
    public void DefaultUsesAppDataAndRecognizesOldInstallationDatabase()
    {
        var paths = AppStoragePaths.Resolve(new(), Install, Local, []);
        Assert.Equal(Destination, paths.DatabasePath);
        Assert.Equal(Source, paths.LegacyDatabasePath);
        Assert.Equal(Path.Combine(Local, "TimeTracker"), paths.DataDirectory);
    }

    [Fact]
    public void PortableModeExplicitlyUsesInstallationDirectory()
    {
        var paths = AppStoragePaths.Resolve(new() { PortableMode = true }, Install, Local, []);
        Assert.Equal(Source, paths.DatabasePath);
        Assert.Equal(Install, paths.DataDirectory);
        Assert.Null(paths.LegacyDatabasePath);
    }

    [Fact]
    public void AbsoluteCustomDatabaseAndCommandLineOverrideNeverTriggerMigration()
    {
        var custom = Path.Combine(_root, "custom");
        var paths = AppStoragePaths.Resolve(new() { DatabaseDirectory = custom }, Install, Local, []);
        Assert.Equal(Path.Combine(custom, "timetracker.db"), paths.DatabasePath);
        Assert.Null(paths.LegacyDatabasePath);
        paths = AppStoragePaths.Resolve(new(), Install, Local, ["--db-path", "isolated.db"]);
        Assert.Equal(Path.Combine(Install, "isolated.db"), paths.DatabasePath);
        Assert.Null(paths.LegacyDatabasePath);
    }

    [Fact]
    public async Task MigrationIncludesUncheckpointedWalAndPreservesSourceAndPreferences()
    {
        using var source = Open(Source);
        Execute(source, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE Entries(Id INTEGER, Created TEXT); INSERT INTO Entries VALUES(7, '2026-10-14T00:01:02Z'); PRAGMA user_version=3;");
        Assert.True(File.Exists(Source + "-wal"));
        await File.WriteAllTextAsync(Source + ".view.json", "{\"FiltersExpanded\":true}");
        Assert.True(await LegacyDatabaseMigration.MigrateAsync(Source, Destination));
        using var destination = Open(Destination);
        Assert.Equal(7L, Scalar(destination, "SELECT Id FROM Entries;"));
        Assert.Equal("2026-10-14T00:01:02Z", Scalar(destination, "SELECT Created FROM Entries;"));
        Assert.Equal(3L, Scalar(destination, "PRAGMA user_version;"));
        Assert.Equal("ok", Scalar(destination, "PRAGMA integrity_check;"));
        Assert.Equal(await File.ReadAllTextAsync(Source + ".view.json"), await File.ReadAllTextAsync(Destination + ".view.json"));
        Assert.Equal(7L, Scalar(source, "SELECT Id FROM Entries;"));
        Assert.True(File.Exists(Source));
        Assert.False(await LegacyDatabaseMigration.MigrateAsync(Source, Destination));
    }

    [Fact]
    public async Task ExistingDestinationIsNeverReplaced()
    {
        using (var source = Open(Source)) Execute(source, "CREATE TABLE Entries(Id INTEGER); INSERT INTO Entries VALUES(1);");
        using (var destination = Open(Destination)) Execute(destination, "CREATE TABLE Entries(Id INTEGER); INSERT INTO Entries VALUES(2);");
        Assert.False(await LegacyDatabaseMigration.MigrateAsync(Source, Destination));
        using var check = Open(Destination);
        Assert.Equal(2L, Scalar(check, "SELECT Id FROM Entries;"));
    }

    [Fact]
    public async Task MissingSourceStartsFreshWithoutCreatingOrCopyingFiles()
    {
        Assert.False(await LegacyDatabaseMigration.MigrateAsync(Source, Destination));
        Assert.False(File.Exists(Source));
        Assert.False(File.Exists(Destination));
    }

    [Fact]
    public async Task CorruptSourceFailsWithoutPublishingEmptyDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Source)!);
        await File.WriteAllTextAsync(Source, "This is not a database");
        await Assert.ThrowsAsync<SqliteException>(() => LegacyDatabaseMigration.MigrateAsync(Source, Destination));
        Assert.False(File.Exists(Destination));
        Assert.Equal("This is not a database", await File.ReadAllTextAsync(Source));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Destination)!, "*.migration"));
    }

    [Fact]
    public async Task ConcurrentCopiesMigrateOnlyOnce()
    {
        using (var source = Open(Source)) Execute(source, "CREATE TABLE Entries(Id INTEGER); INSERT INTO Entries VALUES(5);");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => LegacyDatabaseMigration.MigrateAsync(Source, Destination)));
        Assert.Single(results.Where(result => result));
        using var check = Open(Destination);
        Assert.Equal(5L, Scalar(check, "SELECT Id FROM Entries;"));
    }

    [Fact]
    public async Task ExistingListPreferencesArePreserved()
    {
        using (var source = Open(Source)) Execute(source, "CREATE TABLE Entries(Id INTEGER);");
        Directory.CreateDirectory(Path.GetDirectoryName(Destination)!);
        await File.WriteAllTextAsync(Source + ".view.json", "old preferences");
        await File.WriteAllTextAsync(Destination + ".view.json", "current preferences");
        Assert.True(await LegacyDatabaseMigration.MigrateAsync(Source, Destination));
        Assert.Equal("current preferences", await File.ReadAllTextAsync(Destination + ".view.json"));
    }

    [Fact]
    public async Task CancellationDoesNotCreateDestination()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LegacyDatabaseMigration.MigrateAsync(Source, Destination, cancellation.Token));
        Assert.False(File.Exists(Destination));
    }

    private static SqliteConnection Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
