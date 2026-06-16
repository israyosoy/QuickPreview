using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace QuickPreview.Handlers;

public class FontHandler : IPreviewHandler
{
    public async Task<UIElement> CreatePreviewAsync(string filePath)
    {
        // WOFF/WOFF2 are web-only formats; WPF's GlyphTypeface cannot load them
        string inputExt = Path.GetExtension(filePath).ToLowerInvariant();
        if (inputExt is ".woff" or ".woff2")
            return new TextBlock
            {
                Text = "Formato web (WOFF/WOFF2): no compatible con el visor de fuentes de Windows.\n\n" +
                       "Este formato es exclusivo para navegadores web.\n" +
                       "Instala la fuente en formato TTF u OTF para previsualizarla.",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(160, 140, 60)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(32),
                MaxWidth = 480
            };

        try
        {
            // Read font metadata on background thread (GlyphTypeface is thread-safe)
            var (familyName, glyphCount, fileSize) = await Task.Run(() =>
            {
                var gt = new GlyphTypeface(new Uri("file:///" + filePath.Replace('\\', '/')));
                var culture = CultureInfo.GetCultureInfo("en-US");
                string name = gt.FamilyNames.TryGetValue(culture, out string? n) ? n
                    : gt.FamilyNames.Values.FirstOrDefault() ?? Path.GetFileNameWithoutExtension(filePath);
                return (name, gt.GlyphCount, new FileInfo(filePath).Length);
            });

            // Create FontFamily pointing at the font file directory
            string dir = (Path.GetDirectoryName(filePath) ?? "") + Path.DirectorySeparatorChar;
            var baseUri = new Uri(dir.Replace('\\', '/').TrimEnd('/') + "/", UriKind.Absolute);
            var ff = new WpfFontFamily(baseUri, "./#" + familyName);

            // Build specimen panel on UI thread (back here after await)
            var panel = new StackPanel
            {
                Orientation = WpfOrientation.Vertical,
                Margin = new Thickness(32),
                MaxWidth = 860
            };

            // Font name header
            panel.Children.Add(MakeText(familyName, 28, WpfColor.FromRgb(220, 220, 220), ff, FontWeights.Bold));
            panel.Children.Add(MakeSeparator());

            // Meta info
            string extLabel = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            panel.Children.Add(MakeText(
                $"{extLabel}  ·  {glyphCount:N0} glifos  ·  {fileSize / 1024.0:F1} KB",
                11, WpfColor.FromRgb(100, 100, 100), new WpfFontFamily("Consolas"), FontWeights.Normal));
            panel.Children.Add(new Border { Height = 20 });

            // Large Aa specimen
            panel.Children.Add(MakeText("Aa", 80, WpfColor.FromRgb(200, 200, 200), ff, FontWeights.Normal));
            panel.Children.Add(new Border { Height = 8 });

            // Alphabet rows
            panel.Children.Add(MakeText("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 18, WpfColor.FromRgb(180, 180, 180), ff, FontWeights.Normal));
            panel.Children.Add(MakeText("abcdefghijklmnopqrstuvwxyz", 18, WpfColor.FromRgb(160, 160, 160), ff, FontWeights.Normal));
            panel.Children.Add(MakeText("0123456789  !@#$%&*()+-=[]{}|;':\",./<>?", 16, WpfColor.FromRgb(130, 130, 130), ff, FontWeights.Normal));
            panel.Children.Add(new Border { Height = 16 });

            // Pangram at various sizes
            const string pangram = "The quick brown fox jumps over the lazy dog";
            foreach ((int size, WpfColor color) in new[]
            {
                (28, WpfColor.FromRgb(200, 200, 200)),
                (20, WpfColor.FromRgb(170, 170, 170)),
                (14, WpfColor.FromRgb(140, 140, 140)),
            })
            {
                panel.Children.Add(MakeText(pangram, size, color, ff, FontWeights.Normal));
            }

            return panel;
        }
        catch (Exception ex)
        {
            return new TextBlock
            {
                Text = $"No se pudo cargar la fuente.\n\n{ex.Message}",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(180, 80, 80)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(32),
                MaxWidth = 480
            };
        }
    }

    private static TextBlock MakeText(string text, double size, WpfColor color, WpfFontFamily ff, FontWeight weight) => new()
    {
        Text = text,
        FontSize = size,
        FontFamily = ff,
        FontWeight = weight,
        Foreground = new SolidColorBrush(color),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static Border MakeSeparator() => new()
    {
        Height = 1,
        Background = new SolidColorBrush(WpfColor.FromRgb(50, 50, 50)),
        Margin = new Thickness(0, 8, 0, 12)
    };
}
