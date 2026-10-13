using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TimeTracker.App.ViewModels;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Configuration;
using TimeTracker.Core.Contracts;
using TimeTracker.Data;
using SystemColors = System.Windows.SystemColors;

namespace TimeTracker.App;

public partial class App : System.Windows.Application
{
    private AppInstanceGuard? _instanceGuard;
    private RegisteredWaitHandle? _directoryActivateWait;
    private RegisteredWaitHandle? _anyActivateWait;
    private bool _activationRequested;
    private IHost? _host;
    private static string _dataDirectory = AppStoragePaths.DataRoot(new StorageOptions());


    public App()
    {
        SystemParameters.StaticPropertyChanged += OnSystemAppearanceChanged;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static IServiceProvider Services { get; private set; } = null!;

    private void OnSystemAppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast))
        {
            Dispatcher.Invoke(ApplyHighContrastColors);
        }
    }

    internal void ApplyHighContrastColors()
    {
        // Override the semantic palette, leaving native controls to Windows' own theme.
        var colors = new Dictionary<string, System.Windows.Media.Brush>
        {
            ["CanvasBrush"] = SystemColors.WindowBrush,
            ["SurfaceBrush"] = SystemColors.WindowBrush,
            ["TextBrush"] = SystemColors.WindowTextBrush,
            ["MutedBrush"] = SystemColors.WindowTextBrush,
            ["LineBrush"] = SystemColors.WindowTextBrush,
            ["HeaderSeparatorBrush"] = SystemColors.WindowTextBrush,
            ["AccentBrush"] = SystemColors.HighlightBrush,
            ["AccentTextBrush"] = SystemColors.HighlightTextBrush,
            ["HoverBrush"] = SystemColors.ControlBrush,
            ["SelectedBrush"] = SystemColors.HighlightBrush,
            ["SelectedTextBrush"] = SystemColors.HighlightTextBrush,
            ["RunningBrush"] = SystemColors.WindowBrush,
            ["RunningTextBrush"] = SystemColors.WindowTextBrush,
            ["RunningBorderBrush"] = SystemColors.WindowTextBrush,
            ["DangerBrush"] = SystemColors.HighlightBrush
        };
        foreach (var (key, brush) in colors)
        {
            if (SystemParameters.HighContrast)
            {
                Resources[key] = brush;
            }
            else
            {
                Resources.Remove(key);
            }
        }
    }

    private async void OnApplicationStartup(object sender, StartupEventArgs e)
    {

        var userSid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        AppRuntimeOptions runtimeOptions = new();
        _instanceGuard = await Task.Run(() => AppInstanceGuard.TryAcquire(
            AppContext.BaseDirectory, userSid, () =>
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();
                configuration.Bind(runtimeOptions);
                return runtimeOptions.AllowMultipleInstances;
            }, activateExisting: !WindowsStartupRegistration.IsStartupLaunch(e.Args)));
        if (_instanceGuard is null)
        {
            Shutdown();
            return;
        }
        _directoryActivateWait = ThreadPool.RegisterWaitForSingleObject(
            _instanceGuard.DirectoryActivation, OnActivateRequested, null, Timeout.Infinite, false);
        _anyActivateWait = ThreadPool.RegisterWaitForSingleObject(
            _instanceGuard.AnyActivation, OnActivateRequested, null, Timeout.Infinite, false);

        ApplyUiScaling(runtimeOptions);
        ApplyHighContrastColors();

        var paths = AppStoragePaths.Resolve(runtimeOptions.Storage, AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), e.Args);
        _dataDirectory = paths.DataDirectory;
        var databasePath = paths.DatabasePath;
        try
        {
            if (paths.LegacyDatabasePath is not null)
                await LegacyDatabaseMigration.MigrateAsync(paths.LegacyDatabasePath, databasePath);
        }
        catch (Exception ex)
        {
            LogException(ex, "Database migration");
            System.Windows.MessageBox.Show(
                $"Your existing database could not be copied to the new data folder. TimeTracker will close so it does not start with an empty database. Your original files have been kept.\n\n{ex.Message}",
                "TimeTracker data migration", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices(services =>
            {
                services.AddSingleton(runtimeOptions);
                services.AddSingleton(new SqliteConnectionFactory(databasePath));
                services.AddSingleton<TimeEntryRepository>();
                services.AddSingleton<ITimeEntryRepository>(provider => provider.GetRequiredService<TimeEntryRepository>());
                services.AddSingleton<IScreenSessionEventRepository>(provider => provider.GetRequiredService<TimeEntryRepository>());
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<ScreenEventsViewModel>();
                services.AddSingleton<ImportExportHistoryViewModel>();
                services.AddSingleton<ScreenEventsWindow>(provider => new ScreenEventsWindow
                {
                    DataContext = provider.GetRequiredService<ScreenEventsViewModel>()
                });
                services.AddSingleton<ImportExportHistoryWindow>(provider => new ImportExportHistoryWindow
                {
                    DataContext = provider.GetRequiredService<ImportExportHistoryViewModel>()
                });
                services.AddSingleton<MainWindow>(provider =>
                {
                    var window = new MainWindow(
                        provider.GetRequiredService<ScreenEventsWindow>(),
                        provider.GetRequiredService<ImportExportHistoryWindow>())
                    {
                        DataContext = provider.GetRequiredService<MainViewModel>()
                    };
                    return window;
                });
            })
            .Build();

        Services = _host.Services;

        await _host.StartAsync();

        var repository = _host.Services.GetRequiredService<ITimeEntryRepository>();
        await repository.InitializeAsync();

        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync(databasePath);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.InitializeForLaunch(WindowsStartupRegistration.IsStartupLaunch(e.Args));
        if (_activationRequested) mainWindow.ShowAndActivate();
        await mainWindow.ShowUnfinishedTaskAsync(startup: true);
    }

    private void OnActivateRequested(object? state, bool timedOut)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (Dispatcher.HasShutdownStarted) return;
            _activationRequested = true;
            if (MainWindow is TimeTracker.App.MainWindow mainWindow)
            {
                mainWindow.ShowAndActivate();
                return;
            }
            if (MainWindow is { IsVisible: false } window)
            {
                window.Show();
            }

            MainWindow?.Activate();
            MainWindow?.Focus();
        });
    }

    private async void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        if (MainWindow is not TimeTracker.App.MainWindow mainWindow) return;
        try { await mainWindow.RecordSystemShutdownAsync(); }
        catch (Exception ex) { LogException(ex, "Shutdown timer checkpoint"); }
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemAppearanceChanged;
        _directoryActivateWait?.Unregister(null);
        _anyActivateWait?.Unregister(null);
        _instanceGuard?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }


    }

    private static void LogException(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            var logPath = Path.Combine(_dataDirectory, "TimeTracker.error.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.UtcNow:O}] Unhandled exception from {source}");
            builder.AppendLine(exception.ToString());
            if (exception.InnerException is not null)
            {
                builder.AppendLine("--- Inner Exception ---");
                builder.AppendLine(exception.InnerException.ToString());
            }
            builder.AppendLine();
            File.AppendAllText(logPath, builder.ToString());
        }
        catch
        {
            // ignored
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "Dispatcher");
        e.Handled = true;
        Shutdown();
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex, "AppDomain");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception, "TaskScheduler");
        e.SetObserved();
    }

    private static void ApplyUiScaling(AppRuntimeOptions options)
    {
        var multiplier = options.Ui.ScaleMultiplier;
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
        {
            multiplier = 1.0;
        }

        multiplier = Math.Clamp(multiplier, 0.75, 2.5);
        var baseFontSize = 15 * multiplier;

        Current.Resources["UiScaleMultiplier"] = multiplier;
        Current.Resources["BaseFontSize"] = baseFontSize;
    }

}


