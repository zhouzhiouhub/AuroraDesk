using AuroraDesk.Core.Enums;

namespace AuroraDesk.Core.Models;

public record PlaybackOptions(
    bool Loop = true,
    bool Mute = false,
    int Volume = 100,
    double PlaybackRate = 1.0,
    ScaleMode FitMode = ScaleMode.Fill,
    bool PauseWhenFullscreen = true,
    bool PauseWhenBatteryLow = false);
