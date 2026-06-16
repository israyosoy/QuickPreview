using QuickPreview.Handlers.Documents;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace QuickPreview.Windows;

public partial class DocumentPreviewWindow : Window
{
    private string _filePath;
    private IDocumentHandler _handler;

    public DocumentPreviewWindow(string filePath, IDocumentHandler handler)
    {
        InitializeComponent();
        _filePath = filePath;
        _handler = handler;
        SetFileInfo(filePath);
    }

    // Called by App.xaml.cs for in-place navigation (same window, no flash)
    public async Task NavigateAsync(string filePath, IDocumentHandler handler)
    {
        _filePath = filePath;
        _handler = handler;
        WebView.Visibility = Visibility.Collapsed;
        LoadingPane.Visibility = Visibility.Visible;
        LoadingText.Text = "Cargando...";
        SetFileInfo(filePath);
        await LoadDocumentAsync();
    }

    private void SetFileInfo(string filePath)
    {
        TitleText.Text = Path.GetFileName(filePath);
        var info = new FileInfo(filePath);
        FileInfoText.Text =
            $"{FormatSize(info.Length)}  ·  {info.LastWriteTime:dd/MM/yyyy  HH:mm}  ·  " +
            $"{Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()}";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();

            WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(30, 30, 30);

            var settings = WebView.CoreWebView2.Settings;
            settings.IsScriptEnabled = false;
            settings.IsWebMessageEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;

            // Allow only local content: about: (NavigateToString) and file: (PDF)
            // Block all outbound http/https/ftp navigation
            WebView.CoreWebView2.NavigationStarting += (_, e) =>
            {
                string uri = e.Uri ?? "";
                bool allowed = uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
                            || uri.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
                if (!allowed)
                    e.Cancel = true;
            };

            // Block any file downloads
            WebView.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;

            // Block popup windows
            WebView.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;

            WebView.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                WebView.Visibility = Visibility.Visible;
                LoadingPane.Visibility = Visibility.Collapsed;
            };

            await LoadDocumentAsync();
        }
        catch (Exception ex)
        {
            LoadingText.Text =
                $"No se puede inicializar el visor.\n" +
                $"Asegúrate de que Microsoft Edge esté instalado.\n\n{ex.Message}";
        }
    }

    private async Task LoadDocumentAsync()
    {
        try
        {
            var content = await _handler.PrepareAsync(_filePath);
            if (content.FileUrl != null)
                WebView.CoreWebView2.Navigate(content.FileUrl);
            else if (content.Html != null)
                WebView.NavigateToString(content.Html);
        }
        catch (Exception ex)
        {
            LoadingText.Text = $"Error al cargar.\n{ex.Message}";
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatSize(long b) => b switch
    {
        < 1024 => $"{b} B",
        < 1024 * 1024 => $"{b / 1024.0:F1} KB",
        _ => $"{b / (1024.0 * 1024):F1} MB"
    };
}
