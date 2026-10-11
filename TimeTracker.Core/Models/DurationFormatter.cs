using System.Globalization;

namespace TimeTracker.Core.Models;

public static class DurationFormatter
{
    // Work directly in seconds: totals can exceed TimeSpan's range, and hours must never wrap at 24.
    private static ulong Magnitude(long seconds)
        => seconds < 0 ? (ulong)(-(seconds + 1)) + 1 : (ulong)seconds;

    public static string Clock(long seconds)
    {
        var value = Magnitude(seconds);
        return string.Create(CultureInfo.InvariantCulture,
            $"{(seconds < 0 ? "-" : "")}{value / 3600:D2}:{value / 60 % 60:D2}:{value % 60:D2}");
    }

    public static string HoursMinutes(long seconds)
    {
        var value = Magnitude(seconds);
        return string.Create(CultureInfo.InvariantCulture,
            $"{(seconds < 0 ? "-" : "")}{value / 3600} h {value / 60 % 60} min");
    }
}
