using System.IO;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;
using Xunit;

namespace TimeTracker.Tests;

public sealed class EntryQueryPreferencesTests
{
    [Fact]
    public async Task PreferencesRoundTripAndRecoverFromCorruptOrInvalidAppliedState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"TimeTracker.View.{Guid.NewGuid()}.json");
        try
        {
            var applied = new EntryQuerySelection
            {
                Description = "Client", Status = "Completed", FromDate = new DateTime(2026, 10, 1),
                ToDate = new DateTime(2026, 10, 11), Sort = TimeEntrySort.Modified, Descending = false
            };
            var expected = new EntryQueryPreferences { Applied = applied, Draft = applied with { Description = "Unsaved draft" }, FiltersExpanded = true };
            await expected.SaveAsync(path);
            Assert.Equal(expected, await EntryQueryPreferences.LoadAsync(path));
            Assert.False(File.Exists(path + ".tmp"));
            await File.WriteAllTextAsync(path, "broken json");
            Assert.Equal(new EntryQueryPreferences(), await EntryQueryPreferences.LoadAsync(path));
            await (expected with { Applied = applied with { Sort = (TimeEntrySort)100 } }).SaveAsync(path);
            Assert.Equal(new EntryQueryPreferences(), await EntryQueryPreferences.LoadAsync(path));
        }
        finally { File.Delete(path); File.Delete(path + ".tmp"); }
    }
}
