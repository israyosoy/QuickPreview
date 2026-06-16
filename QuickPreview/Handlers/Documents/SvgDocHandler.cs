using System.IO;
using System.Text;
using System.Xml.Linq;

namespace QuickPreview.Handlers.Documents;

public class SvgDocHandler : IDocumentHandler
{
    public Task<DocumentContent> PrepareAsync(string filePath) => Task.Run(() =>
    {
        try
        {
            string raw = File.ReadAllText(filePath, Encoding.UTF8);
            string svg = SanitizeSvg(raw);

            string body = $"<div class='wrap'>{svg}</div>";
            string css = """
                body { display:flex; align-items:center; justify-content:center; min-height:100vh; padding:16px; }
                .wrap { display:flex; align-items:center; justify-content:center; }
                svg { max-width:100%; max-height:80vh; }
                """;

            return new DocumentContent(HtmlBuilder.Wrap(body, css), null);
        }
        catch (Exception ex)
        {
            return new DocumentContent(HtmlBuilder.Error($"No se pudo abrir el SVG.\n{ex.Message}"), null);
        }
    });

    private static string SanitizeSvg(string svg)
    {
        try
        {
            var doc = XDocument.Parse(svg, LoadOptions.PreserveWhitespace);
            if (doc.Root != null)
                SanitizeElement(doc.Root);
            return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            // If XML parsing fails (malformed SVG), return empty placeholder
            return "<svg xmlns='http://www.w3.org/2000/svg'><text y='20' fill='#888'>SVG malformado</text></svg>";
        }
    }

    private static readonly HashSet<string> _blockedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "foreignObject", "use"
    };

    // Attributes that can carry javascript: URIs
    private static readonly HashSet<string> _uriAttrs = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src", "action", "data", "xlink:href"
    };

    private static void SanitizeElement(XElement el)
    {
        foreach (var child in el.Elements().ToList())
        {
            if (_blockedElements.Contains(child.Name.LocalName))
                child.Remove();
            else
                SanitizeElement(child);
        }

        // Remove on* event handlers and javascript: URI attributes
        foreach (var attr in el.Attributes().ToList())
        {
            string localName = attr.Name.LocalName;
            bool isEventHandler = localName.StartsWith("on", StringComparison.OrdinalIgnoreCase);
            bool isJsUri = _uriAttrs.Contains(localName)
                && attr.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);
            if (isEventHandler || isJsUri)
                attr.Remove();
        }
    }
}
