using System.IO;
using System.Text.Json;
using TimeTracker.Core.Models;

namespace TimeTracker.App.Helpers;

public sealed record EntryQuerySelection
{
    public string Description { get; init; } = string.Empty;
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string Status { get; init; } = "All";
    public TimeEntrySort Sort { get; init; } = TimeEntrySort.Start;
    public bool Descending { get; init; } = true;

    public bool IsValid => Description is not null && Status is "All" or "Running" or "Completed"
        && Enum.IsDefined(Sort) && (!FromDate.HasValue || !ToDate.HasValue || FromDate.Value.Date <= ToDate.Value.Date)
        && ToDate?.Date != DateTime.MaxValue.Date;

    public TimeEntryFilter ToFilter() => new()
    {
        Description = Description.Trim(),
        FromUtc = FromDate?.Date.ToUniversalTime(),
        ToUtcExclusive = ToDate?.Date.AddDays(1).ToUniversalTime(),
        IsRunning = Status == "Running" ? true : Status == "Completed" ? false : null
    };
}

public sealed record EntryQueryPreferences
{
    public EntryQuerySelection Applied { get; init; } = new();
    public EntryQuerySelection Draft { get; init; } = new();
    public bool FiltersExpanded { get; init; }
    public bool ManualEntryExpanded { get; init; } = true;
    public EntryColumnLayout[]? Columns { get; init; }

    public static async Task<EntryQueryPreferences> LoadAsync(string path)
    {
        try
        {
            var result = JsonSerializer.Deserialize<EntryQueryPreferences>(await File.ReadAllTextAsync(path));
            return result is { Applied.IsValid: true, Draft: not null } ? result : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public Task SaveAsync(string path) => Task.Run(async () =>
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    });
}
