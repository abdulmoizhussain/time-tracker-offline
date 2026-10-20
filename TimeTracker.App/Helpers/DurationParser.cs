using System.Globalization;
using System.Text.RegularExpressions;

namespace TimeTracker.App.Helpers;

public static class DurationParser
{
    public static bool TryParse(string? input, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim();
        if (text.Any(char.IsLetter)) return TryUnits(text, out result);
        if (text.Any(c => (c < '0' || c > '9') && c != ':' && c != '.' && c != ',')) return false;
        decimal seconds;
        try
        {
            if (text.Contains('.') || text.Contains(','))
            {
                var parts = text.Replace(',', '.').Split('.');
                if (parts.Length != 2 || parts.Any(string.IsNullOrEmpty) || text.Contains(':') ||
                    !decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hours))
                    return false;
                seconds = decimal.Truncate(hours * 3600);
            }
            else
            {
                var parts = text.Split(':');
                if (parts.Length == 1)
                {
                    if (!TryNumber(parts[0], out var minutes)) return false;
                    seconds = (decimal)minutes * 60;
                }
                else if (parts.Length == 2)
                {
                    if (parts.All(string.IsNullOrEmpty) ||
                        !TryNumber(parts[0], out var minutes, allowEmpty: true) ||
                        !TryNumber(parts[1], out var componentSeconds, allowEmpty: true) || componentSeconds > 59)
                        return false;
                    seconds = (decimal)minutes * 60 + componentSeconds;
                }
                else if (parts.Length == 3)
                {
                    if (!TryNumber(parts[0], out var hours) || !TryNumber(parts[1], out var minutes) ||
                        !TryNumber(parts[2], out var componentSeconds) || minutes > 59 || componentSeconds > 59)
                        return false;
                    seconds = (decimal)hours * 3600 + minutes * 60 + componentSeconds;
                }
                else return false;
            }
            if (seconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond) return false;
            result = TimeSpan.FromTicks((long)seconds * TimeSpan.TicksPerSecond);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryNumber(string text, out long number, bool allowEmpty = false)
    {
        number = 0;
        return (allowEmpty && text.Length == 0) || long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryUnits(string text, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        // Consume the complete input: no unlabeled numbers, repeated units, or reordered units.
        var match = Regex.Match(text, @"\A(?:(?<h>[0-9]+)h\s*)?(?:(?<m>[0-9]+)m\s*)?(?:(?<s>[0-9]+)s)?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return false;
        var groups = new[] { match.Groups["h"], match.Groups["m"], match.Groups["s"] };
        var count = groups.Count(g => g.Success);
        if (count == 0) return false;
        decimal seconds = 0;
        var multipliers = new[] { 3600, 60, 1 };
        for (var i = 0; i < groups.Length; i++)
        {
            if (!groups[i].Success) continue;
            if (!TryNumber(groups[i].Value, out var value) || (count > 1 && i > 0 && value > 59)) return false;
            seconds += (decimal)value * multipliers[i];
        }
        if (seconds > TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond) return false;
        result = TimeSpan.FromTicks((long)seconds * TimeSpan.TicksPerSecond);
        return true;
    }
}
