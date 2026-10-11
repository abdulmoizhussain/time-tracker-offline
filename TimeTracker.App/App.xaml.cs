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
using TimeTracker.Core.Configuration;
using TimeTracker.Core.Contracts;
using TimeTracker.Data;

namespace TimeTracker.App;

public partial class App : System.Windows.Application
{
    private const string MutexPrefix = "Local\\TimeTracker.SingleInstance.";
    private const string EventPrefix = "Local\\TimeTracker.Activate.";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWaitHandle;
    private IHost? _host;


    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static IServiceProvider Services { get; private set; } = null!;

    private async void OnApplicationStartup(object sender, StartupEventArgs e)
    {

        var userSid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var mutexName = MutexPrefix + userSid;
        var eventName = EventPrefix + userSid;

        _singleInstanceMutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                using var existingEvent = EventWaitHandle.OpenExisting(eventName);
                existingEvent.Set();
            }
            catch
            {
                // ignored
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        _activateWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            OnActivateRequested,
            null,
            Timeout.Infinite,
            false);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var runtimeOptions = new AppRuntimeOptions();
        configuration.Bind(runtimeOptions);

        ApplyUiScaling(runtimeOptions);

        var databaseDirectory = runtimeOptions.Storage.DatabaseDirectory;
        var databaseFileName = runtimeOptions.Storage.DatabaseFileName;

        for (var i = 0; i < e.Args.Length - 1; i++)
        {
            if (e.Args[i] == "--db-path" || e.Args[i] == "-db-path")
            {
                databaseDirectory = e.Args[i + 1];
                databaseFileName = string.Empty;
                break;
            }
        }

        if (!Path.IsPathRooted(databaseDirectory))
        {
            databaseDirectory = Path.Combine(AppContext.BaseDirectory, databaseDirectory);
        }

        var databasePath = string.IsNullOrEmpty(databaseFileName)
            ? databaseDirectory
            : Path.Combine(databaseDirectory, databaseFileName);

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
        mainWindow.Show();
    }

    private void OnActivateRequested(object? state, bool timedOut)
    {
        Dispatcher.Invoke(() =>
        {
            if (MainWindow is { IsVisible: false } window)
            {
                window.Show();
            }

            MainWindow?.Activate();
            MainWindow?.Focus();
        });
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        _activateWaitHandle?.Unregister(null);
        _activateEvent?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }

    private static void LogException(Exception exception, string source)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "TimeTracker.error.log");
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


