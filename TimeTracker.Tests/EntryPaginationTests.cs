using System.IO;
using TimeTracker.Core.Models;
using TimeTracker.Data;
using Xunit;

namespace TimeTracker.Tests;

public sealed class EntryPaginationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"TimeTracker.Pagination.{Guid.NewGuid()}.db");
    private readonly TimeEntryRepository _repository;

    public EntryPaginationTests()
    {
        _repository = new TimeEntryRepository(new SqliteConnectionFactory(_path));
        _repository.InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
        File.Delete(_path + "-wal");
        File.Delete(_path + "-shm");
        File.Delete(_path + ".view.json");
        File.Delete(_path + ".view.json.tmp");
    }

    private Task<IReadOnlyList<TimeEntry>> Week(DateTime monday, TimeEntryFilter? filter = null,
        TimeEntrySort sort = TimeEntrySort.Start, bool descending = true)
        => _repository.GetWeekEntriesAsync(filter ?? new(), monday.ToUniversalTime(), monday.AddDays(7).ToUniversalTime(), sort, descending);

    [Fact]
    public async Task ExportRangeIncludesBothLocalDatesAndLegacyEntriesButExcludesRunning()
    {
        var day = DateTime.Today;
        await _repository.AddDurationEntryAsync("Before", TimeSpan.FromMinutes(1), day.AddDays(-1));
        await _repository.AddDurationEntryAsync("First", TimeSpan.FromMinutes(1), day);
        await _repository.AddDurationEntryAsync("Last", TimeSpan.FromMinutes(1), day.AddDays(1));
        await _repository.AddDurationEntryAsync("After", TimeSpan.FromMinutes(1), day.AddDays(2));
        await _repository.AddDurationEntryAsync("Legacy", TimeSpan.FromMinutes(1));
        await _repository.StartTimerAsync("Running", day.ToUniversalTime());
        var range = await _repository.GetExportEntriesAsync(day.ToUniversalTime(), day.AddDays(2).ToUniversalTime());
        Assert.Equal(new[] { "First", "Last", "Legacy" }, range.Select(e => e.Description).OrderBy(x => x));
        Assert.Equal(5, (await _repository.GetExportEntriesAsync(null, null)).Count);
        Assert.Single(await _repository.GetExportEntriesAsync(null, day.ToUniversalTime()));
        Assert.Empty(await _repository.GetExportEntriesAsync(day.AddDays(3).ToUniversalTime(), null));
    }

    [Fact]
    public async Task ExportIsNotLimitedToTenThousandEntries()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_path}");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH RECURSIVE sequence(n) AS (SELECT 1 UNION ALL SELECT n+1 FROM sequence WHERE n<10005)
            INSERT INTO TimeEntries (Description, StartUtc, EndUtc, DurationSeconds, IsRunning, CreatedUtc, UpdatedUtc)
            SELECT 'Bulk', @Date, @Date, 1, 0, @Date, @Date FROM sequence;
            """;
        command.Parameters.AddWithValue("@Date", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
        Assert.Equal(10005, (await _repository.GetExportEntriesAsync(null, null)).Count);
    }

    [Theory]
    [InlineData(15, 15)] // Monday
    [InlineData(16, 15)]
    [InlineData(21, 15)] // Sunday
    [InlineData(22, 22)]
    public void WeekStartsOnMonday(int day, int monday)
    {
        Assert.Equal(new DateTime(2024, 1, monday), EntryWeek.Start(new DateTime(2024, 1, day, 23, 0, 0)));
        Assert.Equal(3, EntryWeek.Count(new DateTime(2024, 1, 1), new DateTime(2024, 1, 15)));
    }

    [Fact]
    public async Task WeeklyQueryIncludesMondayAndSundayButNotNextMonday()
    {
        var monday = new DateTime(2024, 1, 15);
        await _repository.AddDurationEntryAsync("Monday", TimeSpan.FromMinutes(10), monday);
        await _repository.AddDurationEntryAsync("Sunday", TimeSpan.FromMinutes(20), monday.AddDays(6));
        await _repository.AddDurationEntryAsync("Next Monday", TimeSpan.FromMinutes(30), monday.AddDays(7));
        Assert.Equal(new[] { "Sunday", "Monday" }, (await Week(monday)).Select(e => e.Description));
        Assert.Equal("Next Monday", Assert.Single(await Week(monday.AddDays(7))).Description);
        Assert.Empty(await Week(monday.AddDays(-7)));
    }

    [Fact]
    public async Task FiltersApplyToBothBoundsAndWeekQueriesWithLiteralTextAndExclusiveEnd()
    {
        var monday = new DateTime(2024, 1, 15);
        await _repository.AddDurationEntryAsync("Client 100%_a", TimeSpan.FromMinutes(10), monday);
        await _repository.AddDurationEntryAsync("CLIENT 100%_a", TimeSpan.FromMinutes(20), monday.AddDays(6));
        await _repository.AddDurationEntryAsync("Client 100%_a", TimeSpan.FromMinutes(30), monday.AddDays(7));
        await _repository.AddDurationEntryAsync("Client other", TimeSpan.FromMinutes(40), monday.AddDays(1));
        await _repository.StartTimerAsync("Client 100%_a", monday.AddDays(2).ToUniversalTime());
        var filter = new TimeEntryFilter
        {
            Description = "100%_A",
            FromUtc = monday.ToUniversalTime(),
            ToUtcExclusive = monday.AddDays(7).ToUniversalTime(),
            IsRunning = false
        };
        var entries = await Week(monday, filter);
        Assert.Equal(2, entries.Count);
        var bounds = await _repository.GetEntryDateBoundsAsync(filter);
        Assert.Equal(2, bounds.Count);
        Assert.Equal(monday.ToUniversalTime(), bounds.FirstUtc);
        Assert.Equal(monday.AddDays(6).ToUniversalTime(), bounds.LastUtc);
        Assert.Single(await Week(monday, filter with { IsRunning = true }));
        Assert.Empty(await Week(monday, filter with { Description = "no such task" }));
        Assert.Equal(new TimeEntryDateBounds(null, null, 0), await _repository.GetEntryDateBoundsAsync(filter with { Description = "no such task" }));
    }

    [Theory]
    [InlineData(TimeEntrySort.Start)]
    [InlineData(TimeEntrySort.Description)]
    [InlineData(TimeEntrySort.Duration)]
    [InlineData(TimeEntrySort.Created)]
    [InlineData(TimeEntrySort.Modified)]
    public async Task EverySortSupportsBothDirectionsWithStableTies(TimeEntrySort sort)
    {
        var monday = new DateTime(2024, 1, 15);
        var a = await _repository.AddDurationEntryAsync("Z task", TimeSpan.FromMinutes(10), monday);
        await _repository.AddDurationEntryAsync("A task", TimeSpan.FromMinutes(30), monday.AddDays(1));
        await _repository.AddDurationEntryAsync("A task", TimeSpan.FromMinutes(30), monday.AddDays(1));
        var oldest = (await _repository.GetRecentEntriesAsync(10)).Single(e => e.Id == a);
        await _repository.UpdateEntryAsync(oldest);
        var asc = await Week(monday, sort: sort, descending: false);
        var desc = await Week(monday, sort: sort, descending: true);
        Assert.Equal(asc.Reverse().Select(e => e.Id), desc.Select(e => e.Id));
        Assert.Equal(3, asc.Count);
        if (sort == TimeEntrySort.Description)
        {
            Assert.Equal(new[] { "A task", "A task", "Z task" }, asc.Select(e => e.Description));
        }
        if (sort == TimeEntrySort.Duration)
        {
            Assert.Equal(new[] { 600, 1800, 1800 }, asc.Select(e => e.DurationSeconds));
        }
        if (sort == TimeEntrySort.Modified)
        {
            Assert.Equal(a, desc[0].Id);
        }
    }

    [Fact]
    public async Task PaginationReachesBackdatedEntriesBeyondTheOld500Limit()
    {
        var monday = new DateTime(2024, 1, 15);
        var id = await _repository.AddDurationEntryAsync("Older task", TimeSpan.FromMinutes(5), monday);
        for (var i = 0; i < 501; i++)
        {
            await _repository.AddDurationEntryAsync("Newer task", TimeSpan.FromMinutes(1), monday.AddDays(7));
        }
        Assert.Equal(id, Assert.Single(await Week(monday)).Id);
        Assert.Equal(501, (await Week(monday.AddDays(7))).Count);
        Assert.Equal(502, (await _repository.GetEntryDateBoundsAsync(new())).Count);
    }

    [Fact]
    public async Task LegacyDurationEntriesUseCreationDateAndRunningLookupIgnoresPages()
    {
        var id = await _repository.StartTimerAsync("Still running", DateTime.UtcNow.AddDays(-30));
        await _repository.AddDurationEntryAsync("Legacy duration", TimeSpan.FromMinutes(10));
        Assert.Equal(id, (await _repository.GetRunningEntryAsync())!.Id);
        var entries = await Week(EntryWeek.Start(DateTime.Today));
        Assert.Equal("Legacy duration", Assert.Single(entries).Description);
    }

    [Fact]
    public async Task ViewModelNavigatesEmptyWeeksAndKeepsFiltersAndSortUntilReset()
    {
        var currentMonday = EntryWeek.Start(DateTime.Today);
        await _repository.AddDurationEntryAsync("Current task", TimeSpan.FromMinutes(10), currentMonday);
        await _repository.AddDurationEntryAsync("Older short", TimeSpan.FromMinutes(5), currentMonday.AddDays(-14));
        await _repository.AddDurationEntryAsync("Older long", TimeSpan.FromMinutes(45), currentMonday.AddDays(-14));
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var application = new System.Windows.Application();
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke((Action)(async () =>
            {
                try
                {
                    var vm = new TimeTracker.App.ViewModels.MainViewModel(_repository, _repository,
                        Microsoft.Extensions.Options.Options.Create(new TimeTracker.Core.Configuration.AppRuntimeOptions()));
                    await vm.InitializeAsync(_path);
                    Assert.Single(vm.Entries);
                    Assert.False(vm.HasPendingEntryQuery);
                    Assert.False(vm.HasActiveEntryFilters);
                    vm.ManualDescription = "Duration draft";
                    vm.RangeDescription = "Range draft";
                    vm.SelectedEntry = vm.Entries[0];
                    Assert.Equal("Duration draft", vm.ManualDescription);
                    Assert.Equal("Range draft", vm.RangeDescription);
                    Assert.False(vm.IsEditMode);
                    Assert.False(vm.NewerWeekCommand.CanExecute(null));
                    Assert.True(vm.OlderWeekCommand.CanExecute(null));
                    await vm.OlderWeekCommand.ExecuteAsync(null);
                    Assert.Empty(vm.Entries);
                    Assert.True(vm.ShowEmptyEntries);
                    Assert.Equal("No entries this week", vm.EmptyEntriesText);
                    Assert.Contains("Week 2 of 3", vm.WeekPageText);
                    await vm.OlderWeekCommand.ExecuteAsync(null);
                    Assert.Equal(2, vm.Entries.Count);
                    Assert.False(vm.OlderWeekCommand.CanExecute(null));

                    vm.FilterDescription = "Older";
                    vm.SelectedEntrySort = TimeEntrySort.Duration;
                    vm.SortDescending = false;
                    Assert.True(vm.HasPendingEntryQuery);
                    Assert.Equal(0, vm.AppliedFilterCount);
                    await vm.ApplyEntryFiltersCommand.ExecuteAsync(null);
                    Assert.False(vm.HasPendingEntryQuery);
                    Assert.Equal(1, vm.AppliedFilterCount);
                    Assert.True(vm.HasActiveEntryFilters);
                    Assert.Contains("Filtered completed total", vm.WeekTotalText);
                    Assert.Equal(new[] { "Older short", "Older long" }, vm.Entries.Select(e => e.Description));
                    Assert.Contains("Week 1 of 1", vm.WeekPageText);
                    Assert.Contains("0 h 50 min", vm.EntryPageSummary);
                    Assert.False(vm.NewerWeekCommand.CanExecute(null));
                    Assert.False(vm.OlderWeekCommand.CanExecute(null));

                    vm.FilterFromDate = DateTime.Today;
                    vm.FilterToDate = DateTime.Today.AddDays(-1);
                    await vm.ApplyEntryFiltersCommand.ExecuteAsync(null);
                    Assert.NotEmpty(vm.EntryQueryError);
                    Assert.True(vm.HasPendingEntryQuery);
                    Assert.Equal(1, vm.AppliedFilterCount);
                    Assert.Equal(2, vm.Entries.Count);

                    await vm.SaveEntryQueryPreferencesAsync();
                    var restored = new TimeTracker.App.ViewModels.MainViewModel(_repository, _repository,
                        Microsoft.Extensions.Options.Options.Create(new TimeTracker.Core.Configuration.AppRuntimeOptions()));
                    await restored.InitializeAsync(_path);
                    Assert.Equal("Older", restored.FilterDescription);
                    Assert.Equal(TimeEntrySort.Duration, restored.SelectedEntrySort);
                    Assert.False(restored.SortDescending);
                    Assert.Equal(vm.FilterFromDate, restored.FilterFromDate);
                    Assert.Equal(vm.FilterToDate, restored.FilterToDate);
                    Assert.True(restored.HasPendingEntryQuery);
                    Assert.Equal(new[] { "Older short", "Older long" }, restored.Entries.Select(e => e.Description));
                    await vm.ResetEntryFiltersCommand.ExecuteAsync(null);
                    await restored.InitializeAsync(_path);
                    Assert.False(restored.HasActiveEntryFilters);
                    Assert.Equal("Current task", Assert.Single(vm.Entries).Description);
                    Assert.Equal(string.Empty, vm.FilterDescription);
                    Assert.Null(vm.FilterFromDate);
                    Assert.Null(vm.FilterToDate);
                    Assert.Equal(string.Empty, vm.EntryQueryError);
                    Assert.False(vm.HasPendingEntryQuery);
                    Assert.Equal(0, vm.AppliedFilterCount);

                    var initialCount = (await _repository.GetEntryDateBoundsAsync(new())).Count;
                    vm.ManualDuration = "19999980";
                    await vm.AddDurationEntryCommand.ExecuteAsync(null);
                    Assert.NotEmpty(vm.ManualDurationError);
                    vm.ManualDuration = "30";
                    vm.ManualStartDate = null;
                    await vm.AddDurationEntryCommand.ExecuteAsync(null);
                    Assert.Contains("Start Date", vm.ManualDurationError);
                    vm.ManualStartTime = "09:99";
                    vm.ManualEndTime = "10:00";
                    await vm.AddManualRangeEntryCommand.ExecuteAsync(null);
                    Assert.NotEmpty(vm.ManualRangeError);
                    vm.ManualStartTime = "10:00";
                    await vm.AddManualRangeEntryCommand.ExecuteAsync(null);
                    Assert.Contains("after start", vm.ManualRangeError);
                    Assert.Equal(initialCount, (await _repository.GetEntryDateBoundsAsync(new())).Count);

                    await _repository.AddDurationEntryAsync("Large legacy entry", TimeSpan.FromSeconds(1199998800), currentMonday);
                    vm.FilterDescription = "Large legacy";
                    await vm.ApplyEntryFiltersCommand.ExecuteAsync(null);
                    Assert.Equal("333333:00:00", Assert.Single(vm.Entries).DurationText);
                    Assert.Contains("333333 h 0 min", vm.EntryPageSummary);
                    await vm.SaveEntryDescriptionAsync(vm.Entries[0].Id, "Large legacy renamed");
                    Assert.Equal("Large legacy renamed", Assert.Single(vm.Entries).Description);
                    Assert.False(vm.IsEditMode);
                    Assert.Equal(1199998800, Assert.Single(await _repository.GetWeekEntriesAsync(
                        new TimeEntryFilter { Description = "Large legacy" }, currentMonday.ToUniversalTime(), currentMonday.AddDays(7).ToUniversalTime(), TimeEntrySort.Start, true)).DurationSeconds);
                    vm.CancelEditCommand.Execute(null);
                    await vm.ResetEntryFiltersCommand.ExecuteAsync(null);
                    vm.TimerDescription = "Context timer";
                    await vm.StartTimerCommand.ExecuteAsync(null);
                    var runningRow = vm.Entries.Single(e => e.IsRunning);
                    var completedRow = vm.Entries.First(e => !e.IsRunning);
                    vm.ManualDescription = "Keep this draft";
                    vm.SelectedEntry = completedRow;
                    Assert.True(vm.StopEntryCommand.CanExecute(runningRow));
                    Assert.False(vm.StopEntryCommand.CanExecute(completedRow));
                    await vm.SaveEntryDescriptionAsync(runningRow.Id, "Renamed running timer");
                    Assert.True(vm.IsTimerRunning);
                    Assert.Equal("Running: Renamed running timer", vm.RunningStatusText);
                    Assert.Contains("Renamed running timer", vm.WindowTitle);
                    Assert.Equal("Renamed running timer", (await _repository.GetRunningEntryAsync())!.Description);
                    Assert.Equal("Keep this draft", vm.ManualDescription);
                    await vm.SaveEntryDescriptionAsync(completedRow.Id, "Renamed completed task");
                    Assert.Equal("Running: Renamed running timer", vm.RunningStatusText);
                    await vm.StopEntryCommand.ExecuteAsync(runningRow);
                    Assert.False(vm.IsTimerRunning);
                    Assert.Equal("Time Tracker", vm.WindowTitle);
                    Assert.Equal(System.Windows.Shell.TaskbarItemProgressState.None, vm.TaskbarProgressState);
                    Assert.Null(await _repository.GetRunningEntryAsync());
                    Assert.Equal("Keep this draft", vm.ManualDescription);
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                finally
                {
                    application.Shutdown();
                }
            }));
            System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
