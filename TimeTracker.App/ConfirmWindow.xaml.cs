using System.Windows;

namespace TimeTracker.App;

public partial class ConfirmWindow : Window
{
    public string Heading { get; }
    public string Message { get; }
    public string ConfirmText { get; }

    public ConfirmWindow(string heading, string message, string confirmText)
    {
        Heading = heading;
        Message = message;
        ConfirmText = confirmText;
        InitializeComponent();
        DataContext = this;
    }

    public static bool Confirm(string heading, string message, string confirmText)
        => new ConfirmWindow(heading, message, confirmText)
        {
            Owner = System.Windows.Application.Current.MainWindow
        }.ShowDialog() == true;

    private void OnConfirmClick(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
