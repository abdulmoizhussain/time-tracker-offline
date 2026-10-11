using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using Xunit;

namespace TimeTracker.UITests;

public sealed class MainWindowSmokeTests : IDisposable
{
    private readonly string _databasePath;
    private readonly Process _application;
    private AutomationElement? _mainWindow;

    public MainWindowSmokeTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"TimeTracker.UITests.{Guid.NewGuid()}.db");

        var solutionDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var exePath = Path.Combine(
            solutionDirectory,
            "TimeTracker.App",
            "bin",
            "Debug",
            "net8.0-windows",
            "TimeTracker.exe");

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Could not find TimeTracker.exe at {exePath}");
        }

        _application = new Process
        {
            StartInfo = new ProcessStartInfo(exePath, $"--db-path \"{_databasePath}\"")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath)!
            }
        };

        _application.Start();
        _mainWindow = WaitForMainWindow();
    }

    public void Dispose()
    {
        try
        {
            if (!_application.HasExited)
            {
                _application.CloseMainWindow();
                if (!_application.WaitForExit(2000))
                {
                    _application.Kill();
                }
            }
        }
        catch
        {
            try { _application.Kill(); } catch { }
        }
        finally
        {
            _application.Dispose();
        }

        try { File.Delete(_databasePath); } catch { }
        try { File.Delete($"{_databasePath}-shm"); } catch { }
        try { File.Delete($"{_databasePath}-wal"); } catch { }
    }

    private static AutomationElement? FindWindow(string titleSubstring, int timeoutMs = 10000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end)
        {
            var desktop = AutomationElement.RootElement;
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
            var windows = desktop.FindAll(TreeScope.Children, condition);
            for (var i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                var name = window.Current.Name;
                if (name.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                {
                    return window;
                }
            }

            Thread.Sleep(250);
        }

        return null;
    }

    private AutomationElement WaitForMainWindow()
    {
        var window = FindWindow("Time Tracker", timeoutMs: 15000);
        Assert.NotNull(window);
        return window;
    }

    private AutomationElement? FindByAutomationId(AutomationElement root, string automationId)
    {
        return root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
    }

    private static void Click(AutomationElement element)
    {
        var pattern = (InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern);
        pattern.Invoke();
    }

    private static void SetValue(AutomationElement element, string value)
    {
        var pattern = (ValuePattern)element.GetCurrentPattern(ValuePattern.Pattern);
        pattern.SetValue(value);
    }

    [Fact]
    public void App_Launches_MainWindowVisible()
    {
        Assert.NotNull(_mainWindow);
        Assert.True(_mainWindow.Current.IsEnabled);
    }

    [Fact]
    public void StartStopTimer_ButtonsWork()
    {
        var startButton = FindByAutomationId(_mainWindow!, "StartTimerButton");
        Assert.NotNull(startButton);
        Click(startButton);

        Thread.Sleep(500);

        var stopButton = FindByAutomationId(_mainWindow!, "StopTimerButton");
        Assert.NotNull(stopButton);
        Click(stopButton);

        // If the app crashed, the main window will be gone.
        _mainWindow = WaitForMainWindow();
        Assert.NotNull(_mainWindow);
    }

    [Fact]
    public void AddDurationEntry_ButtonWorks()
    {
        var descriptionBox = FindByAutomationId(_mainWindow!, "ManualDescriptionTextBox");
        var durationBox = FindByAutomationId(_mainWindow!, "ManualDurationTextBox");
        var addButton = FindByAutomationId(_mainWindow!, "AddDurationButton");

        Assert.NotNull(descriptionBox);
        Assert.NotNull(durationBox);
        Assert.NotNull(addButton);

        SetValue(descriptionBox, "UI test entry");
        SetValue(durationBox, "30");
        Click(addButton);

        Thread.Sleep(500);
        _mainWindow = WaitForMainWindow();
        Assert.NotNull(_mainWindow);
    }
}
