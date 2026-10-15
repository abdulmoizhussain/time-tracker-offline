using System.Windows.Controls;
using TimeTracker.App.Helpers;
using Xunit;

namespace TimeTracker.Tests;

public sealed class EntryColumnLayoutTests
{
    [Fact]
    public void LayoutRestoresByStableKeyAndKeepsNewColumns()
        => OnSta(() =>
        {
            var view = CreateView();
            EntryGridLayout.Apply(view, [new("timer", 72), new("id", 48)]);
            Assert.Equal(new[] { "timer", "id", "description" }, view.Columns.Select(EntryGridLayout.GetColumnKey));
            Assert.Equal(72, view.Columns[0].Width);
            Assert.Equal(48, view.Columns[1].Width);
            Assert.Equal(235, view.Columns[2].Width);
            var saved = EntryGridLayout.Capture(view);
            var restarted = CreateView();
            EntryGridLayout.Apply(restarted, saved);
            Assert.Equal(saved, EntryGridLayout.Capture(restarted));
        });

    [Fact]
    public void UnknownDuplicateAndInvalidWidthsDoNotBreakLayout()
        => OnSta(() =>
        {
            var view = CreateView();
            EntryGridLayout.Apply(view, [new("removed", 30), new("description", double.NaN),
                new("description", 500), new("timer", -5), new("id", double.PositiveInfinity)]);
            Assert.Equal(new[] { "description", "timer", "id" }, view.Columns.Select(EntryGridLayout.GetColumnKey));
            Assert.Equal(235, view.Columns[0].Width);
            Assert.Equal(52, view.Columns[1].Width);
            Assert.Equal(42, view.Columns[2].Width);
        });

    private static GridView CreateView()
    {
        var view = new GridView();
        foreach (var (key, width) in new[] { ("id", 42d), ("timer", 52d), ("description", 235d) })
        {
            var column = new GridViewColumn { Header = key, Width = width };
            EntryGridLayout.SetColumnKey(column, key);
            view.Columns.Add(column);
        }
        return view;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
