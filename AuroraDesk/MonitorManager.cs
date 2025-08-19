using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace AuroraDesk.Core;

public record MonitorInfo(string DeviceName, RectInt32 Bounds, bool Primary, IntPtr Handle);

internal static class MonitorManager
{
    private const int CCHDEVICENAME = 32;
    private const int MONITORINFOF_PRIMARY = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string szDevice;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref mi))
            {
                var b = new RectInt32(mi.rcMonitor.left,
                                      mi.rcMonitor.top,
                                      mi.rcMonitor.right - mi.rcMonitor.left,
                                      mi.rcMonitor.bottom - mi.rcMonitor.top);
                bool primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                list.Add(new MonitorInfo(mi.szDevice, b, primary, hMon));
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
