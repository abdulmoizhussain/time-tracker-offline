using System.Globalization;
using System.Windows.Data;

namespace TimeTracker.App.Converters;

[ValueConversion(typeof(DateTime), typeof(string))]
public sealed class DateGroupLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime date)
        {
            return string.Empty;
        }

        var today = DateTime.Today;
        var dateOnly = date.Date;

        if (dateOnly == today)
        {
            return "Today";
        }

        if (dateOnly == today.AddDays(-1))
        {
            return "Yesterday";
        }

        var diff = (today - dateOnly).TotalDays;
        if (diff is >= 2 and < 7)
        {
            return dateOnly.ToString("dddd", culture);
        }

        return dateOnly.ToString("ddd, MMM d, yyyy", culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
