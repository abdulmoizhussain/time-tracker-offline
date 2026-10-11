using System.Globalization;
using System.Windows.Data;
using TimeTracker.App.ViewModels;
using TimeTracker.Core.Models;

namespace TimeTracker.App.Converters;

[ValueConversion(typeof(CollectionViewGroup), typeof(string))]
public sealed class EntryGroupHeaderConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not CollectionViewGroup group)
        {
            return string.Empty;
        }

        var dateLabel = new DateGroupLabelConverter().Convert(group.Name, typeof(string), null!, culture) as string ?? string.Empty;
        var totalSeconds = group.Items.OfType<TimeEntryViewModel>().Where(e => !e.IsRunning).Sum(e => (long)e.DurationSeconds);
        if (parameter is "Total")
        {
            return $"Completed: {DurationFormatter.HoursMinutes(totalSeconds)}";
        }
        return $"{dateLabel}   {DurationFormatter.HoursMinutes(totalSeconds)}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => Convert(values[0], targetType, parameter, culture);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
