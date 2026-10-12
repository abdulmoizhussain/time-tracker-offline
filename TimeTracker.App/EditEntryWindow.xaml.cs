using System.ComponentModel;
using System.Windows;
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

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.IsSaving) e.Cancel = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        base.OnClosed(e);
    }
}
