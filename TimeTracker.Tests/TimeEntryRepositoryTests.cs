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
        Assert.Equal(4, version);
    }

    [Fact]
    public async Task VersionTwoMigrationPreservesEntriesAndAddsRecoveryState()
    {
        var id = await _repository.StartTimerAsync("Existing timer", DateTime.UtcNow.AddHours(-1));
        var before = (await _repository.GetRunningEntryAsync())!;
        using (var connection = new SqliteConnectionFactory(_databasePath).CreateConnection())
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE TimerInterruptions; PRAGMA user_version = 2;";
            await command.ExecuteNonQueryAsync();
        }
        await _repository.InitializeAsync();
        await _repository.InitializeAsync();
        Assert.Equal(4, await _repository.GetDatabaseVersionAsync());
        var after = (await _repository.GetRunningEntryAsync())!;
        Assert.Equal(before.StartUtc, after.StartUtc);
        Assert.Equal(before.CreatedUtc, after.CreatedUtc);
        Assert.Equal(before.UpdatedUtc, after.UpdatedUtc);
        var checkpoint = new TimerInterruption(id, TimerInterruptionReason.Exit, DateTime.UtcNow);
        await _repository.SaveTimerInterruptionAsync(checkpoint);
        Assert.Equal(checkpoint, await _repository.GetTimerInterruptionAsync(id));
    }

    [Theory]
    [InlineData(TimerInterruptionReason.Lock)]
    [InlineData(TimerInterruptionReason.Exit)]
    [InlineData(TimerInterruptionReason.Shutdown)]
    [InlineData(TimerInterruptionReason.LastSeen)]
    public async Task InterruptionSurvivesRepositoryRecreationWithoutChangingEntry(TimerInterruptionReason reason)
    {
        var now = DateTime.UtcNow;
        var id = await _repository.StartTimerAsync("Unfinished", now.AddHours(-2));
        var before = Assert.Single(await _repository.GetRecentEntriesAsync(1));
        var checkpoint = new TimerInterruption(id, reason, now);
        await _repository.SaveTimerInterruptionAsync(checkpoint);
        var restored = new TimeEntryRepository(new SqliteConnectionFactory(_databasePath));
        Assert.Equal(checkpoint, await restored.GetTimerInterruptionAsync(id));
        var after = await restored.GetRunningEntryAsync();
        Assert.NotNull(after);
        Assert.Equal(before.CreatedUtc, after.CreatedUtc);
        Assert.Equal(before.UpdatedUtc, after.UpdatedUtc);
        Assert.Equal(before.StartUtc, after.StartUtc);
        await restored.ClearTimerInterruptionAsync(id);
        Assert.Null(await restored.GetTimerInterruptionAsync(id));
        Assert.NotNull(await restored.GetRunningEntryAsync());
    }

    [Fact]
    public async Task ActivityCannotOverwriteLockAndOlderEventsCannotReplaceExit()
    {
        var now = DateTime.UtcNow;
        var id = await _repository.StartTimerAsync("Task", now.AddMinutes(-10));
        var locked = new TimerInterruption(id, TimerInterruptionReason.Lock, now);
        await _repository.SaveTimerInterruptionAsync(locked);
        await _repository.SaveTimerInterruptionAsync(new(id, TimerInterruptionReason.LastSeen, now.AddMinutes(1)));
        Assert.Equal(locked, await _repository.GetTimerInterruptionAsync(id));
        var exited = new TimerInterruption(id, TimerInterruptionReason.Exit, now.AddMinutes(2));
        await _repository.SaveTimerInterruptionAsync(exited);
        await _repository.SaveTimerInterruptionAsync(locked);
        Assert.Equal(exited, await _repository.GetTimerInterruptionAsync(id));
        await _repository.ClearTimerInterruptionAsync(id, locked.OccurredUtc);
        Assert.Equal(exited, await _repository.GetTimerInterruptionAsync(id));
    }

    [Fact]
    public async Task StoppingAtRecordedTimeIncludesInitialDurationAndPreservesCreated()
    {
        var now = DateTime.UtcNow;
        var id = await _repository.StartTimerAsync("Seeded timer", now.AddHours(-2));
        var created = (await _repository.GetRunningEntryAsync())!.CreatedUtc;
        var lockedAt = now.AddMinutes(5);
        await _repository.SaveTimerInterruptionAsync(new(id, TimerInterruptionReason.Lock, lockedAt));
        await _repository.StopTimerAsync(id, lockedAt);
        var stopped = Assert.Single(await _repository.GetRecentEntriesAsync(1));
        Assert.Equal(7500, stopped.DurationSeconds);
        Assert.Equal(lockedAt, stopped.EndUtc);
        Assert.Equal(created, stopped.CreatedUtc);
        Assert.Null(await _repository.GetTimerInterruptionAsync(id));
        await _repository.SaveTimerInterruptionAsync(new(id, TimerInterruptionReason.LastSeen, DateTime.UtcNow));
        Assert.Null(await _repository.GetTimerInterruptionAsync(id));
    }

    [Fact]
    public async Task DeletingEntriesCascadesRecoveryStateAndRecentEntriesUseCreationOrder()
    {
        var id = await _repository.StartTimerAsync("Backdated Start", DateTime.UtcNow.AddDays(-1));
        await _repository.SaveTimerInterruptionAsync(new(id, TimerInterruptionReason.Lock, DateTime.UtcNow));
        await _repository.AddManualRangeEntryAsync("Newer addition", DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-10).AddHours(1));
        Assert.Equal("Newer addition", (await _repository.GetRecentEntriesAsync(5))[0].Description);
        await _repository.DeleteEntryAsync(id);
        await _repository.ClearAllDataAsync();
        Assert.Null(await _repository.GetTimerInterruptionAsync(id));
        Assert.Empty(await _repository.GetRecentEntriesAsync(5));
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

    [Theory]
    [InlineData(-30)]
    [InlineData(0)]
    [InlineData(30)]
    public async Task DurationPlacementKeepsCreatedAtActualAddTime(int selectedDayOffset)
    {
        var selectedDate = DateTime.Today.AddDays(selectedDayOffset);
        Assert.True(TimeTracker.App.Helpers.ManualEntryValidation.TryDurationRange(
            selectedDate, TimeSpan.FromHours(23), TimeSpan.FromHours(2), out var startLocal, out var endLocal));

        var beforeAddUtc = DateTime.UtcNow;
        await _repository.AddManualRangeEntryAsync("Late-day duration", startLocal, endLocal);
        var afterAddUtc = DateTime.UtcNow;

        var entry = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.InRange(entry.CreatedUtc, beforeAddUtc, afterAddUtc);
        Assert.Equal(DateTimeKind.Utc, entry.CreatedUtc.Kind);
        Assert.Equal(entry.CreatedUtc, entry.UpdatedUtc);
        Assert.Equal(selectedDate.AddHours(21), entry.StartUtc!.Value.ToLocalTime());
        Assert.Equal(selectedDate.AddHours(23), entry.EndUtc!.Value.ToLocalTime());
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
    public async Task CompletedTimingUpdatePreservesCreatedAndRejectsRunningOrDeletedEntries()
    {
        var date = DateTime.Today.AddDays(-1);
        var id = await _repository.AddManualRangeEntryAsync("Original", date.AddHours(10), date.AddHours(11));
        var entry = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        var created = entry.CreatedUtc;
        entry.Description = "Edited";
        entry.StartUtc = date.AddHours(22).ToUniversalTime();
        entry.EndUtc = date.AddDays(1).ToUniversalTime();
        entry.DurationSeconds = 7200;
        var before = DateTime.UtcNow;
        Assert.True(await _repository.UpdateCompletedEntryAsync(entry));
        var updated = Assert.Single(await _repository.GetRecentEntriesAsync(10));
        Assert.Equal(created, updated.CreatedUtc);
        Assert.InRange(updated.UpdatedUtc, before, DateTime.UtcNow);
        Assert.Equal(entry.StartUtc, updated.StartUtc);
        Assert.Equal(entry.EndUtc, updated.EndUtc);
        Assert.Equal(7200, updated.DurationSeconds);
        Assert.Equal("Edited", updated.Description);
        await _repository.DeleteEntryAsync(id);
        Assert.False(await _repository.UpdateCompletedEntryAsync(entry));

        var runningId = await _repository.StartTimerAsync("Running", DateTime.UtcNow.AddHours(-1));
        entry.Id = runningId;
        Assert.False(await _repository.UpdateCompletedEntryAsync(entry));
        var running = (await _repository.GetRunningEntryAsync())!;
        Assert.Equal("Running", running.Description);
        Assert.Null(running.EndUtc);
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
