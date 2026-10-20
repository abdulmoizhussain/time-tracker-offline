using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TimeTracker.App.ViewModels;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;
using Application = System.Windows.Application;

namespace TimeTracker.App;

public partial class MainWindow : Window
{
    private readonly ScreenEventsWindow _screenEventsWindow;
    private readonly ImportExportHistoryWindow _importExportHistoryWindow;
    private NotifyIcon? _notifyIcon;
    private TrayPopupWindow? _trayPopup;
    // private ToolStripMenuItem? _startupTrayMenuItem; // Startup registration disabled (AR-039).
    private bool _isExiting;
    private bool _restoringEntryLayout;
    private bool _showingUnfinishedTimer;
    private UnfinishedTimerWindow? _unfinishedTimerWindow;

    public MainWindow(ScreenEventsWindow screenEventsWindow, ImportExportHistoryWindow importExportHistoryWindow)
    {
        _screenEventsWindow = screenEventsWindow;
        _importExportHistoryWindow = importExportHistoryWindow;
        InitializeComponent();
        DataContextChanged += (_, _) => RestoreEntryLayout();
        var view = (GridView)EntriesListView.View;
        ((INotifyCollectionChanged)view.Columns).CollectionChanged += (_, _) => CaptureEntryLayout();
        foreach (var column in view.Columns)
            DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn))
                .AddValueChanged(column, OnEntryColumnWidthChanged);
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
        /* Startup registration disabled (AR-039).
        _startupTrayMenuItem = new ToolStripMenuItem("Launch at startup");
        _startupTrayMenuItem.Click += (_, _) => ToggleLaunchAtStartup();
        contextMenu.Items.Add(_startupTrayMenuItem);
        contextMenu.Opening += (_, _) => RefreshStartupMenus();
        */
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (sender, e) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;
        // RefreshStartupMenus(); // Startup registration disabled (AR-039).

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

    /* Startup registration disabled (AR-039); retained only as source comments.
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
        // RefreshStartupMenus(); // Startup registration disabled (AR-039).
    }

    private void OnLaunchAtStartupClick(object sender, RoutedEventArgs e) => ToggleLaunchAtStartup();

    private void OnFileMenuOpened(object sender, RoutedEventArgs e) => RefreshStartupMenus();

    */

    private static Icon CreateCircleIcon(int size, System.Drawing.Color color)
    {
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);

        // Windows controls the tray slot size; use its full canvas without an outer margin.
        var rect = new RectangleF(0, 0, size, size);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, rect);

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
        if (_isExiting) return;
        _isExiting = true;
        _unfinishedTimerWindow?.Close();
        try { await ViewModel.CaptureTimerInterruptionAsync(TimerInterruptionReason.Exit, DateTime.UtcNow); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not record the exit time: {ex.Message}", "Timer recovery"); }
        CaptureEntryLayout();
        await ViewModel.SaveEntryQueryPreferencesAsync();
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
        CaptureEntryLayout();
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
        var occurredUtc = DateTime.UtcNow;
        var eventType = e.Reason switch
        {
            Microsoft.Win32.SessionSwitchReason.SessionLock => "Lock",
            Microsoft.Win32.SessionSwitchReason.SessionUnlock => "Unlock",
            _ => null
        };

        if (eventType is not null && !Dispatcher.HasShutdownStarted)
        {
            try
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (_isExiting || DataContext is not MainViewModel viewModel) return;
                    if (eventType == "Lock")
                    {
                        CaptureEntryLayout();
                        await viewModel.SaveEntryQueryPreferencesAsync();
                        try { await viewModel.CaptureTimerInterruptionAsync(TimerInterruptionReason.Lock, occurredUtc); }
                        catch (Exception ex) { viewModel.TimerError = $"Could not save lock time: {ex.Message}"; }
                    }
                    else viewModel.OnSessionUnlocked();
                    try { await viewModel.RecordSessionEventAsync(eventType, occurredUtc); }
                    catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Session history: {ex}"); }
                    if (eventType == "Unlock") await ShowUnfinishedTaskAsync(startup: false);
                }).Task.Unwrap();
            }
            catch (TaskCanceledException) when (_isExiting || Dispatcher.HasShutdownStarted) { }
        }
    }

    public async Task RecordSystemShutdownAsync()
    {
        _isExiting = true;
        _unfinishedTimerWindow?.Close();
        CaptureEntryLayout();
        await ViewModel.SaveEntryQueryPreferencesAsync();
        await ViewModel.CaptureTimerInterruptionAsync(TimerInterruptionReason.Shutdown, DateTime.UtcNow);
    }

    public async Task ShowUnfinishedTaskAsync(bool startup)
    {
        if (_isExiting) return;
        if (_showingUnfinishedTimer)
        {
            _unfinishedTimerWindow?.BringToFront();
            return;
        }
        _showingUnfinishedTimer = true;
        try
        {
            while (!_isExiting)
            {
                var unfinished = await ViewModel.GetUnfinishedTimerAsync(startup);
                startup = false;
                if (unfinished is null || _isExiting) return;
                var (entry, interruption) = unfinished.Value;
                _unfinishedTimerWindow = new UnfinishedTimerWindow(entry.Description, interruption);
                if (IsVisible)
                {
                    _unfinishedTimerWindow.Owner = this;
                    _unfinishedTimerWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                _unfinishedTimerWindow.ShowDialog();
                if (_isExiting) return;
                await ViewModel.ResolveUnfinishedTimerAsync(interruption,
                    _unfinishedTimerWindow.Action != UnfinishedTimerAction.Continue,
                    _unfinishedTimerWindow.Action == UnfinishedTimerAction.StopAtRecordedTime);
            }
        }
        catch (Exception ex)
        {
            if (!_isExiting) System.Windows.MessageBox.Show($"Could not resolve the unfinished task: {ex.Message}", "Timer recovery");
        }
        finally { _unfinishedTimerWindow = null; _showingUnfinishedTimer = false; }
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var column in ((GridView)EntriesListView.View).Columns)
            DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn))
                .RemoveValueChanged(column, OnEntryColumnWidthChanged);
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

    private void OnEntryPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListView list || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(list, source) is not System.Windows.Controls.ListViewItem row) return;

        // Select the full row even when the description TextBox handles the click.
        // Leave the event and focus alone so text copying, double-click editing, and controls still work.
        for (var element = source; element is not null && element != row;
             element = System.Windows.Media.VisualTreeHelper.GetParent(element))
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase) return;
        }
        row.IsSelected = true;
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
            if (element is System.Windows.Controls.Primitives.ButtonBase or System.Windows.Controls.TextBox) return;
        }
        e.Handled = true;
        ViewModel.EditEntryCommand.Execute(entry);
    }

    private void RestoreEntryLayout()
    {
        if (DataContext is not MainViewModel viewModel) return;
        _restoringEntryLayout = true;
        try { EntryGridLayout.Apply((GridView)EntriesListView.View, viewModel.EntryColumns); }
        finally { _restoringEntryLayout = false; }
    }

    private void OnEntryColumnWidthChanged(object? sender, EventArgs e)
    {
        if (_restoringEntryLayout) return;
        Dispatcher.BeginInvoke(CaptureEntryLayout, DispatcherPriority.Background);
    }

    private void CaptureEntryLayout()
    {
        if (!_restoringEntryLayout && DataContext is MainViewModel viewModel)
            viewModel.UpdateEntryColumnLayout(EntryGridLayout.Capture((GridView)EntriesListView.View));
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
        CloseSuggestionsAfterFocusLoss(sender);
    }

    private void OnTimerDescriptionTextChanged(object sender, TextChangedEventArgs e)
    {
        // Suggestions update is handled via PropertyChanged in ViewModel.
    }

    private void OnTimerSuggestionSelected(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TryGetSuggestion(sender, e, out var suggestion))
        {
            ViewModel.SelectDescriptionSuggestion("Timer", suggestion);
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
        CloseSuggestionsAfterFocusLoss(sender);
    }

    private void OnManualDescriptionTextChanged(object sender, TextChangedEventArgs e)
    {
        // Suggestions update is handled via PropertyChanged in ViewModel.
    }

    private void OnRangeDescriptionLostFocus(object sender, RoutedEventArgs e)
    {
        CloseSuggestionsAfterFocusLoss(sender);
    }

    private void OnManualEntryCollapsed(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.CloseDescriptionSuggestions("Manual");
        viewModel.CloseDescriptionSuggestions("Range");
    }

    private void CloseSuggestionsAfterFocusLoss(object sender)
    {
        if (sender is not System.Windows.Controls.TextBox { Tag: string field } textBox) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (!textBox.IsKeyboardFocusWithin) ViewModel.CloseDescriptionSuggestions(field);
        }, DispatcherPriority.Background);
    }

    private System.Windows.Controls.ListBox SuggestionList(string field) => field switch
    {
        "Timer" => TimerSuggestionsListBox, "Manual" => ManualSuggestionsListBox, "Range" => RangeSuggestionsListBox,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private void OnDescriptionPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox { Tag: string field } textBox) return;
        // Holding Enter after accepting a suggestion must not also start the timer.
        if (field == "Timer" && e.Key == Key.Enter && e.IsRepeat)
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && ViewModel.SuggestionsOpen(field))
        {
            ViewModel.CloseDescriptionSuggestions(field);
            e.Handled = true;
        }
        else if (ViewModel.SuggestionsOpen(field) && e.Key is Key.Up or Key.Down)
        {
            ViewModel.MoveDescriptionSuggestion(field, e.Key == Key.Down ? 1 : -1);
            ScrollSelectedSuggestion(field);
            e.Handled = true;
        }
        else if (ViewModel.SuggestionsOpen(field) && e.Key == Key.Enter)
        {
            ViewModel.AcceptDescriptionSuggestion(field);
            textBox.CaretIndex = textBox.Text.Length;
            e.Handled = true;
        }
        else if (field == "Timer" && e.Key == Key.Enter) OnTimerStartPreviewKeyDown(sender, e);
        else if (e.Key == Key.Down && string.IsNullOrWhiteSpace(textBox.Text)) e.Handled = true;
    }

    private void OnDurationLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox) NormalizeDuration(textBox);
    }

    private void OnDurationPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not System.Windows.Controls.TextBox textBox) return;
        NormalizeDuration(textBox);
        e.Handled = true;
    }

    private void NormalizeDuration(System.Windows.Controls.TextBox textBox)
    {
        var isTimer = textBox == TimerInitialDurationTextBox;
        var valid = isTimer
            ? TimerInput.TryInitialDuration(textBox.Text, out var duration)
            : ManualEntryValidation.TryDuration(textBox.Text, out duration);
        var error = valid || string.IsNullOrWhiteSpace(textBox.Text) ? string.Empty
            : $"Invalid duration. Use Clock pattern or Unit pattern (e.g. 1h 2m 3s); maximum {(isTimer ? "23:59:59" : "24:00:00")}.";
        if (isTimer) ViewModel.TimerError = error;
        else ViewModel.ManualDurationError = error;
        if (!valid || string.IsNullOrWhiteSpace(textBox.Text)) return;
        var formatted = DurationFormatter.Clock(duration.Ticks / TimeSpan.TicksPerSecond);
        if (isTimer) ViewModel.TimerInitialDuration = formatted;
        else ViewModel.ManualDuration = formatted;
    }

    private void OnTimerStartPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (sender is System.Windows.Controls.TextBox textBox && textBox == TimerInitialDurationTextBox) NormalizeDuration(textBox);
        e.Handled = true;
        if (e.IsRepeat || ViewModel.IsTimerRunning || string.IsNullOrWhiteSpace(ViewModel.TimerDescription) ||
            !ViewModel.StartTimerCommand.CanExecute(null)) return;

        ViewModel.CloseDescriptionSuggestions("Timer");
        ViewModel.StartTimerCommand.Execute(null);
    }

    private async void OnDescriptionPreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox { Tag: string field } textBox ||
            e.Key != Key.Down || !string.IsNullOrWhiteSpace(textBox.Text) || ViewModel.SuggestionsOpen(field)) return;
        e.Handled = true;
        await ViewModel.ShowRecentDescriptionSuggestionsAsync(field);
        ScrollSelectedSuggestion(field);
    }

    private void ScrollSelectedSuggestion(string field)
    {
        var list = SuggestionList(field);
        if (list.SelectedItem is not null) list.ScrollIntoView(list.SelectedItem);
    }

    private void OnRangeSuggestionSelected(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TryGetSuggestion(sender, e, out var suggestion))
        {
            ViewModel.SelectDescriptionSuggestion("Range", suggestion);
            ViewModel.IsRangeSuggestionsOpen = false;
        }
    }

    private void OnManualSuggestionSelected(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TryGetSuggestion(sender, e, out var suggestion))
        {
            ViewModel.SelectDescriptionSuggestion("Manual", suggestion);
            ViewModel.IsManualSuggestionsOpen = false;
        }
    }

    private static bool TryGetSuggestion(object sender, MouseButtonEventArgs e, out string suggestion)
    {
        suggestion = string.Empty;
        if (sender is not System.Windows.Controls.ListBox list || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(list, source) is not ListBoxItem { DataContext: string text }) return false;
        suggestion = text;
        e.Handled = true;
        return true;
    }

}

