namespace TimeTracker.Core.Models;

public sealed class TimeEntry
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsRunning { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
