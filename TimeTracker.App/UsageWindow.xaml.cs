using System.Windows;

namespace TimeTracker.App;

public partial class UsageWindow : Window
{
    public UsageWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
