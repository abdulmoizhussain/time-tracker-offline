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
        vm.TimerDescription = "Building a more comfortable TimeTracker interface";
        Wait(vm.StartTimerCommand.ExecuteAsync(null));
        Render(CreateMain(vm), 1100, 760, Path.Combine(output, "main-running.png"));
        vm.IsFiltersExpanded = true;
        vm.FilterDescription = "work";
        Render(CreateMain(vm), 960, 510, Path.Combine(output, "main-small-filters.png"));
        app.Resources["UiScaleMultiplier"] = 1.5;
        Render(CreateMain(vm), 1100, 760, Path.Combine(output, "main-scaled.png"));
        app.Resources["UiScaleMultiplier"] = 1.0;
        vm.IsFiltersExpanded = false;
        Render(new TrayPopupWindow(main), 340, 240, Path.Combine(output, "tray-running.png"));
        Render(new UsageWindow(), 660, 610, Path.Combine(output, "usage.png"));
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
        if (diagnostics.Errors.Count != 0)
        {
            foreach (var error in diagnostics.Errors.Distinct()) Console.Error.WriteLine(error);
            throw new InvalidOperationException("WPF binding errors detected during preview.");
        }
        Console.WriteLine($"Rendered nine previews to {output}; no WPF binding errors.");
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(database);
        File.Delete(database + "-wal");
        File.Delete(database + "-shm");
        File.Delete(database + ".view.json");
        File.Delete(database + ".view.json.tmp");
    }

    private static void Wait(Task task)
    {
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
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
