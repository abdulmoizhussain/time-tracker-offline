using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TimeTracker.App.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var inverted = parameter is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase);
        var flag = value is true;
        if (inverted)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
