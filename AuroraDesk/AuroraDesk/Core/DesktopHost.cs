using System;
using static AuroraDesk.Core.NativeMethods;

namespace AuroraDesk.Core;

public static class DesktopHost
{
    private const uint WM_SPAWN_WORKERW = 0x052C;

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
            bool found = EnumWindows((top, _) =>
            {
                var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
                    return false; // 找到就停止枚举
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

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
}
