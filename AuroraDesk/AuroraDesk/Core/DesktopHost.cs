using System;
using static AuroraDesk.Core.NativeMethods;

namespace AuroraDesk.Core;

public static class DesktopHost
{
    private const uint WM_SPAWN_WORKERW = 0x052C;
    private static readonly string s_hostClass = "AuroraDeskHostWindow";
    private static NativeMethods.WndProc? s_wndProc;

    public static IntPtr GetWorkerW()
    {
        try
        {
            var progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Could not find Progman window");
                return IntPtr.Zero;
            }

            // 发送消息创建 WorkerW
            var result = SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
            if (result == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("SendMessageTimeout failed");
                return IntPtr.Zero;
            }

            IntPtr workerw = IntPtr.Zero;
            EnumWindowsProc enumProc = (top, _) =>
            {
                var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    return false; // 找到就停止枚举
                }
                return true; // 继续枚举
            };
            bool found = EnumWindows(enumProc, IntPtr.Zero);
            System.GC.KeepAlive(enumProc);

            if (workerw == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("Could not find WorkerW window");
            }

            return workerw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception in GetWorkerW: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    internal static IntPtr CreateChildHostOnWorkerW(out IntPtr parentWorkerw, out NativeMethods.RECT parentClient)
    {
        parentWorkerw = GetWorkerW();
        parentClient = default;
        if (parentWorkerw == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try { NativeMethods.GetClientRect(parentWorkerw, out parentClient); } catch { }

        s_wndProc ??= (h, m, w, l) => NativeMethods.DefWindowProcW(h, m, w, l);
        var wcx = new NativeMethods.WNDCLASSEXW
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WNDCLASSEXW>(),
            lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(s_wndProc),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = s_hostClass,
            lpszMenuName = string.Empty
        };
        _ = NativeMethods.RegisterClassExW(ref wcx);

        int width = Math.Max(0, parentClient.right - parentClient.left);
        int height = Math.Max(0, parentClient.bottom - parentClient.top);
        var hwnd = NativeMethods.CreateWindowExW(
            0,
            s_hostClass,
            "AuroraDeskHost",
            NativeMethods.WS_CHILDWINDOW | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS,
            0,
            0,
            width,
            height,
            parentWorkerw,
            IntPtr.Zero,
            NativeMethods.GetModuleHandle(null),
            IntPtr.Zero);

        return hwnd;
    }
}
