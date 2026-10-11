using System.IO;
using TimeTracker.Core.Models;
using TimeTracker.Data;
using Xunit;

namespace TimeTracker.Tests;

public sealed class TimeEntryRepositoryTests : IDisposable
{
    private readonly string _databasePath;
    private readonly TimeEntryRepository _repository;

    public TimeEntryRepositoryTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"TimeTracker.Tests.{Guid.NewGuid()}.db");
        var factory = new SqliteConnectionFactory(_databasePath);
        _repository = new TimeEntryRepository(factory);
        _repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { File.Delete(_databasePath); } catch { }
    }

    [Fact]
    public async Task InitializeAsync_CreatesSchema()
    {
        var version = await _repository.GetDatabaseVersionAsync();
        Assert.Equal(2, version);
    }

    [Fact]
    public async Task StartTimerAsync_CreatesRunningEntry()
    {
        var startUtc = DateTime.UtcNow;
        var id = await _repository.StartTimerAsync("Test task", startUtc);
        Assert.True(id > 0);
        var entries = await _repository.GetRecentEntriesAsync(10);
        var entry = Assert.Single(entries);
        Assert.Equal("Test task", entry.Description);
        Assert.True(entry.IsRunning);
    }

    [Fact]
    public async Task StopTimerAsync_StopsRunningEntry()
    {
        var startUtc = DateTime.UtcNow;
        var id = await _repository.StartTimerAsync("Test task", startUtc);
        var endUtc = startUtc.AddMinutes(5);
        await _repository.StopTimerAsync(id, endUtc);
        var entries = await _repository.GetRecentEntriesAsync(10);
        var entry = Assert.Single(entries);
        Assert.False(entry.IsRunning);
        Assert.Equal(300, entry.DurationSeconds);
    }

    [Fact]
    public async Task StopTimerAsync_NonExistentId_DoesNotThrow()
    {
        await _repository.StopTimerAsync(int.MaxValue, DateTime.UtcNow);
    }

    [Fact]
    public async Task AddDurationEntryAsync_CreatesEntry()
    {
        var id = await _repository.AddDurationEntryAsync("Manual entry", TimeSpan.FromMinutes(45));
        Assert.True(id > 0);
        var entries = await _repository.GetRecentEntriesAsync(10);
        var entry = Assert.Single(entries);
        Assert.Equal(2700, entry.DurationSeconds);
    }

    [Fact]
    public async Task AddManualRangeEntryAsync_CreatesEntry()
    {
        var startLocal = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Local);
        var endLocal = startLocal.AddHours(2);
        var id = await _repository.AddManualRangeEntryAsync("Range entry", startLocal, endLocal);
        Assert.True(id > 0);
        var entries = await _repository.GetRecentEntriesAsync(10);
        var entry = Assert.Single(entries);
        Assert.Equal(7200, entry.DurationSeconds);
    }

    [Fact]
    public async Task UpdateEntryAsync_UpdatesEntry()
    {
        var id = await _repository.AddDurationEntryAsync("Original", TimeSpan.FromMinutes(10));
        var entries = await _repository.GetRecentEntriesAsync(10);
        var entry = entries[0];
        entry.Description = "Updated";
        entry.DurationSeconds = 600;
        await _repository.UpdateEntryAsync(entry);
        entries = await _repository.GetRecentEntriesAsync(10);
        Assert.Equal("Updated", entries[0].Description);
    }

    [Fact]
    public async Task DeleteEntryAsync_RemovesEntry()
    {
        var id = await _repository.AddDurationEntryAsync("To delete", TimeSpan.FromMinutes(10));
        await _repository.DeleteEntryAsync(id);
        var entries = await _repository.GetRecentEntriesAsync(10);
        Assert.Empty(entries);
    }

    [Fact]
    public async Task ClearAllDataAsync_RemovesAllEntries()
    {
        await _repository.AddDurationEntryAsync("Entry", TimeSpan.FromMinutes(10));
        await _repository.RecordSessionEventAsync("Lock", DateTime.UtcNow);
        await _repository.ClearAllDataAsync();
        Assert.Empty(await _repository.GetRecentEntriesAsync(10));
        Assert.Empty(await _repository.GetRecentSessionEventsAsync(10));
    }

    [Fact]
    public async Task RecordImportExportAsync_CreatesHistoryRecord()
    {
        var record = new ImportExportHistory
        {
            ActionType = "Export",
            ItemCount = 5,
            FirstEntryDescription = "First",
            LastEntryDescription = "Last",
            FilePath = "C:\\data.json",
            Notes = "Test export",
            OccurredUtc = DateTime.UtcNow
        };
        await _repository.RecordImportExportAsync(record);
        var history = await _repository.GetRecentImportExportHistoryAsync(10);
        var entry = Assert.Single(history);
        Assert.Equal("Export", entry.ActionType);
    }
}
