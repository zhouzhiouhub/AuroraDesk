using AuroraDesk.Core.Models;

namespace AuroraDesk.Core.Interfaces;

public interface IDesktopHost
{
    void EmbedWindow(IntPtr childHwnd, int x, int y, int width, int height);
    void DetachWindow(IntPtr childHwnd);
    ScreenRect GetPrimaryScreenBounds();
    void Reset();
}
