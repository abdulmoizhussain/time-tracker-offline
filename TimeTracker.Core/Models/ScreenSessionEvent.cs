namespace TimeTracker.Core.Models;

public sealed class ScreenSessionEvent
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
