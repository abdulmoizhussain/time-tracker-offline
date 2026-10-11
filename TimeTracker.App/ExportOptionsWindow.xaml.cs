using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TimeTracker.App;

public partial class ExportOptionsWindow : Window
{
    private readonly ExportOptionsViewModel _options = new();
    private bool _invalidFrom;
    private bool _invalidTo;

    public ExportOptionsWindow()
    {
        InitializeComponent();
        DataContext = _options;
    }

    public DateTime? FromUtc { get; private set; }
    public DateTime? ToUtcExclusive { get; private set; }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        if (_options.UseDateRange)
        {
            if (string.IsNullOrWhiteSpace(FromPicker.Text)) _invalidFrom = false;
            if (string.IsNullOrWhiteSpace(ToPicker.Text)) _invalidTo = false;
            if (_invalidFrom || _invalidTo)
            {
                _options.ErrorText = "Enter a valid date or clear the field.";
                return;
            }
            if (_options.FromDate.HasValue && _options.ToDate.HasValue && _options.FromDate.Value.Date > _options.ToDate.Value.Date)
            {
                _options.ErrorText = "From date must be on or before To date.";
                return;
            }
            if (_options.ToDate?.Date == DateTime.MaxValue.Date)
            {
                _options.ErrorText = "Choose a To date earlier than 31 December 9999.";
                return;
            }
            FromUtc = _options.FromDate?.Date.ToUniversalTime();
            ToUtcExclusive = _options.ToDate?.Date.AddDays(1).ToUniversalTime();
        }
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void OnFromDateChanged(object? sender, SelectionChangedEventArgs e) => _invalidFrom = false;
    private void OnToDateChanged(object? sender, SelectionChangedEventArgs e) => _invalidTo = false;

    private void OnDateValidationError(object sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        var picker = (DatePicker)sender;
        picker.SelectedDate = null;
        if (picker == FromPicker) _invalidFrom = true; else _invalidTo = true;
        _options.ErrorText = "Enter a valid date or clear the field.";
    }
}

public sealed partial class ExportOptionsViewModel : ObservableObject
{
    [ObservableProperty] private bool _useDateRange;
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _errorText = string.Empty;
}
