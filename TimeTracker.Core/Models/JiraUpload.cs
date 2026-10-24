namespace TimeTracker.Core.Models;

public sealed class JiraConnectionSettings
{
    public string Domain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProtectedToken { get; set; } = string.Empty;
}

public sealed class JiraUploadHistory
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtcExclusive { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? FirstUploadedStartUtc { get; set; }
    public DateTime? LastUploadedStartUtc { get; set; }
    public string Status { get; set; } = "In progress";
}
