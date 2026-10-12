namespace TimeTracker.App.Helpers;

public static class TimerInput
{
    public static bool TryInitialDuration(string? input, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        return string.IsNullOrWhiteSpace(input) ||
            (DurationParser.TryParse(input, out duration) && duration >= TimeSpan.Zero && duration <= new TimeSpan(23, 59, 59));
    }
}
