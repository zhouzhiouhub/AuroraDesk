namespace AuroraDesk.Core.Enums;

[Flags]
public enum PausePolicy
{
    None = 0,
    WhenFullscreen = 1 << 0,
    WhenBatteryLow = 1 << 1,
    WhenMaximized = 1 << 2
}
