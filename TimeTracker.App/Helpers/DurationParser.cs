using System.Globalization;

namespace TimeTracker.App.Helpers;

public static class DurationParser
{
    public static bool TryParse(string? input, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        if (trimmed.Contains('.') || trimmed.Contains(','))
        {
            var normalized = trimmed.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var hours))
            {
                var parsed = TimeSpan.FromHours(hours);
                if (parsed > TimeSpan.Zero)
                {
                    result = parsed;
                    return true;
                }
            }

            return false;
        }

        var parts = trimmed.Split(':');
        if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], out var minutes))
            {
                var parsed = TimeSpan.FromMinutes(minutes);
                if (parsed >= TimeSpan.Zero)
                {
                    result = parsed;
                    return true;
                }
            }

            return false;
        }

        if (parts.Length == 2)
        {
            var minutesParsed = int.TryParse(parts[0], out var minutes) || string.IsNullOrEmpty(parts[0]);
            if (!minutesParsed && !string.IsNullOrEmpty(parts[0]))
            {
                return false;
            }

            var secondsParsed = int.TryParse(parts[1], out var seconds) || string.IsNullOrEmpty(parts[1]);
            if (!secondsParsed && !string.IsNullOrEmpty(parts[1]))
            {
                return false;
            }

            var parsed = new TimeSpan(0, minutes, seconds);
            if (parsed >= TimeSpan.Zero)
            {
                result = parsed;
                return true;
            }

            return false;
        }

        if (parts.Length == 3)
        {
            if (!int.TryParse(parts[0], out var hours))
            {
                return false;
            }

            var minutesParsed = int.TryParse(parts[1], out var minutes) || string.IsNullOrEmpty(parts[1]);
            if (!minutesParsed && !string.IsNullOrEmpty(parts[1]))
            {
                return false;
            }

            var secondsParsed = int.TryParse(parts[2], out var seconds) || string.IsNullOrEmpty(parts[2]);
            if (!secondsParsed && !string.IsNullOrEmpty(parts[2]))
            {
                return false;
            }

            var parsed = new TimeSpan(hours, minutes, seconds);
            if (parsed >= TimeSpan.Zero)
            {
                result = parsed;
                return true;
            }

            return false;
        }

        return false;
    }
}
