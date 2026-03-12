using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Models;

namespace AuroraDesk.Core.Interfaces;

public interface IWallpaperRenderer : IDisposable
{
    WallpaperType SupportedType { get; }

    Task InitializeAsync();
    Task LoadAsync(string sourcePath, PlaybackOptions options);
    void Play();
    void Pause();
    void Stop();
    IntPtr GetWindowHandle();
    void SetBounds(int x, int y, int width, int height);
}
