using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.Core.Contracts;
using TimeTracker.Core.Models;

namespace TimeTracker.App.ViewModels;

public sealed partial class ImportExportHistoryViewModel : ObservableObject
{
    private readonly ITimeEntryRepository _repository;

    public ImportExportHistoryViewModel(ITimeEntryRepository repository)
    {
        _repository = repository;
        Records = new ObservableCollection<ImportExportHistoryViewModelItem>();
    }

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public ObservableCollection<ImportExportHistoryViewModelItem> Records { get; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var records = await _repository.GetRecentImportExportHistoryAsync(1000).ConfigureAwait(true);
        Records.Clear();
        foreach (var record in records.OrderByDescending(r => r.OccurredUtc))
        {
            Records.Add(new ImportExportHistoryViewModelItem
            {
                Id = record.Id,
                ActionType = record.ActionType,
                ItemCount = record.ItemCount,
                FirstEntryDescription = record.FirstEntryDescription,
                FirstEntryStartLocal = record.FirstEntryStartUtc?.ToLocalTime(),
                LastEntryDescription = record.LastEntryDescription,
                LastEntryStartLocal = record.LastEntryStartUtc?.ToLocalTime(),
                FilePath = record.FilePath,
                Notes = record.Notes,
                OccurredLocal = record.OccurredUtc.ToLocalTime()
            });
        }

        SummaryText = $"{Records.Count} records";

        var view = CollectionViewSource.GetDefaultView(Records);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ImportExportHistoryViewModelItem.GroupDate)));
        view.Refresh();
    }
}

public sealed partial class ImportExportHistoryViewModelItem : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _actionType = string.Empty;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private string? _firstEntryDescription;

    [ObservableProperty]
    private DateTime? _firstEntryStartLocal;

    [ObservableProperty]
    private string? _lastEntryDescription;

    [ObservableProperty]
    private DateTime? _lastEntryStartLocal;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private DateTime _occurredLocal;

    public DateTime GroupDate => OccurredLocal.Date;
}
