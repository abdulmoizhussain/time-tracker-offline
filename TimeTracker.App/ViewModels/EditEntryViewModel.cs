using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;

namespace TimeTracker.App.ViewModels;

public sealed record EditEntryChanges(string Description, DateTime? StartLocal = null,
    DateTime? EndLocal = null, int? DurationSeconds = null);

public sealed partial class EditEntryViewModel : ObservableObject
{
    private readonly TimeEntry _original;
    private readonly DateTime _originalDate;
    private readonly Func<EditEntryChanges, Task> _save;
    private readonly Func<string, Task<IReadOnlyList<string>>>? _searchDescriptions;
    private readonly Func<Task<IReadOnlyList<string>>>? _recentDescriptions;
    private int _suggestionVersion;
    private bool _selectingSuggestion;
    public ObservableCollection<string> Suggestions { get; } = new();
    public Task SuggestionLoading { get; private set; } = Task.CompletedTask;
    public event Action? Saved;
    public bool CanEditTiming => !_original.IsRunning && !IsSaving;
    public string StatusText { get; }

    public EditEntryViewModel(TimeEntry entry, Func<EditEntryChanges, Task> save,
        Func<string, Task<IReadOnlyList<string>>>? searchDescriptions = null,
        Func<Task<IReadOnlyList<string>>>? recentDescriptions = null)
    {
        _original = entry;
        _description = entry.Description;
        _duration = DurationFormatter.Clock(entry.DurationSeconds);
        _originalDate = (entry.StartUtc ?? entry.EndUtc ?? entry.CreatedUtc).ToLocalTime().Date;
        _date = _originalDate;
        _save = save;
        _searchDescriptions = searchDescriptions;
        _recentDescriptions = recentDescriptions;
        StatusText = entry.IsRunning
            ? "This timer keeps running. Stop it before changing Duration or Date."
            : "Keep Start's clock time when the duration fits; otherwise shift Start backward to end at midnight. Maximum 24 hours."
                + (entry.StartUtc is null ? " This entry has no Start time; timing edits use midnight on the selected date." : "");
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _description;

    partial void OnDescriptionChanged(string value)
    {
        if (!_selectingSuggestion) SuggestionLoading = LoadSuggestionsAsync(recent: false);
    }

    [ObservableProperty]
    private bool _isSuggestionsOpen;

    [ObservableProperty]
    private int _suggestionIndex = -1;

    public void CloseSuggestions()
    {
        ++_suggestionVersion;
        IsSuggestionsOpen = false;
        SuggestionIndex = -1;
    }

    public void MoveSuggestion(int direction)
    {
        if (Suggestions.Count == 0) return;
        SuggestionIndex = Math.Clamp(SuggestionIndex + direction, 0, Suggestions.Count - 1);
        IsSuggestionsOpen = true;
    }

    public void AcceptSuggestion()
    {
        if (SuggestionIndex >= 0 && SuggestionIndex < Suggestions.Count)
            SelectSuggestion(Suggestions[SuggestionIndex]);
    }

    public void SelectSuggestion(string description)
    {
        CloseSuggestions();
        _selectingSuggestion = true;
        try { Description = description; }
        finally { _selectingSuggestion = false; }
    }

    public Task ShowRecentSuggestionsAsync() => SuggestionLoading = LoadSuggestionsAsync(recent: true);

    private async Task LoadSuggestionsAsync(bool recent)
    {
        CloseSuggestions();
        var version = _suggestionVersion;
        var search = Description;
        Suggestions.Clear();
        if (IsSaving || (recent ? !string.IsNullOrWhiteSpace(search) : string.IsNullOrWhiteSpace(search))) return;
        try
        {
            var descriptions = recent
                ? _recentDescriptions is null ? Array.Empty<string>() : await _recentDescriptions()
                : _searchDescriptions is null ? Array.Empty<string>() : await _searchDescriptions(search.Trim());
            if (version != _suggestionVersion || search != Description || IsSaving) return;
            foreach (var description in descriptions) Suggestions.Add(description);
            SuggestionIndex = Suggestions.Count > 0 ? 0 : -1;
            IsSuggestionsOpen = Suggestions.Count > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Edit entry description suggestions: {ex}");
        }
    }

    [ObservableProperty]
    private string _duration;

    [ObservableProperty]
    private DateTime? _date;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditTiming))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    [ObservableProperty]
    private string _error = string.Empty;

    public void NormalizeDuration()
    {
        if (!CanEditTiming) return;
        if (DurationParser.TryParse(Duration, out var duration) &&
            ((duration >= TimeSpan.FromSeconds(1) && duration <= TimeSpan.FromHours(24)) ||
                duration.Ticks / TimeSpan.TicksPerSecond == _original.DurationSeconds))
        {
            Duration = DurationFormatter.Clock(duration.Ticks / TimeSpan.TicksPerSecond);
            Error = string.Empty;
        }
        else Error = "Enter a duration from 1 second to 24 hours using Clock pattern or Unit pattern (e.g. 1h 2m 3s).";
    }

    private bool TryChanges(out EditEntryChanges changes)
    {
        changes = new(Description.Trim());
        if (_original.IsRunning) return true;
        if (Date is null)
        {
            Error = "Choose a valid Date.";
            return false;
        }
        if (!DurationParser.TryParse(Duration, out var duration))
        {
            Error = "Enter a valid duration using Clock pattern or Unit pattern (e.g. 1h 2m 3s).";
            return false;
        }
        // Preserve historical large, undated, and cross-day records on description-only saves.
        if (Date.Value.Date == _originalDate && duration.Ticks / TimeSpan.TicksPerSecond == _original.DurationSeconds)
            return true;
        if (!ManualEntryValidation.TryEditedDurationRange(Date.Value,
            _original.StartUtc?.ToLocalTime().TimeOfDay ?? TimeSpan.Zero, duration, out var start, out var end))
        {
            Error = "Timing edits require 1 second to 24 hours on the selected date. Clock-change gaps, repeated hours, or elapsed-time mismatches are not supported.";
            return false;
        }
        changes = new(Description.Trim(), start, end, (int)duration.TotalSeconds);
        return true;
    }

    private bool CanSave() => !IsSaving && !string.IsNullOrWhiteSpace(Description);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave()) return;
        Error = string.Empty;
        if (!TryChanges(out var changes)) return;
        CloseSuggestions();
        IsSaving = true;
        try
        {
            await _save(changes);
            IsSaving = false;
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            Error = $"Could not save this entry: {ex.Message}";
        }
        finally { IsSaving = false; }
    }
}
