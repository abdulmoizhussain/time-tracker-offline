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

    public DateTime GroupDate => StartUtc?.Date ?? EndUtc?.Date ?? DateTime.UtcNow.Date;

    public string StartLocalText => StartUtc?.ToLocalTime().ToString("g") ?? string.Empty;

    public string EndLocalText => EndUtc?.ToLocalTime().ToString("g") ?? string.Empty;

    public string DurationText
    {
        get
        {
            var duration = TimeSpan.FromSeconds(DurationSeconds);
            return $"{duration.Hours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
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
            IsRunning = entry.IsRunning
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
            IsRunning = IsRunning
        };
    }
}
