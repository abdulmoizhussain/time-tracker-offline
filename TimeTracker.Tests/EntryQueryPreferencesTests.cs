using System.IO;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;
using Xunit;

namespace TimeTracker.Tests;

public sealed class EntryQueryPreferencesTests
{
    [Fact]
    public async Task LayoutAndManualToggleRoundTripWithBackwardCompatibleDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"TimeTracker.Layout.{Guid.NewGuid()}.json");
        try
        {
            var columns = new[] { new EntryColumnLayout("description", 321), new EntryColumnLayout("timer", 65) };
            await new EntryQueryPreferences { ManualEntryExpanded = false, Columns = columns }.SaveAsync(path);
            var restored = await EntryQueryPreferences.LoadAsync(path);
            Assert.False(restored.ManualEntryExpanded);
            Assert.Equal(columns, restored.Columns);
            await File.WriteAllTextAsync(path, "{\"Applied\":{},\"Draft\":{},\"FiltersExpanded\":true}");
            restored = await EntryQueryPreferences.LoadAsync(path);
            Assert.True(restored.ManualEntryExpanded);
            Assert.True(restored.FiltersExpanded);
            Assert.Null(restored.Columns);
        }
        finally { File.Delete(path); }
    }

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
