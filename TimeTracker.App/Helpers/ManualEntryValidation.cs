using System.Globalization;

namespace TimeTracker.App.Helpers;

public static class ManualEntryValidation
{
    public static bool TryDurationRange(DateTime date, TimeSpan preferredStartTime, TimeSpan duration,
        out DateTime startLocal, out DateTime endLocal)
    {
        startLocal = endLocal = default;
        var dayEnd = date.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        if (duration < TimeSpan.FromSeconds(1) || duration > dayEnd - date.Date ||
            preferredStartTime < TimeSpan.Zero || preferredStartTime >= TimeSpan.FromDays(1)) return false;
        startLocal = date.Date + preferredStartTime;
        if (duration > dayEnd - startLocal) startLocal = dayEnd - duration;
        endLocal = startLocal + duration;
        return !TimeZoneInfo.Local.IsInvalidTime(startLocal) && !TimeZoneInfo.Local.IsInvalidTime(endLocal) &&
            !TimeZoneInfo.Local.IsAmbiguousTime(startLocal) && !TimeZoneInfo.Local.IsAmbiguousTime(endLocal) &&
            endLocal.ToUniversalTime() - startLocal.ToUniversalTime() == duration;
    }

    public static bool TryDuration(string? input, out TimeSpan duration)
        => DurationParser.TryParse(input, out duration) && duration >= TimeSpan.FromSeconds(1) && duration <= TimeSpan.FromHours(24);

    public static bool TryTime(string? input, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Trim().Split(':');
        if (parts.Length is not (2 or 3) || parts.Any(p => p.Length is < 1 or > 2 || p.Any(c => c < '0' || c > '9')))
            return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) || hours > 23 ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes > 59)
            return false;
        var seconds = 0;
        if (parts.Length == 3 && (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out seconds) || seconds > 59))
            return false;
        time = new TimeSpan(hours, minutes, seconds);
        return true;
    }
}
