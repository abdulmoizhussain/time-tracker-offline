using System.Windows;
using TimeTracker.App.ViewModels;

namespace TimeTracker.App;

public partial class DefaultTimerTextWindow : Window
{
    private readonly MainViewModel _viewModel;

    public DefaultTimerTextWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveDefaultTimerTextCommand.Execute(null);
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
