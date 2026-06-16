using ClosedXML.Excel;
using System.Text;

namespace QuickPreview.Handlers.Documents;

public class ExcelDocHandler : IDocumentHandler
{
    private const int MaxRows = 500;
    private const int MaxCols = 52;

    public Task<DocumentContent> PrepareAsync(string filePath) => Task.Run(() =>
    {
        try
        {
            using var wb = new XLWorkbook(filePath);
            var sheets = wb.Worksheets.ToList();
            bool multiSheet = sheets.Count > 1;

            var body = new StringBuilder();

            if (multiSheet)
            {
                // Radio inputs must precede tabs and sheets so ~ sibling selector works
                for (int i = 0; i < sheets.Count; i++)
                    body.Append($"<input type='radio' name='sh' id='r{i}'{(i == 0 ? " checked" : "")}>");

                body.Append("<div class='tabs'>");
                for (int i = 0; i < sheets.Count; i++)
                    body.Append($"<label for='r{i}'>{HtmlBuilder.Encode(sheets[i].Name)}</label>");
                body.Append("</div>");
            }

            for (int si = 0; si < sheets.Count; si++)
            {
                var ws = sheets[si];
                body.Append($"<div class='sheet' id='s{si}'>");

                var range = ws.RangeUsed();
                if (range == null)
                {
                    body.Append("<p class='note'>Hoja vacía</p>");
                }
                else
                {
                    int rows = Math.Min(range.RowCount(), MaxRows);
                    int cols = Math.Min(range.ColumnCount(), MaxCols);
                    int r0 = range.FirstRow().RowNumber();
                    int c0 = range.FirstColumn().ColumnNumber();

                    body.Append("<div class='wrap'><table>");
                    for (int r = 0; r < rows; r++)
                    {
                        body.Append("<tr>");
                        for (int c = 0; c < cols; c++)
                        {
                            var cell = ws.Cell(r0 + r, c0 + c);
                            string val = cell.IsEmpty() ? "" : HtmlBuilder.Encode(cell.GetFormattedString());
                            string tag = r == 0 ? "th" : "td";
                            body.Append($"<{tag}>{val}</{tag}>");
                        }
                        body.Append("</tr>");
                    }
                    body.Append("</table></div>");

                    if (range.RowCount() > MaxRows)
                        body.Append($"<p class='note'>Mostrando {MaxRows} de {range.RowCount()} filas.</p>");
                }

                body.Append("</div>");
            }

            // CSS: hide all radio inputs; show/activate via :checked ~ sibling selectors
            var css = new StringBuilder("""
                input[type='radio'] { display:none; }
                .tabs { display:flex; flex-wrap:wrap; gap:4px; margin-bottom:0; }
                .tabs label { background:#2a2a2a; color:#bbb; border:1px solid #3a3a3a; border-bottom:none;
                              padding:5px 14px; cursor:pointer; border-radius:4px 4px 0 0; font-size:12px; }
                .sheet { display:none; border-top:2px solid #3d7fc1; }
                """);

            if (multiSheet)
            {
                for (int i = 0; i < sheets.Count; i++)
                {
                    css.Append($"#r{i}:checked ~ #s{i} {{ display:block; }}\n");
                    css.Append($"#r{i}:checked ~ .tabs label[for='r{i}'] {{ background:#3d7fc1; color:#fff; border-color:#3d7fc1; }}\n");
                }
            }
            else
            {
                css.Append(".sheet { display:block; }\n");
            }

            css.Append("""
                .wrap { overflow-x:auto; }
                table { border-collapse:collapse; width:max-content; min-width:100%; font-size:12px; }
                th { background:#2d2d2d; color:#e0e0e0; font-weight:600; position:sticky; top:0; z-index:1; }
                td,th { border:1px solid #333; padding:4px 12px; white-space:nowrap; }
                tr:nth-child(even) td { background:#222; }
                tr:hover td { background:#2a2a2a; }
                .note { color:#666; font-size:11px; margin:8px 0; padding-left:4px; }
                """);

            return new DocumentContent(HtmlBuilder.Wrap(body.ToString(), css.ToString()), null);
        }
        catch (Exception ex)
        {
            return new DocumentContent(HtmlBuilder.Error($"No se pudo abrir el archivo Excel.\n{ex.Message}"), null);
        }
    });
}
