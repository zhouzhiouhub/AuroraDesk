using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;
using AuroraDesk.Runtime.Native;

namespace AuroraDesk.Runtime.Desktop;

public class DesktopHostManager : IDesktopHost
{
    private IntPtr _workerW;

    private IntPtr GetDesktopWorkerW()
    {
        if (_workerW != IntPtr.Zero)
            return _workerW;

        IntPtr progman = Win32.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            throw new InvalidOperationException("Cannot find Progman window.");

        Win32.SendMessageTimeout(
            progman,
            Win32.WM_SPAWN_WORKER,
            new UIntPtr(0x0D),
            new IntPtr(0x01),
            Win32.SMTO_NORMAL,
            1000,
            out _);

        IntPtr workerW = IntPtr.Zero;
        Win32.EnumWindows((hwnd, lParam) =>
        {
            IntPtr shellView = Win32.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                workerW = Win32.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
            }
            return true;
        }, IntPtr.Zero);

        _workerW = workerW != IntPtr.Zero ? workerW : progman;
        return _workerW;
    }

    public void EmbedWindow(IntPtr childHwnd, int x, int y, int width, int height)
    {
        IntPtr host = GetDesktopWorkerW();

        Win32.SetParent(childHwnd, host);

        int exStyle = Win32.GetWindowLong(childHwnd, Win32.GWL_EXSTYLE);
        exStyle |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLong(childHwnd, Win32.GWL_EXSTYLE, exStyle);

        Win32.SetWindowPos(childHwnd, IntPtr.Zero, x, y, width, height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }

    public void DetachWindow(IntPtr childHwnd)
    {
        Win32.SetParent(childHwnd, IntPtr.Zero);
    }

    public ScreenRect GetPrimaryScreenBounds()
    {
        int virtualX = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        int virtualY = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        int width = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int height = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);
        return new ScreenRect(-virtualX, -virtualY, width, height);
    }

    public void Reset()
    {
        _workerW = IntPtr.Zero;
    }
}
