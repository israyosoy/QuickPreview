using System.IO;

namespace QuickPreview.Handlers.Documents;

// Friendly explanation for formats we deliberately don't render (complex binary
// layouts needing dedicated parsers/SDKs we don't ship).
public class UnsupportedFormatDocHandler : IDocumentHandler
{
    public Task<DocumentContent> PrepareAsync(string filePath)
    {
        string ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        string body = $"""
            <div class='wrap'>
                <div class='ext'>{ext}</div>
                <p class='title'>Formato Adobe no compatible con la vista previa</p>
                <p class='desc'>
                    Los archivos {ext} usan un formato binario propietario de Adobe
                    que requiere Photoshop, Illustrator o InDesign para procesarse.
                </p>
                <p class='hint'>Usa el botón ↗ de la barra de título para abrirlo con su aplicación predeterminada.</p>
            </div>
            """;

        string css = """
            body { display: flex; align-items: center; justify-content: center; height: 100vh; }
            .wrap { text-align: center; max-width: 420px; padding: 32px; }
            .ext {
                display: inline-block;
                background: #3a2d52; color: #b794f6;
                font-family: Consolas, monospace; font-weight: 700; font-size: 13px;
                padding: 4px 14px; border-radius: 6px; margin-bottom: 18px; letter-spacing: 0.5px;
            }
            .title { font-size: 16px; color: #e6e6e6; font-weight: 600; margin-bottom: 10px; }
            .desc { font-size: 13px; color: #9a9a9a; line-height: 1.6; margin-bottom: 16px; }
            .hint { font-size: 12px; color: #6e9fd6; }
            """;

        return Task.FromResult(new DocumentContent(HtmlBuilder.Wrap(body, css), null));
    }
}
