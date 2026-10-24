using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.Options;
using TimeTracker.App;
using TimeTracker.App.ViewModels;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;
using TimeTracker.Core.Configuration;
using TimeTracker.Data;

namespace UiPreview;

// Render actual WPF content without launching the app, showing windows, or acquiring its mutex.
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/ui-preview");
        Directory.CreateDirectory(output);
        var diagnostics = new BindingDiagnostics();
        PresentationTraceSources.DataBindingSource.Listeners.Add(diagnostics);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        // Do not instantiate App: Application queues Startup even without calling Run.
        var app = new Application();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/TimeTracker;component/Styles/Theme.xaml", UriKind.Relative)
        });
        app.Resources["BaseFontSize"] = 15d;
        app.Resources["UiScaleMultiplier"] = 1d;
        app.Resources["BoolToVisibilityConverter"] = new TimeTracker.App.Converters.BoolToVisibilityConverter();
        app.Resources["RunningForegroundConverter"] = new TimeTracker.App.Converters.RunningForegroundConverter();
        app.Resources["ComposerHeightConverter"] = new TimeTracker.App.Converters.ComposerHeightConverter();
        var windowStyle = new Style(typeof(Window));
        windowStyle.Setters.Add(new Setter(Window.FontSizeProperty, 15d));
        windowStyle.Setters.Add(new Setter(Window.FontFamilyProperty, new FontFamily("Segoe UI")));
        windowStyle.Setters.Add(new Setter(Window.ForegroundProperty, app.FindResource("TextBrush")));
        app.Resources["ScaledWindowStyle"] = windowStyle;
        // A fresh preview database each run; never use production records.
        var database = Path.Combine(output, $"preview-{Guid.NewGuid():N}.db");
        var repository = new TimeEntryRepository(new SqliteConnectionFactory(database));
        Wait(repository.InitializeAsync());
        Wait(repository.AddDurationEntryAsync("Review the weekly plan", TimeSpan.FromMinutes(45), DateTime.Today));
        Wait(repository.AddDurationEntryAsync("Deep work · implementation", TimeSpan.FromHours(2.5), DateTime.Today));
        Wait(repository.AddDurationEntryAsync("Historical long duration", TimeSpan.FromHours(333333), DateTime.Today.AddDays(-1)));
        var vm = new MainViewModel(repository, repository, Options.Create(new AppRuntimeOptions()));
        Wait(vm.InitializeAsync(database));
        var main = new MainWindow(new ScreenEventsWindow(), new ImportExportHistoryWindow()) { DataContext = vm };
        Render(main, 1100, 760, Path.Combine(output, "main-idle.png"));
        AssertTimerButtonAlignment(main, "StartTimerButton", "TimerDescriptionTextBox");
        AssertEntryAlignment(main);
        VerifyRowSelection(main, vm, output);
        VerifyDurationInputs(main, vm);
        VerifyTimerEnter(main, vm);
        VerifyViewRestart(main, vm, repository, database, output);
        vm.TimerInitialDuration = "1.5";
        var expandedListHeight = ((ListView)main.FindName("EntriesListView")).ActualHeight;
        var manualExpander = (Expander)main.FindName("ManualEntryExpander");
        vm.ManualDescription = "Draft to preserve";
        vm.RangeDescription = "Independent range draft";
        manualExpander.IsExpanded = false;
        Render(main, 1100, 760, Path.Combine(output, "main-manual-collapsed.png"));
        if (((ListView)main.FindName("EntriesListView")).ActualHeight <= expandedListHeight)
            throw new InvalidOperationException("Collapsing manual entry did not increase list height.");
        manualExpander.IsExpanded = true;
        if (vm.ManualDescription != "Draft to preserve" || vm.RangeDescription != "Independent range draft")
            throw new InvalidOperationException("Collapsing manual entry lost drafts.");
        vm.ManualDescription = vm.RangeDescription = string.Empty;
        vm.TimerDescription = "Building a more comfortable TimeTracker interface";
        Wait(vm.StartTimerCommand.ExecuteAsync(null));
        var runningWindow = CreateMain(vm);
        Render(runningWindow, 1100, 760, Path.Combine(output, "main-running.png"));
        AssertTimerButtonAlignment(runningWindow, "StopTimerButton", "TimerDescriptionTextBox");
        vm.IsFiltersExpanded = true;
        vm.FilterDescription = "work";
        Render(CreateMain(vm), 960, 510, Path.Combine(output, "main-small-filters.png"));
        app.Resources["UiScaleMultiplier"] = 1.5;
        Render(CreateMain(vm), 1100, 760, Path.Combine(output, "main-scaled.png"));
        app.Resources["UiScaleMultiplier"] = 1.0;
        vm.IsFiltersExpanded = false;
        Render(new TrayPopupWindow(main), 340, 240, Path.Combine(output, "tray-running.png"));
        Render(new UsageWindow(), 660, 610, Path.Combine(output, "usage.png"));
        Render(new EditEntryWindow(new EditEntryViewModel(new TimeEntry { Description = "Review the weekly plan", IsRunning = false, StartUtc = DateTime.Today.AddHours(10).ToUniversalTime(), EndUtc = DateTime.Today.AddHours(11).ToUniversalTime(), DurationSeconds = 3600 }, _ => Task.CompletedTask)), 600, 420, Path.Combine(output, "edit-entry.png"));
        Render(new EditEntryWindow(new EditEntryViewModel(new TimeEntry { Description = "Running task", IsRunning = true, StartUtc = DateTime.Today.AddHours(10).ToUniversalTime(), EndUtc = DateTime.Today.AddHours(11).ToUniversalTime(), DurationSeconds = 3600 }, _ => Task.CompletedTask)), 600, 420, Path.Combine(output, "edit-running-entry.png"));
        Render(new EditEntryWindow(new EditEntryViewModel(new TimeEntry { Description = "   ", IsRunning = false, StartUtc = DateTime.Today.AddHours(10).ToUniversalTime(), EndUtc = DateTime.Today.AddHours(11).ToUniversalTime(), DurationSeconds = 3600 }, _ => Task.CompletedTask)), 600, 420, Path.Combine(output, "edit-entry-blank.png"));
        VerifyEditSuggestions(repository, output);
        Render(new UnfinishedTimerWindow("Review the weekly plan", new(1, TimeTracker.Core.Models.TimerInterruptionReason.Lock, DateTime.UtcNow)), 580, 400, Path.Combine(output, "unfinished-lock.png"));
        Render(new UnfinishedTimerWindow("After an abrupt shutdown", new(1, TimeTracker.Core.Models.TimerInterruptionReason.LastSeen, DateTime.UtcNow)), 580, 400, Path.Combine(output, "unfinished-activity.png"));
        Render(new UnfinishedTimerWindow("Legacy running entry", new(1, TimeTracker.Core.Models.TimerInterruptionReason.Unknown, null)), 580, 400, Path.Combine(output, "unfinished-unknown.png"));
        var exportOptions = new ExportOptionsWindow();
        var exportVm = (ExportOptionsViewModel)exportOptions.DataContext;
        exportVm.UseDateRange = true;
        exportVm.FromDate = DateTime.Today.AddDays(-7);
        exportVm.ToDate = DateTime.Today;
        Render(exportOptions, 520, 400, Path.Combine(output, "export-options.png"));
        Render(new ConfirmWindow("Remove entry?", "Remove 'Review the weekly plan'? This entry will be deleted from your local database.", "Remove entry"), 480, 230, Path.Combine(output, "confirmation.png"));
        vm.FilterDescription = "no match";
        Wait(vm.ApplyEntryFiltersCommand.ExecuteAsync(null));
        Render(CreateMain(vm), 1100, 760, Path.Combine(output, "main-empty.png"));
        Wait(vm.StopTimerCommand.ExecuteAsync(null));
        if (vm.TimerDescription != string.Empty) throw new InvalidOperationException("Stopping did not clear the timer description.");
        vm.FilterDescription = string.Empty;
        Wait(vm.ApplyEntryFiltersCommand.ExecuteAsync(null));
        vm.TimerDescription = "Stop from entry list";
        Wait(vm.StartTimerCommand.ExecuteAsync(null));
        Wait(vm.StopEntryCommand.ExecuteAsync(vm.Entries.Single(entry => entry.IsRunning)));
        if (vm.TimerDescription != string.Empty) throw new InvalidOperationException("List stop did not clear the timer description.");
        vm.TimerDescription = "Stop from recovery popup";
        Wait(vm.StartTimerCommand.ExecuteAsync(null));
        var runningEntry = vm.Entries.Single(entry => entry.IsRunning);
        Wait(vm.ResolveUnfinishedTimerAsync(new(runningEntry.Id, TimeTracker.Core.Models.TimerInterruptionReason.Lock, DateTime.UtcNow), true, true));
        if (vm.TimerDescription != string.Empty) throw new InvalidOperationException("Recovery stop did not clear the timer description.");
        if (diagnostics.Errors.Count != 0)
        {
            foreach (var error in diagnostics.Errors.Distinct()) Console.Error.WriteLine(error);
            throw new InvalidOperationException("WPF binding errors detected during preview.");
        }
        Console.WriteLine($"Rendered eighteen previews to {output}; no WPF binding errors.");
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(database);
        File.Delete(database + "-wal");
        File.Delete(database + "-shm");
        File.Delete(database + ".view.json");
        File.Delete(database + ".view.json.tmp");
    }

    private static void VerifyRowSelection(MainWindow main, MainViewModel vm, string output)
    {
        var list = (ListView)main.FindName("EntriesListView");
        var rows = Descendants<ListViewItem>(list).ToArray();
        var sameDay = rows.GroupBy(row => ((TimeEntryViewModel)row.DataContext).GroupDate).First(group => group.Count() >= 2).ToArray();
        if (Equals(sameDay[0].Background, sameDay[1].Background))
            throw new InvalidOperationException("Same-day rows do not alternate backgrounds.");

        var row = sameDay[0];
        var description = Descendants<TextBox>(row).Single();
        description.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,
            Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent
        });
        if (!row.IsSelected || vm.SelectedEntry != row.DataContext || description.SelectionLength != 0)
            throw new InvalidOperationException("Description click did not select only the whole row.");
        if (row.IsKeyboardFocusWithin || !Equals(row.Background, Application.Current.FindResource("SelectedRowBrush")))
            throw new InvalidOperationException("Selected row background requires keyboard focus.");
        description.Select(0, Math.Min(4, description.Text.Length));
        if (description.SelectedText.Length == 0) throw new InvalidOperationException("Description copying is no longer available.");
        description.Select(0, 0);
        Render(main, 1100, 760, Path.Combine(output, "main-selected-striped.png"));

        var selectedId = vm.SelectedEntry!.Id;
        var oldItem = vm.SelectedEntry;
        Wait(vm.ApplyEntryFiltersCommand.ExecuteAsync(null));
        if (vm.SelectedEntry?.Id != selectedId || ReferenceEquals(vm.SelectedEntry, oldItem))
            throw new InvalidOperationException("Selection was not restored to the reloaded entry.");
        vm.FilterDescription = "no matching entry for selection check";
        Wait(vm.ApplyEntryFiltersCommand.ExecuteAsync(null));
        if (vm.SelectedEntry is not null) throw new InvalidOperationException("Filtered-out entry remains selected.");
        Wait(vm.ResetEntryFiltersCommand.ExecuteAsync(null));
        Render(main, 1100, 760, Path.Combine(output, "main-idle.png"));
        Console.WriteLine("Row checks passed: zebra stripes, description click selects whole row, inactive selection, text copying, refresh restoration, filtered-out selection.");
    }

    private static void VerifyEditSuggestions(TimeEntryRepository repository, string output)
    {
        Wait(repository.AddDurationEntryAsync("Edit suggestion alpha", TimeSpan.FromMinutes(15), DateTime.Today));
        Wait(repository.AddDurationEntryAsync("Edit suggestion beta", TimeSpan.FromMinutes(15), DateTime.Today));
        var saved = false;
        var vm = new EditEntryViewModel(new TimeEntry
        {
            Description = "Editor original", StartUtc = DateTime.Today.AddHours(10).ToUniversalTime(), DurationSeconds = 3600
        }, _ => { saved = true; return Task.CompletedTask; },
            query => repository.SearchDescriptionsAsync(query, 8),
            async () => (await repository.GetRecentEntriesAsync(5)).Select(e => e.Description).ToList());
        var dialog = new EditEntryWindow(vm);
        Render(dialog, 600, 420, Path.Combine(output, "edit-entry-search.png"));
        // Popup.IsOpen is coerced false without a loaded presentation source.
        // Host this test window off-screen without activation or a taskbar button.
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = dialog.Top = -10000;
        dialog.ShowActivated = false;
        dialog.ShowInTaskbar = false;
        dialog.Show();
        var box = (TextBox)dialog.FindName("DescriptionTextBox");
        var list = (ListBox)dialog.FindName("SuggestionsListBox");
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Edit suggestion checks"));
        void Type(string text)
        {
            box.Text = text;
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Wait(vm.SuggestionLoading);
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
        System.Windows.Input.KeyEventArgs Key(System.Windows.Input.Key key, bool up = false)
        {
            var e = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            { RoutedEvent = up ? System.Windows.Input.Keyboard.PreviewKeyUpEvent : System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            box.RaiseEvent(e);
            return e;
        }
        Type("Edit suggestion");
        if (!vm.IsSuggestionsOpen || vm.Suggestions.Count < 2 ||
            !((System.Windows.Controls.Primitives.Popup)dialog.FindName("SuggestionsPopup")).IsOpen)
            throw new InvalidOperationException($"Edit entry typing did not open matching suggestions: open={vm.IsSuggestionsOpen}, count={vm.Suggestions.Count}, popup={((System.Windows.Controls.Primitives.Popup)dialog.FindName("SuggestionsPopup")).IsOpen}, text={vm.Description}.");
        Key(System.Windows.Input.Key.Down);
        var expected = vm.Suggestions[vm.SuggestionIndex];
        if (!Key(System.Windows.Input.Key.Enter).Handled || vm.Description != expected || saved || vm.IsSuggestionsOpen)
            throw new InvalidOperationException("Edit suggestion Enter saved or failed to fill the draft.");
        Type("Edit suggestion");
        if (!Key(System.Windows.Input.Key.Escape).Handled || vm.IsSuggestionsOpen)
            throw new InvalidOperationException("Edit suggestion Escape did not dismiss the list.");
        Type("");
        Key(System.Windows.Input.Key.Down);
        Key(System.Windows.Input.Key.Down, up: true);
        Wait(vm.SuggestionLoading);
        if (!vm.IsSuggestionsOpen || vm.Suggestions.Count != 5)
            throw new InvalidOperationException("Edit empty Down-arrow did not show five recent entries.");
        Type("Edit suggestion");
        list.UpdateLayout();
        var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        item.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,
            Environment.TickCount, System.Windows.Input.MouseButton.Left)
        { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
        if (vm.Description != (string)item.DataContext || vm.IsSuggestionsOpen || saved)
            throw new InvalidOperationException("Clicking an edit suggestion failed to fill only the draft.");
        dialog.Close();
        Console.WriteLine("Edit suggestion checks passed: automatic matching, popup binding, navigation, Enter without save, Escape, recents, mouse selection.");
    }

    private static void VerifyDurationInputs(MainWindow main, MainViewModel vm)
    {
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Duration input checks"));
        var manual = (TextBox)main.FindName("ManualDurationTextBox");
        var timer = (TextBox)main.FindName("TimerInitialDurationTextBox");
        if (vm.ManualDuration != "" || vm.ManualStartTime != "" || vm.ManualEndTime != "")
            throw new InvalidOperationException("Manual time fields must start empty.");
        foreach (var box in new[] { manual, timer })
        {
            foreach (var (input, expected) in new[] { ("1h 2m 3s", "01:02:03"), ("90m", "01:30:00"), ("1000s", "00:16:40"), ("29", "00:29:00") })
            {
                box.Text = input;
                box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
                if (box.Text != expected) throw new InvalidOperationException($"Focus-loss normalization failed for {input}.");
            }
            box.Text = "1m 2";
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
            if (box.Text != "1m 2" || string.IsNullOrEmpty(box == timer ? vm.TimerError : vm.ManualDurationError))
                throw new InvalidOperationException("Malformed duration was changed or lacked feedback.");
            box.Text = "600s";
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            box.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                source, Environment.TickCount, System.Windows.Input.Key.Enter)
            { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
            if (box.Text != "00:10:00" || vm.IsTimerRunning)
                throw new InvalidOperationException("Enter normalization failed with a blank timer description.");
        }
        vm.ManualDuration = vm.TimerInitialDuration = string.Empty;
        vm.TimerError = vm.ManualDurationError = string.Empty;
        Console.WriteLine("Duration input checks passed: empty fields, focus-loss/Enter formatting, strict errors, clock fallback.");
    }

    private static void VerifyTimerEnter(MainWindow main, MainViewModel vm)
    {
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Timer keyboard checks"));
        void Enter(string control)
        {
            var box = (TextBox)main.FindName(control);
            box.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                source, Environment.TickCount, System.Windows.Input.Key.Enter)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
            });
            if (vm.StartTimerCommand.ExecutionTask is { } task) Wait(task);
        }

        vm.TimerDescription = "   ";
        Enter("TimerDescriptionTextBox");
        Enter("TimerInitialDurationTextBox");
        if (vm.IsTimerRunning) throw new InvalidOperationException("Blank-description Enter started a timer.");

        vm.TimerDescription = "Keyboard timer check";
        Wait(Task.Delay(250));
        vm.TimerSuggestions.Clear();
        vm.TimerSuggestions.Add("Accepted keyboard suggestion");
        vm.TimerSuggestionIndex = 0;
        vm.IsTimerSuggestionsOpen = true;
        Enter("TimerDescriptionTextBox");
        if (vm.IsTimerRunning || vm.TimerDescription != "Accepted keyboard suggestion" || vm.IsTimerSuggestionsOpen)
            throw new InvalidOperationException("Enter did not accept the suggestion without starting.");
        Enter("TimerDescriptionTextBox");
        if (!vm.IsTimerRunning) throw new InvalidOperationException("Second Enter did not start the timer.");
        Wait(vm.StopTimerCommand.ExecuteAsync(null));

        vm.TimerDescription = "Duration keyboard check";
        vm.TimerInitialDuration = "invalid";
        Enter("TimerInitialDurationTextBox");
        if (vm.IsTimerRunning || string.IsNullOrEmpty(vm.TimerError))
            throw new InvalidOperationException("Enter bypassed initial-duration validation.");
        vm.TimerInitialDuration = "1:30";
        Enter("TimerInitialDurationTextBox");
        if (!vm.IsTimerRunning || vm.RunningElapsedText != "00:01:30")
            throw new InvalidOperationException("Duration-field Enter did not start with the supplied duration.");
        Wait(vm.StopTimerCommand.ExecuteAsync(null));
        Console.WriteLine("Timer Enter checks passed: blank input, suggestion selection, second Enter, invalid duration, seeded start.");
    }

    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    private static void VerifyViewRestart(MainWindow main, MainViewModel vm, TimeEntryRepository repository, string database, string output)
    {
        var view = (GridView)((ListView)main.FindName("EntriesListView")).View;
        Wait(vm.SaveEntryQueryPreferencesAsync());
        var baseline = File.ReadAllText(database + ".view.json");
        var description = view.Columns.Single(column => EntryGridLayout.GetColumnKey(column) == "description");
        description.Width = 300;
        Wait(Task.Delay(1500));
        if (File.ReadAllText(database + ".view.json") != baseline) throw new InvalidOperationException("Column resize saved too early.");
        description.Width = 310;
        Wait(Task.Delay(1500));
        if (File.ReadAllText(database + ".view.json") != baseline) throw new InvalidOperationException("Column resize did not reset the debounce.");
        Wait(Task.Delay(1900));
        var resizedPreferences = EntryQueryPreferences.LoadAsync(database + ".view.json"); Wait(resizedPreferences);
        if (resizedPreferences.Result.Columns?.Single(column => column.Key == "description").Width != 310)
            throw new InvalidOperationException("Final resize was not persisted after the idle delay.");
        view.Columns.Move(1, 3);
        vm.IsManualEntryExpanded = false;
        vm.IsFiltersExpanded = true;
        vm.FilterDescription = "Deep";
        vm.FilterStatus = "Completed";
        vm.SelectedEntrySort = TimeEntrySort.Modified;
        vm.SortDescending = false;
        Wait(vm.ApplyEntryFiltersCommand.ExecuteAsync(null));
        vm.FilterDescription = "Review"; // Pending draft must not replace the applied query.
        Wait(Task.Delay(3400)); // Pump the dispatcher; verify automatic saving without close/exit.
        var restored = new MainViewModel(repository, repository, Options.Create(new AppRuntimeOptions()));
        Wait(restored.InitializeAsync(database));
        if (restored.IsManualEntryExpanded || !restored.IsFiltersExpanded || restored.FilterDescription != "Review"
            || restored.FilterStatus != "Completed" || restored.SelectedEntrySort != TimeEntrySort.Modified || restored.SortDescending
            || !restored.HasPendingEntryQuery || restored.Entries.Count != 1 || !restored.Entries[0].Description.Contains("Deep"))
            throw new InvalidOperationException("Filter/manual state did not survive a view-model restart.");
        var reopened = CreateMain(restored);
        Render(reopened, 1100, 760, Path.Combine(output, "main-restored-layout.png"));
        var restoredView = (GridView)((ListView)reopened.FindName("EntriesListView")).View;
        if (EntryGridLayout.GetColumnKey(restoredView.Columns[3]) != "timer"
            || restoredView.Columns.Single(column => EntryGridLayout.GetColumnKey(column) == "description").Width != 310)
            throw new InvalidOperationException("Column order/width did not survive restart.");
        AssertEntryAlignment(reopened);
        Render(reopened, 960, 650, Path.Combine(output, "main-restored-resized.png"));
        if (restoredView.Columns.Single(column => EntryGridLayout.GetColumnKey(column) == "description").Width != 310)
            throw new InvalidOperationException("Resizing overwrote a restored column width.");
        vm.IsManualEntryExpanded = true;
        vm.IsFiltersExpanded = false;
        Wait(vm.ResetEntryFiltersCommand.ExecuteAsync(null));
        Console.WriteLine("Verified automatic saving and restart of columns, widths, applied/pending filters, and Manual entry state.");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void AssertEntryAlignment(MainWindow window)
    {
        var list = (ListView)window.FindName("EntriesListView");
        var view = (GridView)list.View;
        var row = Descendants<GridViewRowPresenter>(list).First();
        var root = (FrameworkElement)window.Content;
        foreach (var column in view.Columns)
        {
            var header = Descendants<GridViewColumnHeader>(list).First(h => h.Column == column);
            var cell = Descendants<ContentPresenter>(row).First(c => c.ContentTemplate == column.CellTemplate);
            double actual, expected;
            if (EntryGridLayout.GetColumnKey(column) == "timer")
            {
                var button = Descendants<Button>(cell).Single();
                actual = button.TranslatePoint(new Point(button.ActualWidth / 2, 0), root).X;
                expected = header.TranslatePoint(new Point(header.ActualWidth / 2, 0), root).X;
            }
            else
            {
                FrameworkElement text = Descendants<TextBox>(cell).Cast<FrameworkElement>().FirstOrDefault()
                    ?? Descendants<TextBlock>(cell).First();
                actual = text.TranslatePoint(text is TextBox box ? box.GetRectFromCharacterIndex(0).TopLeft : new Point(), root).X;
                expected = header.TranslatePoint(new Point(header.Padding.Left, 0), root).X;
            }
            Console.WriteLine($"Column {EntryGridLayout.GetColumnKey(column)} alignment offset: {actual - expected:F2}");
            if (Math.Abs(actual - expected) > 1) throw new InvalidOperationException($"Column {column.Header} is misaligned.");
        }
    }

    private static void AssertTimerButtonAlignment(MainWindow window, string buttonName, string inputName)
    {
        var button = (FrameworkElement)window.FindName(buttonName);
        var input = (FrameworkElement)((FrameworkElement)window.FindName(inputName)).Parent;
        var buttonCenter = button.TranslatePoint(new Point(0, button.ActualHeight / 2), window).Y;
        var inputCenter = input.TranslatePoint(new Point(0, input.ActualHeight / 2), window).Y;
        if (Math.Abs(buttonCenter - inputCenter) > 1)
            throw new InvalidOperationException($"{buttonName} is not centered on the timer input row.");
    }

    private static MainWindow CreateMain(MainViewModel vm)
        => new(new ScreenEventsWindow(), new ImportExportHistoryWindow()) { DataContext = vm };

    private static void Render(Window window, double width, double height, string path)
    {
        var content = (FrameworkElement)window.Content;
        // Window's LayoutTransform does not participate when rendering its content directly.
        var scale = (double)Application.Current.Resources["UiScaleMultiplier"];
        content.LayoutTransform = new ScaleTransform(scale, scale);
        for (var i = 0; i < 3; i++)
        {
            content.Measure(new Size(width, height));
            content.Arrange(new Rect(0, 0, width, height));
            content.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle((Brush)Application.Current.FindResource("CanvasBrush"), null, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        var list = window.FindName("EntriesListView") as ListView;
        if (list is not null)
        {
            var manual = (ScrollViewer)window.FindName("ManualEntryScroll");
            var headers = (ScrollViewer)window.FindName("EntryHeaderScroll");
            var space = (FrameworkElement)window.FindName("ComposerSpace");
            var advanced = (FrameworkElement)window.FindName("AdvancedFiltersPanel");
            Console.WriteLine($"{Path.GetFileName(path)}: list {list.ActualWidth:F0} × {list.ActualHeight:F0}; composer {manual.ActualHeight:F0}/{manual.ExtentHeight:F0}; headers {headers.ActualHeight:F0}/{headers.ExtentHeight:F0}; available {space.ActualHeight:F0}; filters {advanced.Visibility} {advanced.ActualHeight:F0}");
        }
    }

    private sealed class BindingDiagnostics : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
    }
}
