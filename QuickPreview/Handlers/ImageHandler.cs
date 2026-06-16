using QuickPreview.Services;
using LruBitmapCache = QuickPreview.Services.BitmapCache;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace QuickPreview.Handlers;

public class ImageHandler : IPreviewHandler
{
    public async Task<UIElement> CreatePreviewAsync(string filePath)
    {
        try
        {
            BitmapSource source = await Task.Run(() => LoadBitmapCached(filePath));

            return new WpfImage
            {
                Source = source,
                Stretch = Stretch.Uniform,
                MaxWidth = 960,
                MaxHeight = 720,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
        }
        catch (NotSupportedException)
        {
            return NoCodecPanel(filePath);
        }
        catch (Exception ex)
        {
            return MakeText($"No se pudo cargar la imagen.\n\n{ex.Message}",
                WpfColor.FromRgb(180, 80, 80));
        }
    }

    // Public so App.xaml.cs can warm the cache for adjacent files
    public static BitmapSource LoadBitmapCached(string filePath)
    {
        var cached = LruBitmapCache.Instance.Get(filePath);
        if (cached != null) return cached;

        var bitmap = LoadBitmap(filePath);
        LruBitmapCache.Instance.Put(filePath, bitmap);
        return bitmap;
    }

    private static BitmapSource LoadBitmap(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return ApplyExifRotation(frame);
    }

    private static BitmapSource ApplyExifRotation(BitmapFrame frame)
    {
        int orientation = GetExifOrientation(frame.Metadata as BitmapMetadata);
        double angle = orientation switch
        {
            3 => 180,
            6 => 90,
            8 => 270,
            _ => 0
        };
        if (angle == 0) return frame;

        var rotated = new TransformedBitmap(frame, new RotateTransform(angle));
        rotated.Freeze();
        return rotated;
    }

    private static int GetExifOrientation(BitmapMetadata? meta)
    {
        if (meta == null) return 1;
        try
        {
            // JPEG stores orientation in /app1/ifd; TIFF/RAW stores it in /ifd
            if (meta.GetQuery("/app1/ifd/{ushort=274}") is ushort v1) return v1;
            if (meta.GetQuery("/ifd/{ushort=274}") is ushort v2) return v2;
        }
        catch { }
        return 1;
    }

    private static UIElement NoCodecPanel(string filePath)
    {
        string ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

        string hint = ext switch
        {
            "HEIC" or "HEIF" =>
                "Instala  \"HEIF Image Extensions\"  desde Microsoft Store\n" +
                "(busca 'heif image extensions microsoft')",
            "ARW" or "SRF" or "SR2" =>
                "Sony RAW → instala  \"Microsoft Raw Image Extension\"  (Microsoft Store)\n" +
                "o el codec oficial de Sony.",
            "CR2" or "CR3" or "CRW" =>
                "Canon RAW → instala el  \"Canon RAW Codec\"  desde support.canon.com\n" +
                "o  \"Microsoft Raw Image Extension\"  (Microsoft Store).",
            "NEF" or "NRW" =>
                "Nikon RAW → instala el  \"Nikon Codec\"  desde downloadcenter.nikonimglib.com\n" +
                "o  \"Microsoft Raw Image Extension\"  (Microsoft Store).",
            _ =>
                "Instala  \"Microsoft Raw Image Extension\"  desde Microsoft Store\n" +
                "para obtener soporte de la mayoría de formatos RAW."
        };

        return MakeText(
            $"Codec no instalado para .{ext}\n\n{hint}",
            WpfColor.FromRgb(160, 140, 60));
    }

    private static TextBlock MakeText(string text, WpfColor color) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(color),
        FontSize = 13,
        FontFamily = new WpfFontFamily("Segoe UI"),
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(32),
        MaxWidth = 500,
        VerticalAlignment = VerticalAlignment.Center
    };
}
