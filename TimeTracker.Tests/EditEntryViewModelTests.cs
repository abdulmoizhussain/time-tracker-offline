using System.IO;
using TimeTracker.App.ViewModels;
using Xunit;
using TimeTracker.Core.Models;

namespace TimeTracker.Tests;

public sealed class EditEntryViewModelTests
{
    [Fact]
    public async Task SaveIsDisabledForBlankDraftAndCommitsTrimmedText()
    {
        string? saved = null;
        var closed = false;
        var vm = new EditEntryViewModel(Entry("Original", true), edit => { saved = edit.Description; return Task.CompletedTask; });
        vm.Saved += () => closed = true;
        Assert.Equal("Original", vm.Description);
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.Description = " \t\r\n";
        Assert.False(vm.SaveCommand.CanExecute(null));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(saved);
        Assert.False(closed);
        vm.Description = "  Revised task  ";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Revised task", saved);
        Assert.True(closed);
    }

    [Fact]
    public async Task SaveFailureKeepsDraftAndAllowsRetry()
    {
        var vm = new EditEntryViewModel(Entry("Draft", false), _ => throw new IOException("Database unavailable"));
        var closed = false;
        vm.Saved += () => closed = true;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(closed);
        Assert.Contains("Database unavailable", vm.Error);
        Assert.Equal("Draft", vm.Description);
        Assert.False(vm.IsSaving);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveDisablesRepeatedSubmissionWhilePending()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new EditEntryViewModel(Entry("Draft", true), _ => pending.Task);
        var save = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsSaving);
        Assert.False(vm.SaveCommand.CanExecute(null));
        pending.SetResult();
        await save;
        Assert.False(vm.IsSaving);
    }

    private static TimeEntry Entry(string description = "Original", bool running = false) => new()
    {
        Description = description, IsRunning = running, DurationSeconds = 3600,
        StartUtc = new DateTime(2026, 10, 20, 10, 15, 30).ToUniversalTime(),
        EndUtc = running ? null : new DateTime(2026, 10, 20, 11, 15, 30).ToUniversalTime(),
        CreatedUtc = DateTime.UtcNow
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedSuggestionsTrimQueryAndSelectionOnlyFillsDraft(bool running)
    {
        var saved = false;
        string? query = null;
        var vm = new EditEntryViewModel(Entry(running: running), _ => { saved = true; return Task.CompletedTask; },
            text => { query = text; return Task.FromResult<IReadOnlyList<string>>(new[] { "Matched task", "Second match" }); });
        var date = vm.Date;
        var duration = vm.Duration;
        vm.Description = " m ";
        await vm.SuggestionLoading;
        Assert.Equal("m", query);
        Assert.True(vm.IsSuggestionsOpen);
        Assert.Equal(0, vm.SuggestionIndex);
        vm.MoveSuggestion(1);
        vm.AcceptSuggestion();
        Assert.Equal("Second match", vm.Description);
        Assert.False(vm.IsSuggestionsOpen);
        Assert.False(saved);
        Assert.Equal(date, vm.Date);
        Assert.Equal(duration, vm.Duration);
        Assert.Equal("m", query); // Accepting a match must not launch another query.
    }

    [Fact]
    public async Task OlderSearchesAndDismissedResultsCannotReopenSuggestions()
    {
        var old = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var latest = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new EditEntryViewModel(Entry(), _ => Task.CompletedTask,
            text => text == "old" ? old.Task : latest.Task);
        vm.Description = "old";
        var oldLoad = vm.SuggestionLoading;
        vm.Description = "latest";
        var latestLoad = vm.SuggestionLoading;
        latest.SetResult(new[] { "Latest match" });
        await latestLoad;
        old.SetResult(new[] { "Old match" });
        await oldLoad;
        Assert.Equal("Latest match", Assert.Single(vm.Suggestions));

        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm = new EditEntryViewModel(Entry(), _ => Task.CompletedTask, _ => pending.Task);
        vm.Description = "pending";
        var load = vm.SuggestionLoading;
        vm.CloseSuggestions();
        pending.SetResult(new[] { "Late match" });
        await load;
        Assert.False(vm.IsSuggestionsOpen);
        Assert.Empty(vm.Suggestions);
    }

    [Fact]
    public async Task EmptyDownArrowShowsRecentEntriesIncludingDuplicates()
    {
        var vm = new EditEntryViewModel(Entry(), _ => Task.CompletedTask,
            _ => throw new InvalidOperationException("Empty descriptions must not be searched"),
            () => Task.FromResult<IReadOnlyList<string>>(new[] { "Recent", "Recent", "Older" }));
        vm.Description = "";
        await vm.ShowRecentSuggestionsAsync();
        Assert.Equal(3, vm.Suggestions.Count);
        Assert.True(vm.IsSuggestionsOpen);
        vm.AcceptSuggestion();
        Assert.Equal("Recent", vm.Description);
        Assert.False(vm.IsSuggestionsOpen);
    }

    [Fact]
    public async Task FailedSuggestionQueryDoesNotPreventSaving()
    {
        var saved = false;
        var vm = new EditEntryViewModel(Entry(), _ => { saved = true; return Task.CompletedTask; },
            _ => Task.FromException<IReadOnlyList<string>>(new IOException("Unavailable")));
        vm.Description = "Revised task";
        await vm.SuggestionLoading;
        Assert.False(vm.IsSuggestionsOpen);
        Assert.Empty(vm.Error);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(saved);
    }

    [Theory]
    [InlineData("2h", 10, 15, 30, 12, 15, 30)]
    [InlineData("14h", 10, 0, 0, 0, 0, 0)]
    [InlineData("24h", 0, 0, 0, 0, 0, 0)]
    public async Task TimingEditPreservesStartOrShiftsBackwardToMidnight(string input,
        int startHour, int startMinute, int startSecond, int endHour, int endMinute, int endSecond)
    {
        EditEntryChanges? saved = null;
        var vm = new EditEntryViewModel(Entry(), edit => { saved = edit; return Task.CompletedTask; });
        var date = new DateTime(2026, 10, 21);
        vm.Date = date;
        vm.Duration = input;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(saved);
        Assert.Equal(date.Add(new TimeSpan(startHour, startMinute, startSecond)), saved.StartLocal);
        var endDate = endHour == 0 ? date.AddDays(1) : date;
        Assert.Equal(endDate.Add(new TimeSpan(endHour, endMinute, endSecond)), saved.EndLocal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("24:00:01")]
    [InlineData("1m 2")]
    [InlineData("-2s")]
    public async Task InvalidTimingKeepsPopupOpenWithoutSaving(string input)
    {
        var saved = false;
        var vm = new EditEntryViewModel(Entry(), _ => { saved = true; return Task.CompletedTask; });
        vm.Duration = input;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(saved);
        Assert.NotEmpty(vm.Error);
    }

    [Fact]
    public async Task RequiresDateAndAcceptsUnitNormalization()
    {
        var saved = false;
        var vm = new EditEntryViewModel(Entry(), _ => { saved = true; return Task.CompletedTask; });
        vm.Date = null;
        vm.Duration = "90m";
        vm.NormalizeDuration();
        Assert.Equal("01:30:00", vm.Duration);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(saved);
        Assert.Contains("Date", vm.Error);
    }

    [Fact]
    public async Task UnchangedLegacyTimingAndRunningTimingAreNeverRewritten()
    {
        foreach (var running in new[] { false, true })
        {
            var entry = Entry(running: running);
            entry.DurationSeconds = 1199998800;
            entry.StartUtc = entry.EndUtc = null;
            EditEntryChanges? saved = null;
            var vm = new EditEntryViewModel(entry, edit => { saved = edit; return Task.CompletedTask; });
            Assert.Equal(!running, vm.CanEditTiming);
            vm.Description = "Updated";
            if (running) { vm.Duration = "invalid"; vm.Date = null; }
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.NotNull(saved);
            Assert.Null(saved.DurationSeconds);
            Assert.Null(saved.StartLocal);
            Assert.Null(saved.EndLocal);
        }
    }

    [Fact]
    public async Task DateOnlyEditKeepsClockTimeAndDuration()
    {
        EditEntryChanges? saved = null;
        var vm = new EditEntryViewModel(Entry(), edit => { saved = edit; return Task.CompletedTask; });
        vm.Date = new DateTime(2026, 10, 22);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(new DateTime(2026, 10, 22, 10, 15, 30), saved!.StartLocal);
        Assert.Equal(new DateTime(2026, 10, 22, 11, 15, 30), saved.EndLocal);
        Assert.Equal(3600, saved.DurationSeconds);
    }
}
