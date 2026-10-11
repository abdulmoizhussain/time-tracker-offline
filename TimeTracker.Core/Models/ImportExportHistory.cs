namespace TimeTracker.Core.Models;

public sealed class ImportExportHistory
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string? FirstEntryDescription { get; set; }
    public DateTime? FirstEntryStartUtc { get; set; }
    public string? LastEntryDescription { get; set; }
    public DateTime? LastEntryStartUtc { get; set; }
    public string? FilePath { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
