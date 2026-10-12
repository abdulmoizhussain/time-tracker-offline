using TimeTracker.Core.Models;

namespace TimeTracker.App.Helpers;

public static class TimerRecoveryMessage
{
    public static string Format(string description, TimerInterruption interruption)
    {
        if (!interruption.OccurredUtc.HasValue)
            return $"“{description}” was still running when TimeTracker last stopped. No interruption time was recorded.";
        var time = interruption.OccurredUtc.Value.ToLocalTime().ToString("d MMM yyyy HH:mm:ss");
        return interruption.Reason switch
        {
            TimerInterruptionReason.Lock => $"“{description}” was still running when you locked your PC at {time}.",
            TimerInterruptionReason.Exit => $"“{description}” was still running when you exited TimeTracker at {time}.",
            TimerInterruptionReason.Shutdown => $"“{description}” was still running when Windows began shutting down or signing out at {time}.",
            _ => $"“{description}” was still running at the last recorded activity, {time}. The exact interruption time wasn’t recorded."
        };
    }
}
