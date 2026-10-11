using System.Collections.ObjectModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.Core.Contracts;
using TimeTracker.Core.Models;

namespace TimeTracker.App.ViewModels;

public sealed partial class ScreenEventsViewModel : ObservableObject
{
    private readonly IScreenSessionEventRepository _repository;

    public ScreenEventsViewModel(IScreenSessionEventRepository repository)
    {
        _repository = repository;
        Events = new ObservableCollection<ScreenSessionEventViewModel>();
    }

    [ObservableProperty]
    private string _summaryText = string.Empty;

    public ObservableCollection<ScreenSessionEventViewModel> Events { get; }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var events = await _repository.GetRecentSessionEventsAsync(1000).ConfigureAwait(true);
        Events.Clear();
        foreach (var evt in events.OrderByDescending(e => e.OccurredUtc))
        {
            Events.Add(new ScreenSessionEventViewModel
            {
                Id = evt.Id,
                EventType = evt.EventType,
                OccurredLocal = evt.OccurredUtc.ToLocalTime()
            });
        }

        SummaryText = $"{Events.Count} events";

        var view = CollectionViewSource.GetDefaultView(Events);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ScreenSessionEventViewModel.GroupDate)));
        view.Refresh();
    }
}

public sealed partial class ScreenSessionEventViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _eventType = string.Empty;

    [ObservableProperty]
    private DateTime _occurredLocal;

    public DateTime GroupDate => OccurredLocal.Date;
}
