namespace QuickPreview.Handlers.Documents;

public interface IDocumentHandler
{
    // Returns Html content to navigate to, or a FileUrl to open directly (for PDF).
    Task<DocumentContent> PrepareAsync(string filePath);
}

public readonly record struct DocumentContent(string? Html, string? FileUrl);
