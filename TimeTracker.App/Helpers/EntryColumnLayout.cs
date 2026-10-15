using System.Windows;
using System.Windows.Controls;

namespace TimeTracker.App.Helpers;

public sealed record EntryColumnLayout(string Key, double Width);

public static class EntryGridLayout
{
    public static readonly DependencyProperty ColumnKeyProperty = DependencyProperty.RegisterAttached(
        "ColumnKey", typeof(string), typeof(EntryGridLayout), new PropertyMetadata(string.Empty));

    public static string GetColumnKey(DependencyObject column) => (string)column.GetValue(ColumnKeyProperty);
    public static void SetColumnKey(DependencyObject column, string value) => column.SetValue(ColumnKeyProperty, value);

    public static EntryColumnLayout[] Capture(GridView view) => view.Columns
        .Select(column => new EntryColumnLayout(GetColumnKey(column), double.IsNaN(column.Width) ? column.ActualWidth : column.Width))
        .Where(column => !string.IsNullOrEmpty(column.Key) && double.IsFinite(column.Width) && column.Width >= 0)
        .ToArray();

    public static void Apply(GridView view, IEnumerable<EntryColumnLayout>? saved)
    {
        if (saved is null) return;
        var remaining = view.Columns.ToDictionary(GetColumnKey);
        var index = 0;
        foreach (var entry in saved)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Key) || !remaining.Remove(entry.Key, out var column)) continue;
            if (double.IsFinite(entry.Width) && entry.Width is >= 0 and <= 10000) column.Width = entry.Width;
            view.Columns.Move(view.Columns.IndexOf(column), index++);
        }
    }
}
