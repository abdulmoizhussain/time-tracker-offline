using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TimeTracker.App.ViewModels;

namespace TimeTracker.App;

public partial class EditEntryWindow : Window
{
    private readonly EditEntryViewModel _viewModel;

    public EditEntryWindow(EditEntryViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => { DescriptionTextBox.Focus(); DescriptionTextBox.SelectAll(); };
        _viewModel.Saved += OnSaved;
    }

    private void OnSaved() => DialogResult = true;
    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnDateValidationError(object sender, System.Windows.Controls.DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        _viewModel.Date = null;
        _viewModel.Error = "Choose a valid Date.";
    }

    private void OnDurationLostFocus(object sender, RoutedEventArgs e) => _viewModel.NormalizeDuration();

    private void OnDescriptionLostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!DescriptionTextBox.IsKeyboardFocusWithin) _viewModel.CloseSuggestions();
        }, DispatcherPriority.Background);
    }

    private void OnDescriptionPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.IsRepeat) { e.Handled = true; return; }
        if (e.Key == Key.Escape && _viewModel.IsSuggestionsOpen)
        {
            _viewModel.CloseSuggestions();
            e.Handled = true;
        }
        else if (_viewModel.IsSuggestionsOpen && e.Key is Key.Up or Key.Down)
        {
            _viewModel.MoveSuggestion(e.Key == Key.Down ? 1 : -1);
            ScrollSelectedSuggestion();
            e.Handled = true;
        }
        else if (_viewModel.IsSuggestionsOpen && e.Key == Key.Enter)
        {
            _viewModel.AcceptSuggestion();
            DescriptionTextBox.CaretIndex = DescriptionTextBox.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down && string.IsNullOrWhiteSpace(DescriptionTextBox.Text)) e.Handled = true;
    }

    private async void OnDescriptionPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || !string.IsNullOrWhiteSpace(DescriptionTextBox.Text) || _viewModel.IsSuggestionsOpen) return;
        e.Handled = true;
        await _viewModel.ShowRecentSuggestionsAsync();
        ScrollSelectedSuggestion();
    }

    private void ScrollSelectedSuggestion()
    {
        if (SuggestionsListBox.SelectedItem is not null) SuggestionsListBox.ScrollIntoView(SuggestionsListBox.SelectedItem);
    }

    private void OnSuggestionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(SuggestionsListBox, source) is not ListBoxItem { DataContext: string description }) return;
        _viewModel.SelectSuggestion(description);
        DescriptionTextBox.Focus();
        DescriptionTextBox.CaretIndex = DescriptionTextBox.Text.Length;
        e.Handled = true;
    }

    private void OnDurationPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        _viewModel.NormalizeDuration();
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsSaving) e.Cancel = true;
        else _viewModel.CloseSuggestions();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        base.OnClosed(e);
    }
}
