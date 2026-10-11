using CommunityToolkit.Mvvm.ComponentModel;
using TimeTracker.Core.Models;

namespace TimeTracker.App.ViewModels;

public sealed partial class TimeEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime? _startUtc;

    [ObservableProperty]
    private DateTime? _endUtc;

    [ObservableProperty]
    private int _durationSeconds;

    [ObservableProperty]
    private bool _isRunning;

    public DateTime CreatedUtc { get; init; }

    public DateTime UpdatedUtc { get; init; }

    public string CreatedLocalText => CreatedUtc.ToLocalTime().ToString("g");

    public string ModifiedLocalText => UpdatedUtc.ToLocalTime().ToString("g");

    public string CreatedLocalCompactText => CreatedUtc.ToLocalTime().ToString("d MMM yyyy\nHH:mm:ss");
    public string ModifiedLocalCompactText => UpdatedUtc.ToLocalTime().ToString("d MMM yyyy\nHH:mm:ss");
    public string StartLocalCompactText => StartUtc?.ToLocalTime().ToString("d MMM yyyy\nHH:mm:ss") ?? "—";
    public string EndLocalCompactText => EndUtc?.ToLocalTime().ToString("d MMM yyyy\nHH:mm:ss") ?? "—";

    public DateTime GroupDate => (StartUtc ?? EndUtc ?? CreatedUtc).ToLocalTime().Date;

    public string StartLocalText => StartUtc?.ToLocalTime().ToString("g") ?? string.Empty;

    public string EndLocalText => EndUtc?.ToLocalTime().ToString("g") ?? string.Empty;

    public string DurationText
    {
        get
        {
            return DurationFormatter.Clock(DurationSeconds);
        }
    }

    public static TimeEntryViewModel FromModel(TimeEntry entry)
    {
        return new TimeEntryViewModel
        {
            Id = entry.Id,
            Description = entry.Description,
            StartUtc = entry.StartUtc,
            EndUtc = entry.EndUtc,
            DurationSeconds = entry.DurationSeconds,
            IsRunning = entry.IsRunning,
            CreatedUtc = entry.CreatedUtc,
            UpdatedUtc = entry.UpdatedUtc
        };
    }

    public TimeEntry ToModel()
    {
        return new TimeEntry
        {
            Id = Id,
            Description = Description,
            StartUtc = StartUtc,
            EndUtc = EndUtc,
            DurationSeconds = DurationSeconds,
            IsRunning = IsRunning,
            CreatedUtc = CreatedUtc,
            UpdatedUtc = UpdatedUtc
        };
    }
}
