using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TimeTracker.Core.Contracts;

namespace TimeTracker.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public string AppVersionText { get; private set; } = "Version: Unknown";
    public string DatabaseVersionText { get; private set; } = "Database version: Unknown";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        AppVersionText = version is not null ? $"Version: {version}" : "Version: Unknown";

        try
        {
            var repository = App.Services.GetRequiredService<ITimeEntryRepository>();
            var dbVersion = await repository.GetDatabaseVersionAsync().ConfigureAwait(true);
            DatabaseVersionText = $"Database version: {dbVersion}";
        }
        catch
        {
            DatabaseVersionText = "Database version: Unknown";
        }

        DataContext = this;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
