namespace QuickPreview.Handlers.Documents;

public class PdfDocHandler : IDocumentHandler
{
    // PDF: navigate WebView2 directly to the file:// URL — Edge renders it natively.
    public Task<DocumentContent> PrepareAsync(string filePath)
    {
        string url = "file:///" + filePath.Replace('\\', '/');
        return Task.FromResult(new DocumentContent(null, url));
    }
}
