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
    }

    private Task<IReadOnlyList<TimeEntry>> Week(DateTime monday, TimeEntryFilter? filter = null,
        TimeEntrySort sort = TimeEntrySort.Start, bool descending = true)
        => _repository.GetWeekEntriesAsync(filter ?? new(), monday.ToUniversalTime(), monday.AddDays(7).ToUniversalTime(), sort, descending);

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
                    Assert.False(vm.NewerWeekCommand.CanExecute(null));
                    Assert.True(vm.OlderWeekCommand.CanExecute(null));
                    await vm.OlderWeekCommand.ExecuteAsync(null);
                    Assert.Empty(vm.Entries);
                    Assert.Contains("Week 2 of 3", vm.WeekPageText);
                    await vm.OlderWeekCommand.ExecuteAsync(null);
                    Assert.Equal(2, vm.Entries.Count);
                    Assert.False(vm.OlderWeekCommand.CanExecute(null));

                    vm.FilterDescription = "Older";
                    vm.SelectedEntrySort = TimeEntrySort.Duration;
                    vm.SortDescending = false;
                    await vm.ApplyEntryFiltersCommand.ExecuteAsync(null);
                    Assert.Equal(new[] { "Older short", "Older long" }, vm.Entries.Select(e => e.Description));
                    Assert.Contains("Week 1 of 1", vm.WeekPageText);
                    Assert.Contains("0 h 50 min", vm.EntryPageSummary);
                    Assert.False(vm.NewerWeekCommand.CanExecute(null));
                    Assert.False(vm.OlderWeekCommand.CanExecute(null));

                    vm.FilterFromDate = DateTime.Today;
                    vm.FilterToDate = DateTime.Today.AddDays(-1);
                    await vm.ApplyEntryFiltersCommand.ExecuteAsync(null);
                    Assert.NotEmpty(vm.EntryQueryError);
                    Assert.Equal(2, vm.Entries.Count);

                    await vm.ResetEntryFiltersCommand.ExecuteAsync(null);
                    Assert.Equal("Current task", Assert.Single(vm.Entries).Description);
                    Assert.Equal(string.Empty, vm.FilterDescription);
                    Assert.Null(vm.FilterFromDate);
                    Assert.Null(vm.FilterToDate);
                    Assert.Equal(string.Empty, vm.EntryQueryError);
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
