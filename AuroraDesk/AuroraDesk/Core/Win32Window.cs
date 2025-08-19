using Microsoft.UI.Xaml;
using WinRT.Interop;
using System;

namespace AuroraDesk.Core;

public static class Win32Window
{
    public static nint GetHwnd(Window window) => WindowNative.GetWindowHandle(window);

    public static void AttachToParent(Window window, nint parentHwnd)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));
            
        if (parentHwnd == IntPtr.Zero)
            throw new ArgumentException("Invalid parent window handle", nameof(parentHwnd));

        var hwnd = GetHwnd(window);
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Unable to get window handle");

        // 验证父窗口句柄是否有效
        if (!NativeMethods.IsWindow(parentHwnd))
            throw new ArgumentException("Parent window handle is not valid", nameof(parentHwnd));

        var result = NativeMethods.SetParent(hwnd, parentHwnd);
        if (result == IntPtr.Zero)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"SetParent failed with error code: {error}");
        }
    }
}
