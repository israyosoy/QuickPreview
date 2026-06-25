using QuickPreview.Handlers;
using QuickPreview.Services;
using LruBitmapCache = QuickPreview.Services.BitmapCache;
using QuickPreview.Windows;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace QuickPreview;

public partial class App : System.Windows.Application
{
    private NotifyIcon _trayIcon = null!;
    private GlobalKeyboardHook _keyboardHook = null!;
    private Window? _currentPreview;
    private string? _currentFilePath;
    private volatile bool _hasPreview;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch all unhandled exceptions so the app never vanishes silently
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            ShowFatalError(ex.ExceptionObject?.ToString() ?? "Error desconocido");
        DispatcherUnhandledException += (_, ex) =>
        {
            ex.Handled = true;
            ShowFatalError(ex.Exception.Message);
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            ex.SetObserved();
            // Background task errors are silent — only log, don't interrupt the user
        };

        InitializeTrayIcon();
        _keyboardHook = new GlobalKeyboardHook();
        _keyboardHook.IsPreviewOpen = () => _hasPreview;
        _keyboardHook.SpacePressed += () => Dispatcher.BeginInvoke(OnSpacePressed);
        _keyboardHook.EscapePressed += () => Dispatcher.BeginInvoke(OnEscapePressed);
        _keyboardHook.PrevFileRequested += () => Dispatcher.BeginInvoke(() => NavigatePreview(-1));
        _keyboardHook.NextFileRequested += () => Dispatcher.BeginInvoke(() => NavigatePreview(+1));
        _keyboardHook.FullscreenToggleRequested += () => Dispatcher.BeginInvoke(OnFullscreenToggle);
    }

    // ── Tray icon ─────────────────────────────────────────────────────────────

    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "QuickPreview — Presiona Espacio en Explorer para previsualizar",
            Visible = true,
            Icon = CreateTrayIcon()
        };

        var menu = new ContextMenuStrip();

        var autoStartItem = new ToolStripMenuItem("Iniciar con Windows")
        {
            CheckOnClick = true,
            Checked = IsAutoStartEnabled()
        };
        autoStartItem.CheckedChanged += (_, _) => SetAutoStart(autoStartItem.Checked);
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Acerca de QuickPreview...", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Shutdown());
        _trayIcon.ContextMenuStrip = menu;
    }

    private static void ShowAbout()
    {
        string ver = System.Reflection.Assembly.GetExecutingAssembly()
                         .GetName().Version?.ToString(3) ?? "1.0.0";
        System.Windows.MessageBox.Show(
            $"QuickPreview  v{ver}\n\n" +
            "Previsualiza cualquier archivo al instante\n" +
            "con solo presionar Espacio en el Explorador.\n\n" +
            "Teclas:\n" +
            "  Espacio / Esc — abrir · cerrar\n" +
            "  ← →            — navegar entre archivos\n" +
            "  F              — pantalla completa\n" +
            "  ↗ (barra)      — abrir con app predeterminada\n\n" +
            "Código abierto · Licencia MIT · Uso libre\n" +
            "github.com/QuickPreview/QuickPreview",
            "QuickPreview",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private static void ShowFatalError(string message)
    {
        System.Windows.MessageBox.Show(
            $"QuickPreview encontró un error inesperado:\n\n{message}\n\n" +
            "El preview se cerrará pero la aplicación seguirá activa en la bandeja.",
            "QuickPreview — Error",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var bgBrush = new SolidBrush(Color.FromArgb(60, 130, 220));
        g.FillEllipse(bgBrush, 1, 1, 30, 30);
        using var font = new Font("Segoe UI", 14, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("Q", font, textBrush, new RectangleF(0, 0, 32, 32), sf);
        return Icon.FromHandle(bmp.GetHicon());
    }

    // ── Auto-start ───────────────────────────────────────────────────────────
    // Registry HKCU\Run is virtualized inside MSIX packages and never read by
    // Windows at startup. The Startup folder is not virtualized and works for
    // both packaged and unpackaged scenarios.

    private static string StartupLnkPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "QuickPreview.lnk");

    private static bool IsAutoStartEnabled() => File.Exists(StartupLnkPath);

    private static void SetAutoStart(bool enabled)
    {
        string lnk = StartupLnkPath;
        if (!enabled)
        {
            try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
            return;
        }

        string? exe = Environment.ProcessPath
                     ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe)) return;

        try
        {
            // WScript.Shell creates a proper .lnk shortcut without extra dependencies
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t == null) return;
            dynamic wsh = Activator.CreateInstance(t)!;
            dynamic sc  = wsh.CreateShortcut(lnk);
            sc.TargetPath       = exe;
            sc.WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty;
            sc.Description      = "QuickPreview — Instant File Preview";
            sc.Save();
        }
        catch { }
    }

    // ── Keyboard events ──────────────────────────────────────────────────────

    private void OnSpacePressed()
    {
        if (_hasPreview) { CloseCurrentPreview(); return; }

        string? filePath = ExplorerFileDetector.GetSelectedFilePath();
        if (filePath == null || !File.Exists(filePath)) return;

        OpenPreview(filePath);
    }

    private void OnEscapePressed() => CloseCurrentPreview();

    private void OnFullscreenToggle()
    {
        if (_currentPreview is PreviewWindow pw) pw.ToggleFullscreen();
    }

    private void NavigatePreview(int delta)
    {
        if (_currentFilePath == null) return;

        string dir = Path.GetDirectoryName(_currentFilePath) ?? "";
        if (!Directory.Exists(dir)) return;

        var files = Directory.EnumerateFiles(dir)
            .Where(f => FileTypeRouter.GetImageVideoHandler(f) != null
                     || FileTypeRouter.GetDocumentHandler(f) != null)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        int idx = files.FindIndex(f => string.Equals(f, _currentFilePath, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        int next = idx + delta;
        if (next < 0 || next >= files.Count) return;

        string nextFile = files[next];
        _currentFilePath = nextFile;

        // Reuse the current window type instead of closing and reopening (no flash)
        var imageHandler = FileTypeRouter.GetImageVideoHandler(nextFile);
        if (imageHandler != null)
        {
            if (_currentPreview is PreviewWindow pw)
                _ = pw.NavigateAsync(nextFile, imageHandler);
            else
                OpenPreview(nextFile);
            PreloadAdjacentImages(nextFile);
            return;
        }

        var docHandler = FileTypeRouter.GetDocumentHandler(nextFile);
        if (docHandler != null)
        {
            if (_currentPreview is DocumentPreviewWindow dpw)
                _ = dpw.NavigateAsync(nextFile, docHandler);
            else
                OpenPreview(nextFile);
        }
    }

    // ── Preview lifecycle ─────────────────────────────────────────────────────

    private void OpenPreview(string filePath)
    {
        var imageHandler = FileTypeRouter.GetImageVideoHandler(filePath);
        if (imageHandler != null)
        {
            var win = new PreviewWindow(filePath, imageHandler);
            RegisterPreview(win, filePath);
            win.Show();
            return;
        }

        var docHandler = FileTypeRouter.GetDocumentHandler(filePath);
        if (docHandler != null)
        {
            var win = new DocumentPreviewWindow(filePath, docHandler);
            RegisterPreview(win, filePath);
            win.Show();
        }
    }

    private void RegisterPreview(Window win, string filePath)
    {
        CloseCurrentPreview();
        _currentPreview = win;
        _currentFilePath = filePath;
        _hasPreview = true;
        win.Closed += (_, _) =>
        {
            _currentPreview = null;
            _currentFilePath = null;
            _hasPreview = false;
        };
        PreloadAdjacentImages(filePath);
    }

    // Warm the bitmap cache for the 2 files before and after the current one.
    // Runs entirely on background threads; errors are silently ignored.
    private static void PreloadAdjacentImages(string currentFile)
    {
        string dir = Path.GetDirectoryName(currentFile) ?? "";
        if (!Directory.Exists(dir)) return;

        var files = Directory.EnumerateFiles(dir)
            .Where(f => FileTypeRouter.GetImageVideoHandler(f) is ImageHandler)
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        int idx = files.FindIndex(f => string.Equals(f, currentFile, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        foreach (int offset in new[] { 1, -1, 2, -2 })
        {
            int ni = idx + offset;
            if (ni < 0 || ni >= files.Count) continue;
            string path = files[ni];
            if (LruBitmapCache.Instance.Contains(path)) continue;
            Task.Run(() =>
            {
                try { ImageHandler.LoadBitmapCached(path); }
                catch { /* file disappeared or codec missing — ignore */ }
            });
        }
    }

    private void CloseCurrentPreview()
    {
        if (_currentPreview == null) return;
        _currentPreview.Close();
        _currentPreview = null;
        _currentFilePath = null;
        _hasPreview = false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _keyboardHook.Dispose();
        base.OnExit(e);
    }
}
