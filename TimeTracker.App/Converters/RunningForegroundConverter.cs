using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TimeTracker.App.Converters;

[ValueConversion(typeof(bool), typeof(System.Windows.Media.Brush))]
public sealed class RunningForegroundConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush RunningBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 30, 30));
    private static readonly System.Windows.Media.Brush IdleBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 120, 30));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? RunningBrush : IdleBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
