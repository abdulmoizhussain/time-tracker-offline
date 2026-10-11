using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Configuration;
using TimeTracker.Core.Contracts;
using TimeTracker.Core.Models;

namespace TimeTracker.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITimeEntryRepository _repository;
    private readonly IScreenSessionEventRepository _sessionEventRepository;
    private readonly AppRuntimeOptions _options;
    private readonly DispatcherTimer _runningTimer;

    private int? _runningEntryId;
    private DateTime _runningStartUtc;
    private TimeEntryFilter _activeFilter = new();
    private TimeEntrySort _activeSort = TimeEntrySort.Start;
    private bool _activeDescending = true;
    private DateTime _weekStart = EntryWeek.Start(DateTime.Today);
    private DateTime _oldestWeek = EntryWeek.Start(DateTime.Today);
    private DateTime _newestWeek = EntryWeek.Start(DateTime.Today);
    private CancellationTokenSource? _entriesLoadCancellation;
    private string? _entryPreferencesPath;
    private readonly SemaphoreSlim _preferencesGate = new(1, 1);

    [ObservableProperty]
    private string _entryPreferencesError = string.Empty;

    [ObservableProperty]
    private bool _isFiltersExpanded;

    [ObservableProperty]
    private string _weekTotalText = "Completed this week: 0 h 0 min";

    [ObservableProperty]
    private string _emptyEntriesText = "No entries this week";

    public bool HasActiveEntryFilters => _activeFilter.IsActive;
    public string FiltersButtonText => $"Filters · {AppliedFilterCount}";
    public int AppliedFilterCount => (string.IsNullOrWhiteSpace(_activeFilter.Description) ? 0 : 1)
        + (_activeFilter.FromUtc.HasValue ? 1 : 0) + (_activeFilter.ToUtcExclusive.HasValue ? 1 : 0)
        + (_activeFilter.IsRunning.HasValue ? 1 : 0);
    public bool ShowEmptyEntries => !IsEntriesLoading && Entries.Count == 0 && string.IsNullOrEmpty(EntryQueryError);
    public bool HasPendingEntryQuery => FilterDescription.Trim() != _activeFilter.Description
        || FilterFromDate?.Date.ToUniversalTime() != _activeFilter.FromUtc
        || FilterToDate?.Date != _activeFilter.ToUtcExclusive?.ToLocalTime().Date.AddDays(-1)
        || (FilterStatus == "Running" ? true : FilterStatus == "Completed" ? false : (bool?)null) != _activeFilter.IsRunning
        || SelectedEntrySort != _activeSort || SortDescending != _activeDescending;

    private void NotifyEntryQueryState()
    {
        OnPropertyChanged(nameof(HasPendingEntryQuery));
        OnPropertyChanged(nameof(HasActiveEntryFilters));
        OnPropertyChanged(nameof(FiltersButtonText));
        OnPropertyChanged(nameof(AppliedFilterCount));
        OnPropertyChanged(nameof(ShowEmptyEntries));
    }

    public IReadOnlyList<string> StatusFilters { get; } = new[] { "All", "Running", "Completed" };
    public IReadOnlyList<TimeEntrySort> EntrySortOptions { get; } = Enum.GetValues<TimeEntrySort>();

    [ObservableProperty]
    private string _filterDescription = string.Empty;

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    [ObservableProperty]
    private string _filterStatus = "All";

    [ObservableProperty]
    private TimeEntrySort _selectedEntrySort = TimeEntrySort.Start;

    [ObservableProperty]
    private bool _sortDescending = true;

    [ObservableProperty]
    private string _weekPageText = string.Empty;

    [ObservableProperty]
    private string _entryPageSummary = string.Empty;

    [ObservableProperty]
    private string _entryQueryError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OlderWeekCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewerWeekCommand))]
    private bool _isEntriesLoading;

    public MainViewModel(ITimeEntryRepository repository, IScreenSessionEventRepository sessionEventRepository, IOptions<AppRuntimeOptions> options)
    {
        _repository = repository;
        _sessionEventRepository = sessionEventRepository;
        _options = options.Value;

        _runningTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, OnRunningTimerTick, System.Windows.Application.Current.Dispatcher)
        {
            IsEnabled = false
        };

        Entries = new ObservableCollection<TimeEntryViewModel>();
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowEmptyEntries));
        TimerSuggestions = new ObservableCollection<string>();
        ManualSuggestions = new ObservableCollection<string>();
        RangeSuggestions = new ObservableCollection<string>();

        LoadDefaultTimerDescription();

        PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(FilterDescription) or nameof(FilterFromDate) or nameof(FilterToDate)
                or nameof(FilterStatus) or nameof(SelectedEntrySort) or nameof(SortDescending))
            {
                OnPropertyChanged(nameof(HasPendingEntryQuery));
            }
            if (e.PropertyName is nameof(IsEntriesLoading) or nameof(EntryQueryError))
            {
                OnPropertyChanged(nameof(ShowEmptyEntries));
            }
            if (e.PropertyName == nameof(TimerDescription))
            {
                _ = UpdateTimerSuggestionsAsync();
            }
            else if (e.PropertyName == nameof(ManualDescription))
            {
                _ = UpdateManualSuggestionsAsync();
            }
            else if (e.PropertyName == nameof(RangeDescription))
            {
                _ = UpdateRangeSuggestionsAsync();
            }
        };
    }

    [ObservableProperty]
    private string _timerDescription = string.Empty;

    [ObservableProperty]
    private string _manualDescription = string.Empty;

    [ObservableProperty]
    private string _rangeDescription = string.Empty;

    [ObservableProperty]
    private DateTime? _rangeStartDate = DateTime.Today;

    [ObservableProperty]
    private string _manualDurationError = string.Empty;

    [ObservableProperty]
    private string _manualRangeError = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _rangeSuggestions;

    [ObservableProperty]
    private bool _isRangeSuggestionsOpen;

    public string RangeButtonText => IsEditMode ? "Save Range" : "+ Add with Range";

    partial void OnIsEditModeChanged(bool value) => OnPropertyChanged(nameof(RangeButtonText));

    [ObservableProperty]
    private string _manualDuration = "00:00:00";

    [ObservableProperty]
    private DateTime? _manualStartDate = DateTime.Today;

    [ObservableProperty]
    private string _manualStartTime = "00:00:00";

    [ObservableProperty]
    private string _manualEndTime = "00:00:00";

    [ObservableProperty]
    private string _runningElapsedText = "00:00:00";

    [ObservableProperty]
    private string _runningStatusText = "No running timer.";

    [ObservableProperty]
    private string _windowTitle = "Time Tracker";

    [ObservableProperty]
    private bool _isTimerRunning;

    [ObservableProperty]
    private System.Windows.Shell.TaskbarItemProgressState _taskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

    [ObservableProperty]
    private double _taskbarProgressValue;

    [ObservableProperty]
    private string _databasePathText = string.Empty;

    [ObservableProperty]
    private string _defaultTimerDescription = "Untitled task";

    [ObservableProperty]
    private TimeEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _addButtonText = "+ Add";

    [ObservableProperty]
    private ObservableCollection<string> _timerSuggestions;

    [ObservableProperty]
    private ObservableCollection<string> _manualSuggestions;

    [ObservableProperty]
    private bool _isTimerSuggestionsOpen;

    [ObservableProperty]
    private bool _isManualSuggestionsOpen;

    public ObservableCollection<TimeEntryViewModel> Entries { get; }

    public async Task InitializeAsync(string databasePath)
    {
        DatabasePathText = databasePath;
        _entryPreferencesPath = databasePath + ".view.json";
        var preferences = await EntryQueryPreferences.LoadAsync(_entryPreferencesPath);
        _activeFilter = preferences.Applied.ToFilter();
        _activeSort = preferences.Applied.Sort;
        _activeDescending = preferences.Applied.Descending;
        var draft = preferences.Draft;
        FilterDescription = draft.Description ?? string.Empty;
        FilterFromDate = draft.FromDate;
        FilterToDate = draft.ToDate;
        FilterStatus = StatusFilters.Contains(draft.Status) ? draft.Status : "All";
        SelectedEntrySort = Enum.IsDefined(draft.Sort) ? draft.Sort : TimeEntrySort.Start;
        SortDescending = draft.Descending;
        IsFiltersExpanded = preferences.FiltersExpanded;
        NotifyEntryQueryState();
        await LoadEntriesAsync(resetPage: true);
        await RestoreRunningTimerAsync();
    }

    public async Task SaveEntryQueryPreferencesAsync()
    {
        if (_entryPreferencesPath is null) return;
        await _preferencesGate.WaitAsync();
        try
        {
            await new EntryQueryPreferences
            {
                Applied = new EntryQuerySelection
                {
                    Description = _activeFilter.Description,
                    FromDate = _activeFilter.FromUtc?.ToLocalTime().Date,
                    ToDate = _activeFilter.ToUtcExclusive?.ToLocalTime().Date.AddDays(-1),
                    Status = _activeFilter.IsRunning == true ? "Running" : _activeFilter.IsRunning == false ? "Completed" : "All",
                    Sort = _activeSort, Descending = _activeDescending
                },
                Draft = new EntryQuerySelection
                {
                    Description = FilterDescription, FromDate = FilterFromDate, ToDate = FilterToDate,
                    Status = FilterStatus, Sort = SelectedEntrySort, Descending = SortDescending
                },
                FiltersExpanded = IsFiltersExpanded
            }.SaveAsync(_entryPreferencesPath);
            EntryPreferencesError = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            EntryPreferencesError = $"Could not save list preferences: {ex.Message}";
        }
        finally { _preferencesGate.Release(); }
    }

    private async Task LoadEntriesAsync(bool resetPage = false)
    {
        _entriesLoadCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _entriesLoadCancellation = cancellation;
        IsEntriesLoading = true;
        EntryQueryError = string.Empty;
        var filter = _activeFilter;
        var sort = _activeSort;
        var descending = _activeDescending;
        var requestedWeek = _weekStart;
        try
        {
            var bounds = await _repository.GetEntryDateBoundsAsync(filter, cancellation.Token);
            var latestDate = bounds.LastUtc?.ToLocalTime().Date ?? DateTime.Today;
            if (!filter.IsActive && latestDate < DateTime.Today)
            {
                latestDate = DateTime.Today;
            }
            var newest = EntryWeek.Start(latestDate);
            var oldest = EntryWeek.Start(bounds.FirstUtc?.ToLocalTime().Date ?? latestDate);
            var week = resetPage ? newest : requestedWeek < oldest ? oldest : requestedWeek > newest ? newest : requestedWeek;
            // Convert each local boundary separately so DST weeks need not be 168 hours in UTC.
            var entries = await _repository.GetWeekEntriesAsync(filter, week.ToUniversalTime(), week.AddDays(7).ToUniversalTime(),
                sort, descending, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            _newestWeek = newest;
            _oldestWeek = oldest;
            _weekStart = week;
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(TimeEntryViewModel.FromModel(entry));
            }
            var totalSeconds = entries.Where(e => !e.IsRunning).Sum(e => (long)e.DurationSeconds);
            WeekTotalText = $"{(filter.IsActive ? "Filtered completed total" : "Completed this week")}: {DurationFormatter.HoursMinutes(totalSeconds)}";
            EmptyEntriesText = filter.IsActive ? "No matching entries in this week" : "No entries this week";
            WeekPageText = $"{week:ddd, dd MMM yyyy} – {week.AddDays(6):ddd, dd MMM yyyy}   ·   Week {EntryWeek.Count(week, newest)} of {EntryWeek.Count(oldest, newest)}";
            EntryPageSummary = $"{entries.Count} entries this week · {bounds.Count} matching entries overall · Week total: {DurationFormatter.HoursMinutes(totalSeconds)} (completed entries)";
            if (entries.Count == 0)
            {
                EntryPageSummary += " · No matching entries in this week.";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer refresh owns the list now.
        }
        catch (Exception ex)
        {
            if (_entriesLoadCancellation == cancellation)
            {
                EntryQueryError = $"Could not load entries: {ex.Message}";
            }
        }
        finally
        {
            if (_entriesLoadCancellation == cancellation)
            {
                _entriesLoadCancellation = null;
                IsEntriesLoading = false;
                OlderWeekCommand.NotifyCanExecuteChanged();
                NewerWeekCommand.NotifyCanExecuteChanged();
            }
            cancellation.Dispose();
        }
    }

    [RelayCommand]
    private async Task ApplyEntryFiltersAsync()
    {
        if (FilterFromDate.HasValue && FilterToDate.HasValue && FilterFromDate.Value.Date > FilterToDate.Value.Date)
        {
            EntryQueryError = "From date must be on or before To date.";
            return;
        }
        _activeFilter = new TimeEntryFilter
        {
            Description = FilterDescription.Trim(),
            FromUtc = FilterFromDate?.Date.ToUniversalTime(),
            ToUtcExclusive = FilterToDate?.Date.AddDays(1).ToUniversalTime(),
            IsRunning = FilterStatus == "Running" ? true : FilterStatus == "Completed" ? false : null
        };
        _activeSort = SelectedEntrySort;
        _activeDescending = SortDescending;
        NotifyEntryQueryState();
        await SaveEntryQueryPreferencesAsync();
        await LoadEntriesAsync(resetPage: true);
    }

    [RelayCommand]
    private async Task ResetEntryFiltersAsync()
    {
        FilterDescription = string.Empty;
        FilterFromDate = null;
        FilterToDate = null;
        FilterStatus = "All";
        SelectedEntrySort = TimeEntrySort.Start;
        SortDescending = true;
        await ApplyEntryFiltersAsync();
    }

    private bool CanNavigateOlder() => !IsEntriesLoading && _weekStart > _oldestWeek;
    private bool CanNavigateNewer() => !IsEntriesLoading && _weekStart < _newestWeek;

    [RelayCommand(CanExecute = nameof(CanNavigateOlder))]
    private async Task OlderWeekAsync()
    {
        _weekStart = _weekStart.AddDays(-7);
        await LoadEntriesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanNavigateNewer))]
    private async Task NewerWeekAsync()
    {
        _weekStart = _weekStart.AddDays(7);
        await LoadEntriesAsync();
    }

    private async Task RestoreRunningTimerAsync()
    {
        var running = await _repository.GetRunningEntryAsync().ConfigureAwait(true);
        if (running is null)
        {
            return;
        }

        _runningEntryId = running.Id;
        _runningStartUtc = running.StartUtc ?? DateTime.UtcNow;
        IsTimerRunning = true;
        RunningStatusText = $"Running: {running.Description}";
        _runningTimer.IsEnabled = true;
        UpdateRunningDisplay();
    }

    private void LoadEntryForEditing(TimeEntryViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        IsEditMode = true;
        AddButtonText = "Save";
        ManualDescription = value.Description;
        RangeDescription = value.Description;
        ManualDuration = value.DurationText;
        ManualStartDate = value.GroupDate;
        RangeStartDate = value.GroupDate;
        ManualStartTime = "00:00:00";
        ManualEndTime = "00:00:00";

        if (value.StartUtc.HasValue)
        {
            var startLocal = value.StartUtc.Value.ToLocalTime();
            ManualStartDate = startLocal.Date;
            RangeStartDate = startLocal.Date;
            ManualStartTime = startLocal.ToString("HH:mm:ss");
        }

        if (value.EndUtc.HasValue)
        {
            var endLocal = value.EndUtc.Value.ToLocalTime();
            ManualEndTime = endLocal.ToString("HH:mm:ss");
        }
    }

    [RelayCommand]
    private async Task StartTimerAsync()
    {
        var description = string.IsNullOrWhiteSpace(TimerDescription) ? DefaultTimerDescription : TimerDescription.Trim();
        var startUtc = DateTime.UtcNow;
        var id = await _repository.StartTimerAsync(description, startUtc).ConfigureAwait(true);

        _runningEntryId = id;
        _runningStartUtc = startUtc;
        IsTimerRunning = true;
        RunningStatusText = $"Running: {description}";
        _runningTimer.IsEnabled = true;
        UpdateRunningDisplay();
        await LoadEntriesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StopTimerAsync()
    {
        if (!_runningEntryId.HasValue)
        {
            return;
        }

        var endUtc = DateTime.UtcNow;
        await _repository.StopTimerAsync(_runningEntryId.Value, endUtc).ConfigureAwait(true);

        _runningEntryId = null;
        IsTimerRunning = false;
        RunningElapsedText = "00:00:00";
        RunningStatusText = "No running timer.";
        _runningTimer.IsEnabled = false;
        TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        TaskbarProgressValue = 0;
        WindowTitle = "Time Tracker";
        await LoadEntriesAsync().ConfigureAwait(true);
    }

    private void OnRunningTimerTick(object? sender, EventArgs e)
    {
        UpdateRunningDisplay();
    }

    private void UpdateRunningDisplay()
    {
        if (!IsTimerRunning)
        {
            return;
        }

        var elapsed = DateTime.UtcNow - _runningStartUtc;
        RunningElapsedText = DurationFormatter.Clock(elapsed.Ticks / TimeSpan.TicksPerSecond);
        var description = RunningStatusText.StartsWith("Running: ", StringComparison.Ordinal)
            ? RunningStatusText.Substring("Running: ".Length)
            : RunningStatusText;
        WindowTitle = $"Running: {RunningElapsedText} – {description}";
        TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
        TaskbarProgressValue = (DateTime.UtcNow.Second % 60) / 60.0;
    }

    public void ToggleTimer()
    {
        if (IsTimerRunning)
        {
            StopTimerCommand.Execute(null);
        }
        else
        {
            StartTimerCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task AddDurationEntryAsync()
    {
        ManualDurationError = string.Empty;
        if (!ManualEntryValidation.TryDuration(ManualDuration, out var duration))
        {
            ManualDurationError = "Enter a duration from 1 second to 24 hours. Whole numbers mean minutes; decimals mean hours. Use minutes:seconds or hours:minutes:seconds (components 00–59).";
            return;
        }
        if (!ManualStartDate.HasValue)
        {
            ManualDurationError = "Choose a Start Date.";
            return;
        }
        if (IsEditMode && SelectedEntry?.IsRunning == true)
        {
            ManualDurationError = "Stop the timer before changing its duration or date.";
            return;
        }

        var description = string.IsNullOrWhiteSpace(ManualDescription) ? DefaultTimerDescription : ManualDescription.Trim();

        if (IsEditMode && SelectedEntry is not null)
        {
            var entry = SelectedEntry.ToModel();
            entry.Description = description;
            entry.DurationSeconds = (int)duration.TotalSeconds;
            var startLocal = ManualStartDate.Value.Date + (entry.StartUtc?.ToLocalTime().TimeOfDay ?? TimeSpan.Zero);
            entry.StartUtc = startLocal.ToUniversalTime();
            if (entry.EndUtc.HasValue)
            {
                if (entry.StartUtc.Value > DateTime.MaxValue.Subtract(duration))
                {
                    ManualDurationError = "The selected date and duration exceed the supported date range.";
                    return;
                }
                entry.EndUtc = entry.StartUtc.Value.Add(duration);
            }
            await _repository.UpdateEntryAsync(entry).ConfigureAwait(true);
            CancelEdit();
        }
        else
        {
            await _repository.AddDurationEntryAsync(description, duration, ManualStartDate.Value).ConfigureAwait(true);
            ManualDescription = string.Empty;
            ManualDuration = "00:00:00";
            ManualStartDate = DateTime.Today;
        }

        await LoadEntriesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task AddManualRangeEntryAsync()
    {
        ManualRangeError = string.Empty;
        if (!ManualEntryValidation.TryTime(ManualStartTime, out var startTime) || !ManualEntryValidation.TryTime(ManualEndTime, out var endTime))
        {
            ManualRangeError = "Use 24-hour H:mm or HH:mm:ss times: hours 00–23, minutes and seconds 00–59. Example: 09:30 or 09:30:00.";
            return;
        }

        if (!RangeStartDate.HasValue)
        {
            ManualRangeError = "Choose a Start Date.";
            return;
        }
        if (IsEditMode && SelectedEntry?.IsRunning == true)
        {
            ManualRangeError = "Stop the timer before changing its times or date.";
            return;
        }

        var startLocal = RangeStartDate.Value.Date + startTime;
        var endLocal = RangeStartDate.Value.Date + endTime;

        if (endLocal <= startLocal)
        {
            ManualRangeError = "End time must be after start time on the same day.";
            return;
        }
        if (TimeZoneInfo.Local.IsInvalidTime(startLocal) || TimeZoneInfo.Local.IsInvalidTime(endLocal) ||
            TimeZoneInfo.Local.IsAmbiguousTime(startLocal) || TimeZoneInfo.Local.IsAmbiguousTime(endLocal))
        {
            ManualRangeError = "These times fall in a clock-change gap or repeated hour. Choose unambiguous local times.";
            return;
        }

        var description = string.IsNullOrWhiteSpace(RangeDescription) ? DefaultTimerDescription : RangeDescription.Trim();

        if (IsEditMode && SelectedEntry is not null)
        {
            var entry = SelectedEntry.ToModel();
            entry.Description = description;
            entry.StartUtc = startLocal.ToUniversalTime();
            entry.EndUtc = endLocal.ToUniversalTime();
            entry.DurationSeconds = (int)(entry.EndUtc.Value - entry.StartUtc.Value).TotalSeconds;
            await _repository.UpdateEntryAsync(entry).ConfigureAwait(true);
            CancelEdit();
        }
        else
        {
            await _repository.AddManualRangeEntryAsync(description, startLocal, endLocal).ConfigureAwait(true);
            RangeDescription = string.Empty;
            RangeStartDate = DateTime.Today;
            ManualStartTime = "00:00:00";
            ManualEndTime = "00:00:00";
        }

        await LoadEntriesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void EditEntry(TimeEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        SelectedEntry = entry;
        LoadEntryForEditing(entry);
    }

    [RelayCommand(CanExecute = nameof(CanStopEntry))]
    private async Task StopEntryAsync(TimeEntryViewModel? entry)
    {
        if (entry is null || !entry.IsRunning)
        {
            return;
        }
        await _repository.StopTimerAsync(entry.Id, DateTime.UtcNow);
        if (_runningEntryId == entry.Id)
        {
            ResetRunningState();
        }
        await LoadEntriesAsync();
    }

    private bool CanStopEntry(TimeEntryViewModel? entry) => entry?.IsRunning == true;

    private void ResetRunningState()
    {
        _runningEntryId = null;
        IsTimerRunning = false;
        _runningTimer.IsEnabled = false;
        RunningStatusText = "No running timer.";
        RunningElapsedText = "00:00:00";
        TaskbarProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
        TaskbarProgressValue = 0;
        WindowTitle = "Time Tracker";
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(TimeEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (!ConfirmWindow.Confirm("Remove entry?", $"Remove '{entry.Description}'? This entry will be deleted from your local database.", "Remove entry"))
        {
            return;
        }

        await _repository.DeleteEntryAsync(entry.Id).ConfigureAwait(true);
        if (_runningEntryId == entry.Id)
        {
            ResetRunningState();
        }

        await LoadEntriesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditMode = false;
        AddButtonText = "+ Add";
        SelectedEntry = null;
        ClearManualFields();
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import Time Entries"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(true);
            var data = JsonSerializer.Deserialize<TimeEntryExportDto>(json);
            if (data?.TimeEntries is null || data.TimeEntries.Count == 0)
            {
                MessageBox.Show("No entries found in the selected file.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var count = 0;
            TimeEntryExportItemDto? first = null;
            TimeEntryExportItemDto? last = null;

            foreach (var item in data.TimeEntries)
            {
                if (DateTime.TryParseExact(item.StartDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _) &&
                    TimeSpan.TryParse(item.StartTime, out _) &&
                    TryParseImportedDuration(item.Duration, out var duration))
                {
                    var description = string.IsNullOrWhiteSpace(item.Description) ? DefaultTimerDescription : item.Description;
                    await _repository.AddDurationEntryAsync(description, duration).ConfigureAwait(true);

                    first ??= item;
                    last = item;
                    count++;
                }
            }

            await _repository.RecordImportExportAsync(new ImportExportHistory
            {
                ActionType = "Import",
                ItemCount = count,
                FirstEntryDescription = first?.Description,
                FirstEntryStartUtc = first is not null && DateTime.TryParse(first.StartDate, out var firstDate) ? firstDate.ToUniversalTime() : null,
                LastEntryDescription = last?.Description,
                LastEntryStartUtc = last is not null && DateTime.TryParse(last.StartDate, out var lastDate) ? lastDate.ToUniversalTime() : null,
                FilePath = dialog.FileName,
                OccurredUtc = DateTime.UtcNow
            }).ConfigureAwait(true);

            await LoadEntriesAsync().ConfigureAwait(true);
            MessageBox.Show($"Imported {count} entries. Skipped {data.TimeEntries.Count - count} invalid or unrepresentable entries.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var range = new ExportOptionsWindow { Owner = System.Windows.Application.Current.MainWindow };
        if (range.ShowDialog() != true) return;

        IReadOnlyList<TimeEntry> entries;
        try
        {
            entries = await _repository.GetExportEntriesAsync(range.FromUtc, range.ToUtcExclusive);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load entries for export: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (entries.Count == 0)
        {
            MessageBox.Show("No completed entries in the selected date range.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Export Time Entries",
            FileName = "timetracker-export.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var items = entries
                .Where(e => !e.IsRunning)
                .Select(e =>
                {
                    var startLocal = e.StartUtc?.ToLocalTime() ?? e.CreatedUtc.ToLocalTime();
                    var endLocal = e.EndUtc?.ToLocalTime() ?? startLocal;
                    return new TimeEntryExportItemDto
                    {
                        Description = e.Description,
                        StartDate = startLocal.ToString("yyyy-MM-dd"),
                        StartTime = startLocal.ToString("HH:mm:ss"),
                        EndDate = endLocal.ToString("yyyy-MM-dd"),
                        EndTime = endLocal.ToString("HH:mm:ss"),
                        Duration = DurationFormatter.Clock(e.DurationSeconds)
                    };
                })
                .OrderByDescending(e => e.StartDate)
                .ThenByDescending(e => e.StartTime)
                .ToList();

            var data = new TimeEntryExportDto { TimeEntries = items };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(dialog.FileName, json).ConfigureAwait(true);

            await _repository.RecordImportExportAsync(new ImportExportHistory
            {
                ActionType = "Export",
                ItemCount = items.Count,
                FirstEntryDescription = items.LastOrDefault()?.Description,
                FirstEntryStartUtc = items.LastOrDefault() is { } first && DateTime.TryParse(first.StartDate, out var firstDate) ? firstDate.ToUniversalTime() : null,
                LastEntryDescription = items.FirstOrDefault()?.Description,
                LastEntryStartUtc = items.FirstOrDefault() is { } last && DateTime.TryParse(last.StartDate, out var lastDate) ? lastDate.ToUniversalTime() : null,
                FilePath = dialog.FileName,
                Notes = range.FromUtc.HasValue || range.ToUtcExclusive.HasValue
                    ? $"Local date range: {range.FromUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "any"} to {range.ToUtcExclusive?.ToLocalTime().AddDays(-1).ToString("yyyy-MM-dd") ?? "any"} (inclusive)."
                    : "All completed entries.",
                OccurredUtc = DateTime.UtcNow
            }).ConfigureAwait(true);

            MessageBox.Show($"Exported {items.Count} entries.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        if (!ConfirmWindow.Confirm("Clear all data?", "All time entries will be removed. This cannot be undone.", "Clear all data"))
        {
            return;
        }

        await _repository.ClearAllDataAsync().ConfigureAwait(true);
        _runningEntryId = null;
        IsTimerRunning = false;
        _runningTimer.IsEnabled = false;
        RunningStatusText = "No running timer.";
        RunningElapsedText = "00:00:00";
        await LoadEntriesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void SaveDefaultTimerText()
    {
        SaveDefaultTimerDescription();
    }

    public async Task RecordSessionEventAsync(string eventType)
    {
        if (!_options.SessionEvents.Enabled)
        {
            return;
        }

        await _sessionEventRepository.RecordSessionEventAsync(eventType, DateTime.UtcNow).ConfigureAwait(true);
    }

    private async Task UpdateTimerSuggestionsAsync()
    {
        var search = TimerDescription;
        TimerSuggestions.Clear();
        IsTimerSuggestionsOpen = false;

        if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
        {
            return;
        }

        var suggestions = await _repository.SearchDescriptionsAsync(search, 8).ConfigureAwait(true);
        foreach (var suggestion in suggestions)
        {
            TimerSuggestions.Add(suggestion);
        }

        IsTimerSuggestionsOpen = TimerSuggestions.Count > 0;
    }

    private async Task UpdateManualSuggestionsAsync()
    {
        var search = ManualDescription;
        ManualSuggestions.Clear();
        IsManualSuggestionsOpen = false;

        if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
        {
            return;
        }

        var suggestions = await _repository.SearchDescriptionsAsync(search, 8).ConfigureAwait(true);
        foreach (var suggestion in suggestions)
        {
            ManualSuggestions.Add(suggestion);
        }

        IsManualSuggestionsOpen = ManualSuggestions.Count > 0;
    }

    private async Task UpdateRangeSuggestionsAsync()
    {
        var search = RangeDescription;
        RangeSuggestions.Clear();
        IsRangeSuggestionsOpen = false;
        if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
        {
            return;
        }

        var suggestions = await _repository.SearchDescriptionsAsync(search, 8).ConfigureAwait(true);
        if (RangeDescription != search)
        {
            return;
        }
        RangeSuggestions.Clear();
        foreach (var suggestion in suggestions)
        {
            RangeSuggestions.Add(suggestion);
        }
        IsRangeSuggestionsOpen = RangeSuggestions.Count > 0;
    }

    private void LoadDefaultTimerDescription()
    {
        try
        {
            var path = GetUserSettingsPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<UserSettingsDto>(json);
                if (!string.IsNullOrWhiteSpace(settings?.DefaultTimerDescription))
                {
                    DefaultTimerDescription = settings.DefaultTimerDescription;
                    return;
                }
            }
        }
        catch
        {
            // ignored
        }

        DefaultTimerDescription = _options.Timer.DefaultDescription;
    }

    private void SaveDefaultTimerDescription()
    {
        try
        {
            var path = GetUserSettingsPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new UserSettingsDto { DefaultTimerDescription = DefaultTimerDescription };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch
        {
            // ignored
        }
    }

    private static string GetUserSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TimeTracker", "user-settings.json");
    }

    private void ClearManualFields()
    {
        ManualDescription = string.Empty;
        ManualDuration = "00:00:00";
        ManualStartDate = DateTime.Today;
        RangeDescription = string.Empty;
        RangeStartDate = DateTime.Today;
        ManualStartTime = "00:00:00";
        ManualEndTime = "00:00:00";
        ManualDurationError = string.Empty;
        ManualRangeError = string.Empty;
    }

    private static bool TryParseImportedDuration(string input, out TimeSpan duration)
    {
        // Accept full-hour exports and legacy TimeSpan day notation; no manual 24-hour cap.
        return (DurationParser.TryParse(input, out duration) ||
                TimeSpan.TryParse(input, System.Globalization.CultureInfo.InvariantCulture, out duration)) &&
               duration >= TimeSpan.Zero && duration.TotalSeconds <= int.MaxValue;
    }
}

public sealed class TimeEntryExportDto
{
    public List<TimeEntryExportItemDto> TimeEntries { get; set; } = new();
}

public sealed class TimeEntryExportItemDto
{
    public string Description { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
}

public sealed class UserSettingsDto
{
    public string DefaultTimerDescription { get; set; } = string.Empty;
}

