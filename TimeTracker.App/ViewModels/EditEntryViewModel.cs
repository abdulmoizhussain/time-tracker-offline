using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TimeTracker.App.ViewModels;

public sealed partial class EditEntryViewModel : ObservableObject
{
    private readonly Func<string, Task> _save;
    public event Action? Saved;
    public string StatusText { get; }

    public EditEntryViewModel(string description, bool isRunning, Func<string, Task> save)
    {
        _description = description;
        _save = save;
        StatusText = isRunning ? "This timer keeps running while you edit its description." : "Only the description changes; dates and duration stay the same.";
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _description;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isSaving;

    [ObservableProperty]
    private string _error = string.Empty;

    private bool CanSave() => !IsSaving && !string.IsNullOrWhiteSpace(Description);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave()) return;
        IsSaving = true;
        Error = string.Empty;
        try
        {
            await _save(Description.Trim());
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
