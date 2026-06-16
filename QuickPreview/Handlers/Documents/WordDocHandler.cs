using Mammoth;
using System.IO;

namespace QuickPreview.Handlers.Documents;

public class WordDocHandler : IDocumentHandler
{
    public Task<DocumentContent> PrepareAsync(string filePath) => Task.Run(() =>
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var result = new DocumentConverter().ConvertToHtml(stream);

            string css = """
                h1,h2,h3,h4,h5,h6 { color: #e8e8e8; margin: 20px 0 8px; font-weight: 600; }
                h1 { font-size: 22px; } h2 { font-size: 18px; } h3 { font-size: 16px; }
                p  { margin-bottom: 10px; }
                ul,ol { padding-left: 28px; margin-bottom: 10px; }
                li { margin-bottom: 4px; }
                table { border-collapse: collapse; width: 100%; margin: 16px 0; }
                td,th { border: 1px solid #3a3a3a; padding: 6px 12px; }
                th { background: #2d2d2d; color: #e0e0e0; }
                img { max-width: 100%; border-radius: 4px; }
                blockquote { border-left: 3px solid #3d7fc1; padding-left: 16px; color: #999; margin: 12px 0; }
                code { background: #252525; padding: 2px 6px; border-radius: 3px; font-family: Consolas; font-size: 13px; }
                """;

            return new DocumentContent(HtmlBuilder.Wrap(result.Value, css), null);
        }
        catch (Exception ex)
        {
            return new DocumentContent(HtmlBuilder.Error($"No se pudo abrir el archivo Word.\n{ex.Message}"), null);
        }
    });
}
