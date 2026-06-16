using System.Windows;

namespace QuickPreview.Handlers;

public interface IPreviewHandler
{
    Task<UIElement> CreatePreviewAsync(string filePath);
}
