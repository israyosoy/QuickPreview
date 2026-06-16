using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;

namespace QuickPreview.Handlers.Documents;

public class PowerPointDocHandler : IDocumentHandler
{
    public Task<DocumentContent> PrepareAsync(string filePath) => Task.Run(() =>
    {
        try
        {
            using var pres = PresentationDocument.Open(filePath, false);
            var presPart = pres.PresentationPart;
            if (presPart == null)
                return new DocumentContent(HtmlBuilder.Error("No se pudo leer la presentación."), null);

            var presentation = presPart.Presentation;
            var slideIds = presentation?.SlideIdList?
                .Elements<SlideId>().ToList() ?? [];

            int total = slideIds.Count;
            var body = new StringBuilder();
            body.Append($"<p class='deck-info'>{total} diapositiva{(total != 1 ? "s" : "")}</p>");
            body.Append("<div class='deck'>");

            int idx = 0;
            foreach (var sid in slideIds)
            {
                idx++;
                var relId = sid.RelationshipId?.Value;
                if (relId == null) continue;

                var slidePart = pres.PresentationPart!.GetPartById(relId) as SlidePart;
                if (slidePart == null) continue;

                body.Append("<div class='slide'>");
                body.Append($"<span class='num'>{idx} / {total}</span>");

                bool firstParagraph = true;
                foreach (var para in slidePart.Slide!.Descendants<A.Paragraph>())
                {
                    string text = string.Concat(para.Descendants<A.Text>().Select(t => t.Text ?? "")).Trim();
                    if (string.IsNullOrEmpty(text)) continue;

                    if (firstParagraph)
                    {
                        body.Append($"<h2 class='title'>{HtmlBuilder.Encode(text)}</h2>");
                        firstParagraph = false;
                    }
                    else
                    {
                        body.Append($"<p class='line'>{HtmlBuilder.Encode(text)}</p>");
                    }
                }

                if (firstParagraph)
                    body.Append("<p class='line empty'>(diapositiva sin texto)</p>");

                body.Append("</div>");
            }

            body.Append("</div>");

            string css = """
                .deck-info { color:#555; font-size:11px; margin-bottom:16px; font-family:Consolas; }
                .deck { display:flex; flex-direction:column; gap:12px; }
                .slide { background:#252525; border:1px solid #333; border-radius:6px; padding:20px 24px; }
                .num { color:#555; font-size:10px; display:block; margin-bottom:10px; font-family:Consolas; }
                .title { font-size:17px; color:#e2e2e2; margin-bottom:10px; }
                .line { color:#aaa; font-size:13px; margin-bottom:5px; }
                .line.empty { color:#555; font-style:italic; }
                """;

            return new DocumentContent(HtmlBuilder.Wrap(body.ToString(), css), null);
        }
        catch (Exception ex)
        {
            return new DocumentContent(HtmlBuilder.Error($"No se pudo abrir la presentación.\n{ex.Message}"), null);
        }
    });
}
