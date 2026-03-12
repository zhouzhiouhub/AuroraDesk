namespace AuroraDesk.Core.Models;

public record MonitorProfile(
    string MonitorId,
    string DeviceName,
    ScreenRect Bounds,
    bool IsPrimary,
    double DpiX,
    double DpiY);
