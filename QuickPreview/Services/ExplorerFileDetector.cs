using System.Runtime.InteropServices;

namespace QuickPreview.Services;

public static class ExplorerFileDetector
{
    public static string? GetSelectedFilePath()
    {
        IntPtr foregroundHwnd = GetForegroundWindow();

        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();

            for (int i = 0; i < windows.Count; i++)
            {
                try
                {
                    dynamic window = windows.Item(i);
                    if (window == null) continue;

                    IntPtr hwnd = new IntPtr(Convert.ToInt64(window.HWND));
                    if (hwnd != foregroundHwnd) continue;

                    dynamic selectedItems = window.Document.SelectedItems();
                    if (selectedItems.Count > 0)
                        return selectedItems.Item(0).Path as string;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
