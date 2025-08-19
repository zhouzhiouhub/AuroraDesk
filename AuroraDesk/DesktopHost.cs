using System;
using static AuroraDesk.Core.NativeMethods;

namespace AuroraDesk.Core;

public static class DesktopHost
{
    private const uint WM_SPAWN_WORKERW = 0x052C;

    public static IntPtr GetWorkerW()
    {
        var progman = FindWindow("Progman", null);
        SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

        IntPtr workerw = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            var shellView = FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
                workerw = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return workerw == IntPtr.Zero; // 找到就停
        }, IntPtr.Zero);

        return workerw;
    }
}
