using System.Runtime.InteropServices;
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
                workerW = Win32.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
            return true;
        }, IntPtr.Zero);

        _workerW = workerW != IntPtr.Zero ? workerW : progman;
        return _workerW;
    }

    public void EmbedWindow(IntPtr childHwnd, int screenX, int screenY, int width, int height)
    {
        IntPtr host = GetDesktopWorkerW();
        Win32.SetParent(childHwnd, host);

        int exStyle = Win32.GetWindowLong(childHwnd, Win32.GWL_EXSTYLE);
        exStyle |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLong(childHwnd, Win32.GWL_EXSTYLE, exStyle);

        int virtualOriginX = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        int virtualOriginY = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        int workerX = screenX - virtualOriginX;
        int workerY = screenY - virtualOriginY;

        Win32.SetWindowPos(childHwnd, IntPtr.Zero, workerX, workerY, width, height,
            Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
    }

    public void DetachWindow(IntPtr childHwnd)
    {
        Win32.SetParent(childHwnd, IntPtr.Zero);
    }

    public List<MonitorProfile> GetAllMonitors()
    {
        var monitors = new List<MonitorProfile>();
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
        return monitors;

        bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Win32.RECT rect, IntPtr data)
        {
            var info = new Win32.MONITORINFOEX();
            info.cbSize = Marshal.SizeOf<Win32.MONITORINFOEX>();

            if (Win32.GetMonitorInfo(hMonitor, ref info))
            {
                bool isPrimary = (info.dwFlags & Win32.MONITORINFOF_PRIMARY) != 0;

                uint dpiX = 96, dpiY = 96;
                try { Win32.GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY); }
                catch { /* fallback to 96 */ }

                monitors.Add(new MonitorProfile(
                    info.szDevice,
                    info.szDevice,
                    new ScreenRect(
                        info.rcMonitor.Left,
                        info.rcMonitor.Top,
                        info.rcMonitor.Right - info.rcMonitor.Left,
                        info.rcMonitor.Bottom - info.rcMonitor.Top),
                    isPrimary,
                    dpiX,
                    dpiY));
            }
            return true;
        }
    }

    public void Reset()
    {
        _workerW = IntPtr.Zero;
    }
}
