using System.IO;
using System.IO.Compression;
using System.Text;

namespace QuickPreview.Handlers.Documents;

public class ZipDocHandler : IDocumentHandler
{
    public Task<DocumentContent> PrepareAsync(string filePath) => Task.Run(() =>
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var entries = zip.Entries
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            long totalUncompressed = entries.Sum(e => e.Length);
            long totalCompressed   = entries.Sum(e => e.CompressedLength);

            var sb = new StringBuilder();
            sb.Append($"<p class='info'>{entries.Count} elemento{(entries.Count != 1 ? "s" : "")}");
            if (totalUncompressed > 0)
                sb.Append($"  ·  {Fmt(totalUncompressed)} sin comprimir  ·  {Fmt(totalCompressed)} comprimido");
            sb.Append("</p>");

            sb.Append("<table><tr><th>Nombre</th><th>Tamaño</th><th>Modificado</th></tr>");
            foreach (var e in entries)
            {
                bool isDir = e.FullName.EndsWith('/');
                string icon = isDir ? "📁" : FileIcon(e.Name);
                string name = HtmlBuilder.Encode(e.FullName);
                string size = isDir ? "" : Fmt(e.Length);
                string date = e.LastWriteTime.ToString("dd/MM/yyyy  HH:mm");
                sb.Append($"<tr><td class='n'>{icon} {name}</td><td class='r'>{size}</td><td class='r'>{date}</td></tr>");
            }
            sb.Append("</table>");

            string css = """
                .info { color:#666; font-size:11px; margin-bottom:14px; font-family:Consolas; }
                table { width:100%; border-collapse:collapse; font-size:12px; }
                th { background:#252525; color:#aaa; text-align:left; padding:6px 10px; border-bottom:1px solid #333; position:sticky; top:0; }
                td { padding:4px 10px; border-bottom:1px solid #222; }
                .n { color:#d4d4d4; font-family:Consolas; }
                .r { text-align:right; color:#666; font-family:Consolas; white-space:nowrap; }
                tr:hover td { background:#252525; }
                """;

            return new DocumentContent(HtmlBuilder.Wrap(sb.ToString(), css), null);
        }
        catch (Exception ex)
        {
            return new DocumentContent(HtmlBuilder.Error($"No se pudo abrir el archivo.\n{ex.Message}"), null);
        }
    });

    private static string Fmt(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    private static string FileIcon(string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => "🖼",
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" => "🎬",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" => "🎵",
            ".pdf" => "📄",
            ".docx" or ".doc" => "📝",
            ".xlsx" or ".xls" => "📊",
            ".pptx" or ".ppt" => "📋",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "📦",
            ".exe" or ".msi" => "⚙",
            ".txt" or ".md" or ".log" => "📃",
            _ => "📄"
        };
    }
}
