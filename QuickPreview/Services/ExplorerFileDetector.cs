using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickPreview.Services;

public static class ExplorerFileDetector
{
    public static string? GetSelectedFilePath()
    {
        IntPtr foregroundHwnd = GetForegroundWindow();

        // Desktop (Progman / WorkerW) — shell.Windows() never includes the desktop
        if (IsDesktopWindow(foregroundHwnd))
            return GetDesktopSelectedFilePath();

        // Normal Explorer folder window
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

    // ── Desktop detection ─────────────────────────────────────────────────────

    private static bool IsDesktopWindow(IntPtr hwnd)
    {
        var cls = new StringBuilder(256);
        GetClassName(hwnd, cls, cls.Capacity);
        string c = cls.ToString();
        return c == "Progman" || c == "WorkerW";
    }

    private static string? GetDesktopSelectedFilePath()
    {
        IntPtr hListView = FindDesktopListView();
        if (hListView == IntPtr.Zero) return null;

        // Get focused/selected item index
        int index = (int)SendMessage(hListView, LVM_GETNEXTITEM, new IntPtr(-1), new IntPtr(LVNI_SELECTED));
        if (index < 0) return null;

        string? name = ReadListViewItemText(hListView, index);
        if (string.IsNullOrEmpty(name)) return null;

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string path = Path.Combine(desktopPath, name);
        return (File.Exists(path) || Directory.Exists(path)) ? path : null;
    }

    private static IntPtr FindDesktopListView()
    {
        // Layout: Progman > SHELLDLL_DefView > SysListView32
        IntPtr hListView = FindListViewIn(FindWindow("Progman", null));
        if (hListView != IntPtr.Zero) return hListView;

        // Fallback: WorkerW > SHELLDLL_DefView > SysListView32
        IntPtr hWorkerW = IntPtr.Zero;
        do
        {
            hWorkerW = FindWindowEx(IntPtr.Zero, hWorkerW, "WorkerW", null);
            hListView = FindListViewIn(hWorkerW);
            if (hListView != IntPtr.Zero) return hListView;
        } while (hWorkerW != IntPtr.Zero);

        return IntPtr.Zero;
    }

    private static IntPtr FindListViewIn(IntPtr hParent)
    {
        if (hParent == IntPtr.Zero) return IntPtr.Zero;
        IntPtr hDefView = FindWindowEx(hParent, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (hDefView == IntPtr.Zero) return IntPtr.Zero;
        return FindWindowEx(hDefView, IntPtr.Zero, "SysListView32", null);
    }

    // Read an item's text from a SysListView32 in another process via shared memory.
    // x64 LVITEMW layout: mask(4) iItem(4) iSubItem(4) state(4) stateMask(4) [pad4]
    //                     pszText(8@24) cchTextMax(4@32) iImage(4) lParam(8) ...
    private static string? ReadListViewItemText(IntPtr hListView, int index)
    {
        GetWindowThreadProcessId(hListView, out uint pid);
        IntPtr hProcess = OpenProcess(PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE,
                                      false, pid);
        if (hProcess == IntPtr.Zero) return null;
        try
        {
            const int maxChars   = 260;
            const int structSize = 64;  // covers all fields we set (up to offset 36)
            int  allocSize = structSize + maxChars * 2;

            IntPtr remote = VirtualAllocEx(hProcess, IntPtr.Zero, (UIntPtr)allocSize,
                                           MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remote == IntPtr.Zero) return null;
            try
            {
                long textAddr = remote.ToInt64() + structSize;
                byte[] lvitem = new byte[structSize];
                BitConverter.GetBytes(LVIF_TEXT).CopyTo(lvitem, 0);    // mask
                BitConverter.GetBytes(index)    .CopyTo(lvitem, 4);    // iItem
                BitConverter.GetBytes(textAddr) .CopyTo(lvitem, 24);   // pszText (x64 offset)
                BitConverter.GetBytes(maxChars) .CopyTo(lvitem, 32);   // cchTextMax

                WriteProcessMemory(hProcess, remote, lvitem, (UIntPtr)lvitem.Length, out _);
                SendMessage(hListView, LVM_GETITEMTEXTW, new IntPtr(index), remote);

                byte[] buf = new byte[maxChars * 2];
                ReadProcessMemory(hProcess, new IntPtr(textAddr), buf, (UIntPtr)buf.Length, out _);

                string text = Encoding.Unicode.GetString(buf).TrimEnd('\0');
                return text.Length > 0 ? text : null;
            }
            finally { VirtualFreeEx(hProcess, remote, UIntPtr.Zero, MEM_RELEASE); }
        }
        finally { CloseHandle(hProcess); }
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    private const int  LVM_GETNEXTITEM  = 0x100C;
    private const int  LVM_GETITEMTEXTW = 0x1073;
    private const int  LVNI_SELECTED    = 0x0002;
    private const int  LVIF_TEXT        = 0x0001;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ      = 0x0010;
    private const uint PROCESS_VM_WRITE     = 0x0020;
    private const uint MEM_COMMIT   = 0x1000;
    private const uint MEM_RESERVE  = 0x2000;
    private const uint MEM_RELEASE  = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter,
                                              string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize,
                                                uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll")]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize,
                                             uint dwFreeType);

    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
                                                  byte[] lpBuffer, UIntPtr nSize,
                                                  out UIntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
                                                 byte[] lpBuffer, UIntPtr nSize,
                                                 out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);
}
