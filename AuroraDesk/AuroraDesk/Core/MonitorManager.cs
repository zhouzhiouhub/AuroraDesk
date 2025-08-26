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

    // Fallback: EnumDisplayDevices / EnumDisplaySettingsEx
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool EnumDisplaySettingsEx(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags);

    private const int EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr dw) =>
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
        
        if (list.Count <= 1)
        {
            var fallback = GetAllViaDisplayDevices();
            if (fallback.Count > 0)
            {
                return fallback;
            }
        }
        return list;
    }

    private static IReadOnlyList<MonitorInfo> GetAllViaDisplayDevices()
    {
        var result = new List<MonitorInfo>();
        uint i = 0;
        while (true)
        {
            var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, i, ref dd, EDD_GET_DEVICE_INTERFACE_NAME)) break;
            i++;

            var attached = (dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
            if (!attached) continue;

            var devmode = new DEVMODE();
            devmode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            if (EnumDisplaySettingsEx(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref devmode, 0))
            {
                var bounds = new RectInt32(devmode.dmPositionX, devmode.dmPositionY, devmode.dmPelsWidth, devmode.dmPelsHeight);
                bool primary = (dd.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
                result.Add(new MonitorInfo(dd.DeviceName, bounds, primary, IntPtr.Zero));
            }
        }
        return result;
    }
}


