namespace QuickPreview.Handlers.Documents;

public static class HtmlBuilder
{
    // No extraScript parameter: JS is disabled in WebView2 for security.
    // Tab navigation and similar interactions use pure CSS.
    public static string Wrap(string bodyHtml, string? extraCss = null)
    {
        string css = extraCss ?? string.Empty;
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy"
                  content="default-src 'none'; style-src 'unsafe-inline'; img-src data: blob:;">
            <style>
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
                background: #1e1e1e;
                color: #d4d4d4;
                font-family: 'Segoe UI', system-ui, sans-serif;
                font-size: 14px;
                line-height: 1.65;
                padding: 28px 32px;
            }
            ::-webkit-scrollbar { width: 8px; height: 8px; }
            ::-webkit-scrollbar-track { background: #1e1e1e; }
            ::-webkit-scrollbar-thumb { background: #424242; border-radius: 4px; }
            a { color: #4ec9b0; pointer-events: none; cursor: default; }
            {{css}}
            </style>
            </head>
            <body>
            {{bodyHtml}}
            </body>
            </html>
            """;
    }

    public static string Encode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);

    public static string Error(string message) =>
        Wrap($"<p style='color:#f48771;padding:16px'>{Encode(message)}</p>");
}
