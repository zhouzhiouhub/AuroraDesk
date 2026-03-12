using AuroraDesk.Core.Models;

namespace AuroraDesk.Core.Interfaces;

public interface IDesktopHost
{
    /// <summary>
    /// Embeds a window as a child of the desktop WorkerW.
    /// Coordinates are in virtual screen space (primary monitor at 0,0).
    /// </summary>
    void EmbedWindow(IntPtr childHwnd, int screenX, int screenY, int width, int height);
    void DetachWindow(IntPtr childHwnd);
    List<MonitorProfile> GetAllMonitors();
    void Reset();
}
