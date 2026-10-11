namespace TimeTracker.Core.Models;

public sealed record TimeEntryFilter
{
    public string Description { get; init; } = string.Empty;
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtcExclusive { get; init; }
    public bool? IsRunning { get; init; }
    public bool IsActive => Description.Length > 0 || FromUtc.HasValue || ToUtcExclusive.HasValue || IsRunning.HasValue;
}

public enum TimeEntrySort
{
    Start,
    Description,
    Duration,
    Created,
    Modified
}

public sealed record TimeEntryDateBounds(DateTime? FirstUtc, DateTime? LastUtc, int Count);
