using System;
using System.Runtime.InteropServices;

namespace AuroraDesk.Core;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /*--------------------------------*\
    | ShowWindow / SetWindowPos 常量与导入 |
    \*_-------------------------------*/
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNORMAL = 1;
    internal const int SW_SHOWMINIMIZED = 2;
    internal const int SW_SHOWMAXIMIZED = 3;
    internal const int SW_RESTORE = 9;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    internal static readonly IntPtr HWND_TOP = IntPtr.Zero;

    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_FRAMECHANGED = 0x0020;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    /*--------------------------------*\
    | Window styles / Get/SetWindowLong |
    \*_-------------------------------*/
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_CHILD = 0x40000000;
    internal const int WS_POPUP = unchecked((int)0x80000000);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    /*--------------------------------*\
    | Win32 host window creation APIs  |
    \*_-------------------------------*/
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
        public IntPtr hIconSm;
    }

    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassExW(ref WNDCLASSEXW wcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowExW(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    internal const int WS_VISIBLE = 0x10000000;
    internal const int WS_CLIPSIBLINGS = 0x04000000;
    internal const int WS_CLIPCHILDREN = 0x02000000;
    internal const int WS_OVERLAPPED = 0x00000000;
    internal const int WS_CHILDWINDOW = 0x40000000;

    // DPI
    [DllImport("Shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    internal const int MDT_EFFECTIVE_DPI = 0;
}
