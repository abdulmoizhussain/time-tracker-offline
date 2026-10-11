using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeTracker.App.ViewModels;

namespace TimeTracker.App;

public partial class TrayPopupWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isClosing;

    public TrayPopupWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public bool IsClosed { get; private set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        IsClosed = true;
    }

    private void SafeClose()
    {
        if (!_isClosing && !IsClosed)
        {
            _isClosing = true;
            Close();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        SafeClose();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        SafeClose();
    }

    private void OnStartStopClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTimer();
    }

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ShowAndActivate();
        }

        SafeClose();
    }

    private async void OnDescriptionLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            await _viewModel.UpdateRunningDescriptionAsync(textBox.Text).ConfigureAwait(false);
        }
    }

    private async void OnDescriptionKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox textBox)
        {
            await _viewModel.UpdateRunningDescriptionAsync(textBox.Text).ConfigureAwait(false);
        }
    }
}
