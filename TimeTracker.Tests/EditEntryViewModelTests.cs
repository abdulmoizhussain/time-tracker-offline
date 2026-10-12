using System.IO;
using TimeTracker.App.ViewModels;
using Xunit;

namespace TimeTracker.Tests;

public sealed class EditEntryViewModelTests
{
    [Fact]
    public async Task SaveIsDisabledForBlankDraftAndCommitsTrimmedText()
    {
        string? saved = null;
        var closed = false;
        var vm = new EditEntryViewModel("Original", true, text => { saved = text; return Task.CompletedTask; });
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
        var vm = new EditEntryViewModel("Draft", false, _ => throw new IOException("Database unavailable"));
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
        var vm = new EditEntryViewModel("Draft", true, _ => pending.Task);
        var save = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsSaving);
        Assert.False(vm.SaveCommand.CanExecute(null));
        pending.SetResult();
        await save;
        Assert.False(vm.IsSaving);
    }
}
