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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DescriptionEditPreservesTimingAndRunningState(bool running)
    {
        var id = await _repository.StartTimerAsync("Original", DateTime.UtcNow.AddHours(-2));
        if (!running) await _repository.StopTimerAsync(id, DateTime.UtcNow.AddHours(-1));
        var before = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.True(await _repository.UpdateEntryDescriptionAsync(id, "  Renamed  "));
        var after = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.Equal("Renamed", after.Description);
        Assert.Equal(before.StartUtc, after.StartUtc);
        Assert.Equal(before.EndUtc, after.EndUtc);
        Assert.Equal(before.DurationSeconds, after.DurationSeconds);
        Assert.Equal(before.CreatedUtc, after.CreatedUtc);
        Assert.Equal(before.IsRunning, after.IsRunning);
        Assert.True(after.UpdatedUtc >= before.UpdatedUtc);
    }

    [Fact]
    public async Task DescriptionEditAfterStopDoesNotRestartTaskAndMissingEntryIsReported()
    {
        var id = await _repository.StartTimerAsync("Original", DateTime.UtcNow.AddMinutes(-5));
        await _repository.StopTimerAsync(id, DateTime.UtcNow);
        var stopped = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.True(await _repository.UpdateEntryDescriptionAsync(id, "Renamed after stop"));
        var after = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.False(after.IsRunning);
        Assert.Equal(stopped.EndUtc, after.EndUtc);
        Assert.Equal(stopped.DurationSeconds, after.DurationSeconds);
        await _repository.DeleteEntryAsync(id);
        Assert.False(await _repository.UpdateEntryDescriptionAsync(id, "Deleted"));
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.UpdateEntryDescriptionAsync(id, "   "));
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
    public async Task StopTimerAsync_RepeatedStopDoesNotChangeCompletedEntry()
    {
        var start = DateTime.UtcNow.AddMinutes(-5);
        var id = await _repository.StartTimerAsync("Task", start);
        await _repository.StopTimerAsync(id, start.AddMinutes(5));
        var first = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        await _repository.StopTimerAsync(id, start.AddHours(1));
        var second = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.Equal(first.EndUtc, second.EndUtc);
        Assert.Equal(first.DurationSeconds, second.DurationSeconds);
        Assert.Equal(first.UpdatedUtc, second.UpdatedUtc);
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
    public async Task AddDurationEntryAsync_SelectedDateIsPreservedInLocalGrouping()
    {
        var selectedDate = new DateTime(2024, 1, 15);
        await _repository.AddDurationEntryAsync("Backdated duration", TimeSpan.FromMinutes(45), selectedDate);

        var entry = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.Equal(selectedDate, entry.StartUtc!.Value.ToLocalTime().Date);
        Assert.Equal(2700, entry.DurationSeconds);
        Assert.Null(entry.EndUtc);
        Assert.False(entry.IsRunning);
        Assert.Equal(selectedDate, TimeTracker.App.ViewModels.TimeEntryViewModel.FromModel(entry).GroupDate);
        Assert.True(entry.CreatedUtc > entry.StartUtc.Value);
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
    public async Task UpdateRunningDescriptionAsync_PreservesTimerAndCreationTime()
    {
        var start = DateTime.UtcNow.AddMinutes(-5);
        var id = await _repository.StartTimerAsync("Original", start);
        var original = Assert.Single(await _repository.GetRecentEntriesAsync(10));

        await _repository.UpdateRunningDescriptionAsync(id, "Renamed");

        var renamed = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.Equal("Renamed", renamed.Description);
        Assert.True(renamed.IsRunning);
        Assert.Equal(original.StartUtc, renamed.StartUtc);
        Assert.Equal(original.CreatedUtc, renamed.CreatedUtc);
        Assert.Equal(original.DurationSeconds, renamed.DurationSeconds);
        Assert.Null(renamed.EndUtc);
        Assert.True(renamed.UpdatedUtc >= original.UpdatedUtc);

        await _repository.StopTimerAsync(id, start.AddMinutes(5));
        await _repository.UpdateRunningDescriptionAsync(id, "Late rename");
        var stopped = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.Equal("Renamed", stopped.Description);
        Assert.False(stopped.IsRunning);
        Assert.Equal(300, stopped.DurationSeconds);
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
