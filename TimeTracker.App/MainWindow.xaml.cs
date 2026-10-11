using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TimeTracker.App.ViewModels;
using Application = System.Windows.Application;

namespace TimeTracker.App;

public partial class MainWindow : Window
{
    private readonly ScreenEventsWindow _screenEventsWindow;
    private readonly ImportExportHistoryWindow _importExportHistoryWindow;
    private NotifyIcon? _notifyIcon;
    private TrayPopupWindow? _trayPopup;
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
        var iconSize = SystemInformation.SmallIconSize.Width;
        var idleIcon = CreateCircleIcon(iconSize, System.Drawing.Color.LimeGreen);
        var trackingIcon = CreateCircleIcon(iconSize, System.Drawing.Color.Red);

        _notifyIcon = new NotifyIcon
        {
            Icon = idleIcon,
            Visible = true,
            Text = "Time Tracker - Idle"
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
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (sender, e) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;

        ViewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.IsTimerRunning))
            {
                _notifyIcon.Icon = ViewModel.IsTimerRunning ? trackingIcon : idleIcon;
                _notifyIcon.Text = ViewModel.IsTimerRunning ? "Time Tracker - Tracking" : "Time Tracker - Idle";
            }
        };
    }

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

    private void ExitApplication()
    {
        _isExiting = true;
        _notifyIcon?.Dispose();
        Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
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

    private void OnEntryDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListViewItem { Content: TimeEntryViewModel entry })
        {
            ViewModel.EditEntryCommand.Execute(entry);
        }
    }

    private void OnPlayEntryClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: TimeEntryViewModel entry })
        {
            ViewModel.TimerDescription = entry.Description;
            if (!ViewModel.IsTimerRunning)
            {
                ViewModel.StartTimerCommand.Execute(null);
            }
        }

        e.Handled = true;
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

