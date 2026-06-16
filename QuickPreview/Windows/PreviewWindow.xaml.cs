using QuickPreview.Handlers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfImage = System.Windows.Controls.Image;
using WpfCursors = System.Windows.Input.Cursors;

namespace QuickPreview.Windows;

public partial class PreviewWindow : Window
{
    // Drag state
    private System.Windows.Point? _dragStart;
    private double _dragOriginLeft, _dragOriginTop;

    // Zoom / pan state
    private ScaleTransform? _zoomTransform;
    private double _zoomScale = 1.0;
    private System.Windows.Point? _panOrigin;
    private double _panScrollH, _panScrollV;

    // Fullscreen state
    private bool _isFullscreen;
    private double _savedLeft, _savedTop, _savedWidth, _savedHeight;

    private string _currentFilePath = string.Empty;

    public PreviewWindow(string filePath, IPreviewHandler handler)
    {
        InitializeComponent();
        _currentFilePath = filePath;
        SetFileInfo(filePath);
        Loaded += async (_, _) => await LoadContentAsync(filePath, handler);
    }

    // Called by App.xaml.cs to navigate in-place (same window, no flash)
    public async Task NavigateAsync(string filePath, IPreviewHandler handler)
    {
        _currentFilePath = filePath;
        if (_isFullscreen) ExitFullscreen();
        ResetZoom();
        ContentScroll.Visibility = Visibility.Collapsed;
        PreviewContent.Content = null;
        LoadingPane.Visibility = Visibility.Visible;
        LoadingText.Text = "Cargando...";
        SetFileInfo(filePath);
        await LoadContentAsync(filePath, handler);
    }

    private void SetFileInfo(string filePath)
    {
        TitleText.Text = Path.GetFileName(filePath);
        var info = new FileInfo(filePath);
        FileInfoText.Text =
            $"{FormatSize(info.Length)}  ·  {info.LastWriteTime:dd/MM/yyyy  HH:mm}  ·  " +
            $"{Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()}";
    }

    private async Task LoadContentAsync(string filePath, IPreviewHandler handler)
    {
        try
        {
            var content = await handler.CreatePreviewAsync(filePath);
            PreviewContent.Content = content;

            // Wire up zoom + show pixel dimensions for images
            if (content is WpfImage img && img.Source is BitmapSource bmp && bmp.PixelWidth > 0)
            {
                SetupImageZoom(img);
                FileInfoText.Text = $"{bmp.PixelWidth} × {bmp.PixelHeight}  ·  " + FileInfoText.Text;
            }

            LoadingPane.Visibility = Visibility.Collapsed;
            ContentScroll.Visibility = Visibility.Visible;

            // Re-center on the monitor where the cursor currently is
            CenterOnCurrentMonitor();
        }
        catch (Exception ex)
        {
            LoadingText.Text = $"Error: {ex.Message}";
        }
    }

    // ── Zoom / Pan ───────────────────────────────────────────────────────────

    private void SetupImageZoom(WpfImage image)
    {
        _zoomTransform = new ScaleTransform(1, 1);
        image.LayoutTransform = _zoomTransform;

        // Mouse wheel → zoom
        ContentScroll.PreviewMouseWheel += (_, e) =>
        {
            _zoomScale = e.Delta > 0
                ? Math.Min(_zoomScale * 1.12, 8.0)
                : Math.Max(_zoomScale / 1.12, 0.5);
            _zoomTransform.ScaleX = _zoomScale;
            _zoomTransform.ScaleY = _zoomScale;
            image.Cursor = _zoomScale > 1.0 ? WpfCursors.SizeAll : WpfCursors.Arrow;
            e.Handled = true;
        };

        // Left-drag to pan when zoomed; double-click to reset
        image.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2) { ResetZoom(); e.Handled = true; return; }
            if (_zoomScale <= 1.0) return;
            _panOrigin = e.GetPosition(ContentScroll);
            _panScrollH = ContentScroll.HorizontalOffset;
            _panScrollV = ContentScroll.VerticalOffset;
            image.CaptureMouse();
            e.Handled = true;
        };

        image.MouseMove += (_, e) =>
        {
            if (_panOrigin == null || e.LeftButton != MouseButtonState.Pressed) return;
            var cur = e.GetPosition(ContentScroll);
            ContentScroll.ScrollToHorizontalOffset(_panScrollH + (_panOrigin.Value.X - cur.X));
            ContentScroll.ScrollToVerticalOffset(_panScrollV + (_panOrigin.Value.Y - cur.Y));
        };

        image.MouseUp += (_, e) =>
        {
            _panOrigin = null;
            image.ReleaseMouseCapture();
        };

    }

    private void ResetZoom()
    {
        _zoomScale = 1.0;
        if (_zoomTransform != null) { _zoomTransform.ScaleX = 1; _zoomTransform.ScaleY = 1; }
        ContentScroll.ScrollToTop();
        ContentScroll.ScrollToLeftEnd();
        if (PreviewContent.Content is WpfImage img) img.Cursor = WpfCursors.Arrow;
    }

    // ── Multi-monitor centering ──────────────────────────────────────────────

    private void CenterOnCurrentMonitor()
    {
        UpdateLayout();
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var src = PresentationSource.FromVisual(this);
        double dpiX = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = src?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        double workLeft   = screen.WorkingArea.Left   / dpiX;
        double workTop    = screen.WorkingArea.Top    / dpiY;
        double workWidth  = screen.WorkingArea.Width  / dpiX;
        double workHeight = screen.WorkingArea.Height / dpiY;
        Left = workLeft + (workWidth  - ActualWidth)  / 2;
        Top  = workTop  + (workHeight - ActualHeight) / 2;
    }

    // ── Fullscreen toggle ────────────────────────────────────────────────────

    public void ToggleFullscreen()
    {
        if (!_isFullscreen) EnterFullscreen();
        else ExitFullscreen();
    }

    private void EnterFullscreen()
    {
        _savedLeft   = Left;
        _savedTop    = Top;
        _savedWidth  = ActualWidth;
        _savedHeight = ActualHeight;

        SizeToContent = SizeToContent.Manual;
        ContentRow.Height = new GridLength(1, GridUnitType.Star);
        ContentScroll.MaxWidth  = double.PositiveInfinity;
        ContentScroll.MaxHeight = double.PositiveInfinity;
        if (PreviewContent.Content is WpfImage img)
        {
            img.MaxWidth  = double.PositiveInfinity;
            img.MaxHeight = double.PositiveInfinity;
        }

        WindowState = WindowState.Maximized;
        RootBorder.CornerRadius = new CornerRadius(0);
        ((DropShadowEffect)RootBorder.Effect).Opacity = 0;
        _isFullscreen = true;
    }

    private void ExitFullscreen()
    {
        WindowState = WindowState.Normal;

        ContentRow.Height = GridLength.Auto;
        ContentScroll.MaxWidth  = 960;
        ContentScroll.MaxHeight = 720;
        if (PreviewContent.Content is WpfImage img)
        {
            img.MaxWidth  = 960;
            img.MaxHeight = 720;
        }

        Width  = _savedWidth;
        Height = _savedHeight;
        Left   = _savedLeft;
        Top    = _savedTop;

        RootBorder.CornerRadius = new CornerRadius(10);
        ((DropShadowEffect)RootBorder.Effect).Opacity = 0.6;
        SizeToContent = SizeToContent.WidthAndHeight;
        _isFullscreen = false;
    }

    // ── Open with default app ────────────────────────────────────────────────

    private void OpenWithBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_currentFilePath) { UseShellExecute = true });
        }
        catch { }
    }

    // ── Resize via WndProc (AllowsTransparency=True needs manual NCHITTEST) ──

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        (PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WndProc);
    }

    private const int WM_NCHITTEST = 0x0084;
    private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12;
    private const int HTTOPLEFT = 13, HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            int x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
            int y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
            GetWindowRect(hwnd, out var r);
            const int b = 8;
            bool l = x - r.Left < b, ri = r.Right - x < b;
            bool t = y - r.Top < b, bo = r.Bottom - y < b;
            if (t && l)  { handled = true; return (IntPtr)HTTOPLEFT; }
            if (t && ri) { handled = true; return (IntPtr)HTTOPRIGHT; }
            if (bo && l) { handled = true; return (IntPtr)HTBOTTOMLEFT; }
            if (bo && ri){ handled = true; return (IntPtr)HTBOTTOMRIGHT; }
            if (l)  { handled = true; return (IntPtr)HTLEFT; }
            if (ri) { handled = true; return (IntPtr)HTRIGHT; }
            if (t)  { handled = true; return (IntPtr)HTTOP; }
            if (bo) { handled = true; return (IntPtr)HTBOTTOM; }
        }
        return IntPtr.Zero;
    }

    // ── Title bar drag ───────────────────────────────────────────────────────

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _dragStart = e.GetPosition(this);
        _dragOriginLeft = Left; _dragOriginTop = Top;
        ((IInputElement)sender).CaptureMouse();
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed) return;
        var cur = e.GetPosition(this);
        Left = _dragOriginLeft + (cur.X - _dragStart.Value.X);
        Top  = _dragOriginTop  + (cur.Y - _dragStart.Value.Y);
    }

    private void TitleBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _dragStart = null;
        ((IInputElement)sender).ReleaseMouseCapture();
    }

    private static string FormatSize(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        _ => $"{b / (1024.0 * 1024):F1} MB"
    };
}
