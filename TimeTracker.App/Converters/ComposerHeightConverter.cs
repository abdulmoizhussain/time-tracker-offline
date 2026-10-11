using System.Globalization;
using System.Windows.Data;

namespace TimeTracker.App.Converters;

// Reserve most of the remaining viewport for browsing; composer can scroll at small sizes.
public sealed class ComposerHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double height && height > 0
            ? parameter is "Headers" ? Math.Min(200, Math.Max(50, height * 0.3 + 8)) : Math.Min(265, Math.Max(80, height - 300))
            : 240d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
