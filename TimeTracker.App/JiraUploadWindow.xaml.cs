using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using TimeTracker.App.ViewModels;

namespace TimeTracker.App;

public partial class JiraUploadWindow : Window
{
    public JiraUploadWindow(JiraUploadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        Closing += (_, e) =>
        {
            if (viewModel.IsBusy)
            {
                e.Cancel = true;
                viewModel.ErrorText = "Wait for the current operation to finish. During upload you can stop after the current entry.";
            }
        };
        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= OnViewModelChanged;
            viewModel.TokenDraft = string.Empty;
            TokenBox.Clear();
        };
    }

    private JiraUploadViewModel ViewModel => (JiraUploadViewModel)DataContext;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JiraUploadViewModel.TokenDraft) && ViewModel.TokenDraft.Length == 0) TokenBox.Clear();
    }

    private void OnTokenChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is JiraUploadViewModel vm) vm.TokenDraft = TokenBox.Password;
    }

    private void OnDateValidationError(object sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        if (sender == FromPicker) ViewModel.FromDate = null; else ViewModel.ToDate = null;
        ViewModel.ErrorText = "Enter valid From and To dates.";
    }

    private void OnCopyFailuresClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.FailedEntriesText)) return;
        try { Clipboard.SetText(ViewModel.FailedEntriesText); }
        catch (ExternalException) { ViewModel.ErrorText = "Clipboard is busy. Select the failed entries and copy them, or try again."; }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
