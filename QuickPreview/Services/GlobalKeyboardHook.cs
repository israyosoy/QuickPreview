using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickPreview.Services;

public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int VK_SPACE = 0x20;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_LEFT = 0x25;
    private const int VK_RIGHT = 0x27;
    private const int VK_F = 0x46;
    private const string EXPLORER_CLASS = "CabinetWClass";

    public event Action? SpacePressed;
    public event Action? EscapePressed;
    public event Action? PrevFileRequested;
    public event Action? NextFileRequested;
    public event Action? FullscreenToggleRequested;

    // Set this to allow Space (and arrows) to fire when a preview is already open,
    // even if the foreground window is no longer Explorer.
    public Func<bool>? IsPreviewOpen { get; set; }

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _spaceDown;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP;

            if (vkCode == VK_ESCAPE && isKeyDown)
            {
                EscapePressed?.Invoke();
            }
            else if (vkCode == VK_SPACE)
            {
                bool hasPreview = IsPreviewOpen?.Invoke() == true;
                bool canActivate = hasPreview || (IsForegroundWindowExplorer() && !IsTypingInTextBox());
                if (isKeyDown && !_spaceDown && canActivate)
                {
                    _spaceDown = true;
                    SpacePressed?.Invoke();
                    return (IntPtr)1;
                }
                if (isKeyUp && _spaceDown)
                {
                    _spaceDown = false;
                    return (IntPtr)1;
                }
                if (_spaceDown)
                    return (IntPtr)1;
            }
            else if ((vkCode == VK_LEFT || vkCode == VK_RIGHT) && isKeyDown
                     && IsPreviewOpen?.Invoke() == true && IsForegroundWindowExplorer())
            {
                if (vkCode == VK_LEFT) PrevFileRequested?.Invoke();
                else NextFileRequested?.Invoke();
                return (IntPtr)1;
            }
            else if (vkCode == VK_F && isKeyDown && IsPreviewOpen?.Invoke() == true)
            {
                FullscreenToggleRequested?.Invoke();
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsForegroundWindowExplorer()
    {
        IntPtr hwnd = GetForegroundWindow();
        var cls = new StringBuilder(256);
        GetClassName(hwnd, cls, cls.Capacity);
        string c = cls.ToString();
        // CabinetWClass = normal Explorer folder window
        // Progman / WorkerW = Windows Desktop
        return c == EXPLORER_CLASS || c == "Progman" || c == "WorkerW";
    }

    private static bool IsTypingInTextBox()
    {
        IntPtr foregroundHwnd = GetForegroundWindow();
        uint threadId = GetWindowThreadProcessId(foregroundHwnd, out _);

        var gui = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref gui) || gui.hwndFocus == IntPtr.Zero)
            return false;

        var cls = new StringBuilder(256);
        GetClassName(gui.hwndFocus, cls, cls.Capacity);
        string focusClass = cls.ToString();
        return focusClass.Contains("Edit", StringComparison.OrdinalIgnoreCase)
            || focusClass.Contains("RichEdit", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public System.Drawing.Rectangle rcCaret;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);
}
