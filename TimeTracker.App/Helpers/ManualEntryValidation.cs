using System.Globalization;

namespace TimeTracker.App.Helpers;

public static class ManualEntryValidation
{
    public static bool TryEditedDurationRange(DateTime date, TimeSpan originalStartTime, TimeSpan duration,
        out DateTime startLocal, out DateTime endLocal)
    {
        startLocal = endLocal = default;
        if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromHours(24) ||
            originalStartTime < TimeSpan.Zero || originalStartTime >= TimeSpan.FromDays(1) ||
            date.Date == DateTime.MaxValue.Date) return false;
        var midnight = date.Date.AddDays(1);
        startLocal = date.Date + originalStartTime;
        if (duration > midnight - startLocal) startLocal = midnight - duration;
        endLocal = startLocal + duration;
        return !TimeZoneInfo.Local.IsInvalidTime(startLocal) && !TimeZoneInfo.Local.IsInvalidTime(endLocal) &&
            !TimeZoneInfo.Local.IsAmbiguousTime(startLocal) && !TimeZoneInfo.Local.IsAmbiguousTime(endLocal) &&
            endLocal.ToUniversalTime() - startLocal.ToUniversalTime() == duration;
    }

    public static bool TryDurationRange(DateTime date, TimeSpan preferredEndTime, TimeSpan duration,
        out DateTime startLocal, out DateTime endLocal)
    {
        startLocal = endLocal = default;
        if (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromHours(24) ||
            preferredEndTime < TimeSpan.Zero || preferredEndTime >= TimeSpan.FromDays(1)) return false;
        endLocal = date.Date + preferredEndTime;
        startLocal = duration > preferredEndTime ? date.Date : endLocal - duration;
        if (duration.Ticks > DateTime.MaxValue.Ticks - startLocal.Ticks) return false;
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
        if (parts.Length is not (2 or 3) || parts.Any(p => p.Length != 2 || p.Any(c => c < '0' || c > '9')))
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
