using System.IO;
using System.Text;

namespace QuickPreview.Handlers.Documents;

public class TextDocHandler : IDocumentHandler
{
    public Task<DocumentContent> PrepareAsync(string filePath) => Task.Run(() =>
    {
        const int MaxLines = 3000;
        const long MaxBytes = 400_000;

        try
        {
            string text;
            bool truncated = false;
            long size = new FileInfo(filePath).Length;

            if (size > MaxBytes)
            {
                var lines = new List<string>(MaxLines);
                using var sr = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
                while (lines.Count < MaxLines && sr.ReadLine() is string ln)
                    lines.Add(ln);
                truncated = !sr.EndOfStream;
                text = string.Join("\n", lines);
            }
            else
            {
                // detectEncodingFromByteOrderMarks handles UTF-8, UTF-16, UTF-32 BOM;
                // falls back to UTF-8 for files without BOM (correct for most source/XML)
                using var sr = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
                text = sr.ReadToEnd();
            }

            string encoded = HtmlBuilder.Encode(text);
            string notice = truncated
                ? $"<div class='trunc'>Vista previa truncada — mostrando {MaxLines:N0} líneas ({size / 1024} KB total)</div>"
                : string.Empty;

            string body = $"{notice}<pre><code>{encoded}</code></pre>";

            string css = """
                body { padding: 0; }
                .trunc {
                    background: #2b2200; color: #c9a84c;
                    font-size: 11px; padding: 7px 20px;
                    border-bottom: 1px solid #4a3b00;
                    font-family: 'Segoe UI', system-ui, sans-serif;
                }
                pre { margin: 0; padding: 20px 24px; }
                code {
                    font-family: 'Cascadia Code', 'Fira Code', Consolas, 'Courier New', monospace;
                    font-size: 13px;
                    line-height: 1.65;
                    color: #d4d4d4;
                    white-space: pre;
                    display: block;
                    tab-size: 4;
                }
                """;

            return new DocumentContent(HtmlBuilder.Wrap(body, css), null);
        }
        catch (Exception ex)
        {
            return new DocumentContent(HtmlBuilder.Error($"No se pudo abrir el archivo.\n{ex.Message}"), null);
        }
    });
}
