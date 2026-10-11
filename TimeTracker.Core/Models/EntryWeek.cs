namespace TimeTracker.Core.Models;

public static class EntryWeek
{
    public static DateTime Start(DateTime localDate)
        => localDate.Date.AddDays(-(((int)localDate.DayOfWeek + 6) % 7));

    public static int Count(DateTime oldestWeek, DateTime newestWeek)
        => (int)((newestWeek - oldestWeek).TotalDays / 7) + 1;
}
