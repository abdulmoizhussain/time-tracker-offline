using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using TimeTracker.App.ViewModels;

namespace TimeTracker.App;

public partial class TrayPopupWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly MainWindow _mainWindow;
    private System.Drawing.Point _trayPoint;
    private bool _isClosing;

    public TrayPopupWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        _viewModel = mainWindow.ViewModel;
        DataContext = _viewModel;
        InitializeComponent();
        Loaded += (_, _) => PositionNearTray();
        SizeChanged += (_, _) => PositionNearTray();
    }

    public void ShowNearTray(System.Drawing.Point trayPoint)
    {
        _trayPoint = trayPoint;
        // Seed the native window on the clicked monitor before WPF measures it.
        var handle = new WindowInteropHelper(this).EnsureHandle();
        SetWindowPos(handle, IntPtr.Zero, trayPoint.X, trayPoint.Y, 0, 0, 0x0015);
        Show();
        PositionNearTray();
    }

    private void PositionNearTray()
    {
        if (!IsLoaded || IsClosed)
        {
            return;
        }

        var workArea = System.Windows.Forms.Screen.FromPoint(_trayPoint).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
        var gap = (int)Math.Ceiling(8 * dpi.DpiScaleX);
        var left = Math.Clamp(_trayPoint.X - width, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var top = Math.Clamp(_trayPoint.Y - height - gap, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        // Screen/Cursor use physical pixels; SetWindowPos avoids mixing them with WPF DIPs.
        SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, left, top, 0, 0, 0x0015);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    public bool IsClosed { get; private set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        IsClosed = true;
    }

    private void SafeClose()
    {
        if (!_isClosing && !IsClosed)
        {
            _isClosing = true;
            Close();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        SafeClose();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        SafeClose();
    }

    private void OnStartStopClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTimer();
    }

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        SafeClose();
        _mainWindow.ShowAndActivate();
    }
}
