using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TimeTracker.App.ViewModels;
using TimeTracker.App.Helpers;
using Application = System.Windows.Application;

namespace TimeTracker.App;

public partial class MainWindow : Window
{
    private readonly ScreenEventsWindow _screenEventsWindow;
    private readonly ImportExportHistoryWindow _importExportHistoryWindow;
    private NotifyIcon? _notifyIcon;
    private TrayPopupWindow? _trayPopup;
    private ToolStripMenuItem? _startupTrayMenuItem;
    private bool _isExiting;

    public MainWindow(ScreenEventsWindow screenEventsWindow, ImportExportHistoryWindow importExportHistoryWindow)
    {
        _screenEventsWindow = screenEventsWindow;
        _importExportHistoryWindow = importExportHistoryWindow;
        InitializeComponent();
        LoadWindowIcon();
        Loaded += OnLoaded;
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void LoadWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "TimeTracker.ico");
        if (File.Exists(iconPath))
        {
            Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        }
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel)
        {
            InitializeTrayIcon();
        }
    }

    private void InitializeTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }
        var iconSize = SystemInformation.SmallIconSize.Width;
        var idleIcon = CreateCircleIcon(iconSize, System.Drawing.Color.LimeGreen);
        var trackingIcon = CreateCircleIcon(iconSize, System.Drawing.Color.Red);

        _notifyIcon = new NotifyIcon
        {
            Icon = ViewModel.IsTimerRunning ? trackingIcon : idleIcon,
            Visible = true,
            Text = ViewModel.IsTimerRunning ? "Time Tracker - Tracking" : "Time Tracker - Idle"
        };

        _notifyIcon.DoubleClick += (sender, e) => ShowAndActivate();
        _notifyIcon.MouseClick += (sender, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowTrayPopup();
            }
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (sender, e) => ShowAndActivate());
        _startupTrayMenuItem = new ToolStripMenuItem("Launch at startup");
        _startupTrayMenuItem.Click += (_, _) => ToggleLaunchAtStartup();
        contextMenu.Items.Add(_startupTrayMenuItem);
        contextMenu.Opening += (_, _) => RefreshStartupMenus();
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (sender, e) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;
        RefreshStartupMenus();

        ViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsTimerRunning))
            {
                _notifyIcon.Icon = ViewModel.IsTimerRunning ? trackingIcon : idleIcon;
                _notifyIcon.Text = ViewModel.IsTimerRunning ? "Time Tracker - Tracking" : "Time Tracker - Idle";
            }
        };
    }

    public void InitializeForLaunch(bool startInTray)
    {
        // A never-shown window does not raise Loaded. Initialize the tray explicitly.
        InitializeTrayIcon();
        if (!startInTray)
        {
            ShowAndActivate();
        }
    }

    private void RefreshStartupMenus()
    {
        try
        {
            var enabled = WindowsStartupRegistration.IsEnabled();
            LaunchAtStartupMenuItem.IsEnabled = true;
            LaunchAtStartupMenuItem.IsChecked = enabled;
            if (_startupTrayMenuItem is not null)
            {
                _startupTrayMenuItem.Enabled = true;
                _startupTrayMenuItem.Checked = enabled;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            LaunchAtStartupMenuItem.IsEnabled = false;
            LaunchAtStartupMenuItem.ToolTip = $"Cannot read Windows startup registration: {ex.Message}";
            if (_startupTrayMenuItem is not null)
            {
                _startupTrayMenuItem.Enabled = false;
                _startupTrayMenuItem.ToolTipText = LaunchAtStartupMenuItem.ToolTip.ToString();
            }
        }
    }

    private void ToggleLaunchAtStartup()
    {
        try
        {
            WindowsStartupRegistration.SetEnabled(!WindowsStartupRegistration.IsEnabled());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
        {
            System.Windows.MessageBox.Show($"Could not update Windows startup registration.\n\n{ex.Message}",
                "Launch at startup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshStartupMenus();
    }

    private void OnLaunchAtStartupClick(object sender, RoutedEventArgs e) => ToggleLaunchAtStartup();

    private void OnFileMenuOpened(object sender, RoutedEventArgs e) => RefreshStartupMenus();

    private static Icon CreateCircleIcon(int size, System.Drawing.Color color)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);

        var padding = Math.Max(1, size / 8);
        var rect = new Rectangle(padding, padding, size - padding * 2 - 1, size - padding * 2 - 1);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, rect);
        using var outlinePen = new Pen(System.Drawing.Color.FromArgb(40, 0, 0, 0), Math.Max(1, size / 16f));
        graphics.DrawEllipse(outlinePen, rect);

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void ShowTrayPopup()
    {
        if (_trayPopup is null || _trayPopup.IsClosed)
        {
            _trayPopup = new TrayPopupWindow(this);
        }

        _trayPopup.ShowNearTray(System.Windows.Forms.Cursor.Position);
        _trayPopup.Activate();
    }

    public void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private async void ExitApplication()
    {
        await ViewModel.SaveEntryQueryPreferencesAsync();
        _isExiting = true;
        _notifyIcon?.Dispose();
        Application.Current.Shutdown();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        await ViewModel.SaveEntryQueryPreferencesAsync();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private async void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        var eventType = e.Reason switch
        {
            Microsoft.Win32.SessionSwitchReason.SessionLock => "Lock",
            Microsoft.Win32.SessionSwitchReason.SessionUnlock => "Unlock",
            _ => null
        };

        if (eventType is not null && DataContext is MainViewModel viewModel)
        {
            await viewModel.RecordSessionEventAsync(eventType).ConfigureAwait(false);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
        _notifyIcon?.Dispose();
        base.OnClosed(e);
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        ExitApplication();
    }

    private void OnDefaultTimerTextClick(object sender, RoutedEventArgs e)
    {
        var window = new DefaultTimerTextWindow(ViewModel)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OnScreenEventsClick(object sender, RoutedEventArgs e)
    {
        _screenEventsWindow.Show();
        _screenEventsWindow.Activate();
        if (_screenEventsWindow.DataContext is ScreenEventsViewModel vm)
        {
            vm.RefreshCommand.Execute(null);
        }
    }

    private void OnImportExportHistoryClick(object sender, RoutedEventArgs e)
    {
        _importExportHistoryWindow.Show();
        _importExportHistoryWindow.Activate();
        if (_importExportHistoryWindow.DataContext is ImportExportHistoryViewModel vm)
        {
            vm.RefreshCommand.Execute(null);
        }
    }

    private void OnUsageClick(object sender, RoutedEventArgs e)
    {
        var window = new UsageWindow { Owner = this };
        window.ShowDialog();
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var window = new AboutWindow()
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OnPlayEntryClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: TimeEntryViewModel entry })
        {
            if (entry.IsRunning)
            {
                ViewModel.StopEntryCommand.Execute(entry);
            }
            else if (!ViewModel.IsTimerRunning)
            {
                ViewModel.TimerDescription = entry.Description;
                ViewModel.StartTimerCommand.Execute(null);
            }
        }

        e.Handled = true;
    }

    private void OnEntryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not System.Windows.Controls.ListView list ||
            e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(list, source) is not System.Windows.Controls.ListViewItem { DataContext: TimeEntryViewModel entry } row)
        {
            return;
        }
        // Double-clicking a row's timer control must not also open the editor.
        for (var element = source; element is not null && element != row;
             element = System.Windows.Media.VisualTreeHelper.GetParent(element))
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase) return;
        }
        e.Handled = true;
        ViewModel.EditEntryCommand.Execute(entry);
    }

    private void OnEntriesSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep all metadata available, and give surplus width to the description.
        if (DescriptionColumn is not null)
        {
            DescriptionColumn.Width = Math.Max(210, e.NewSize.Width - 685);
        }
    }

    private void OnTimerDescriptionGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private void OnTimerDescriptionLostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ViewModel.IsTimerSuggestionsOpen = false, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnTimerDescriptionTextChanged(object sender, TextChangedEventArgs e)
    {
        // Suggestions update is handled via PropertyChanged in ViewModel.
    }

    private void OnTimerSuggestionSelected(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: string suggestion })
        {
            ViewModel.TimerDescription = suggestion;
            ViewModel.IsTimerSuggestionsOpen = false;
        }
    }

    private void OnManualDateValidationError(object sender, DatePickerDateValidationErrorEventArgs e)
    {
        e.ThrowException = false;
        if (sender is not DatePicker datePicker)
        {
            return;
        }
        // Invalid typed dates must not silently reuse the previously selected date.
        datePicker.SelectedDate = null;
        if (System.Windows.Automation.AutomationProperties.GetAutomationId(datePicker) == "DurationStartDatePicker")
        {
            ViewModel.ManualStartDate = null;
            ViewModel.ManualDurationError = "Enter a valid Start Date.";
        }
        else
        {
            ViewModel.RangeStartDate = null;
            ViewModel.ManualRangeError = "Enter a valid Start Date.";
        }
    }

    private void OnManualDescriptionGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private void OnManualDescriptionLostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ViewModel.IsManualSuggestionsOpen = false, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnManualDescriptionTextChanged(object sender, TextChangedEventArgs e)
    {
        // Suggestions update is handled via PropertyChanged in ViewModel.
    }

    private void OnRangeDescriptionLostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ViewModel.IsRangeSuggestionsOpen = false, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnRangeSuggestionSelected(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: string suggestion })
        {
            ViewModel.RangeDescription = suggestion;
            ViewModel.IsRangeSuggestionsOpen = false;
        }
    }

    private void OnManualSuggestionSelected(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox { SelectedItem: string suggestion })
        {
            ViewModel.ManualDescription = suggestion;
            ViewModel.IsManualSuggestionsOpen = false;
        }
    }
}

