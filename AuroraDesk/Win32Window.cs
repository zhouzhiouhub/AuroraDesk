using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AuroraDesk.Core;

public static class Win32Window
{
    public static nint GetHwnd(Window window) => WindowNative.GetWindowHandle(window);

    public static void AttachToParent(Window window, nint parentHwnd)
    {
        var hwnd = GetHwnd(window);
        NativeMethods.SetParent(hwnd, parentHwnd);
    }
}
