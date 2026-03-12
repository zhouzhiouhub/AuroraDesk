using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;
using Microsoft.Extensions.Logging;

namespace AuroraDesk.Application.Services;

public sealed class WallpaperService : IDisposable
{
    private readonly IDesktopHost _desktopHost;
    private readonly IConfigService _configService;
    private readonly Func<WallpaperType, IWallpaperRenderer> _rendererFactory;
    private readonly ILogger<WallpaperService> _logger;

    private IWallpaperRenderer? _currentRenderer;

    public string? CurrentWallpaperPath { get; private set; }
    public ScaleMode CurrentScaleMode { get; private set; } = ScaleMode.Fill;
    public bool IsActive => _currentRenderer is not null;

    public WallpaperService(
        IDesktopHost desktopHost,
        IConfigService configService,
        Func<WallpaperType, IWallpaperRenderer> rendererFactory,
        ILogger<WallpaperService> logger)
    {
        _desktopHost = desktopHost;
        _configService = configService;
        _rendererFactory = rendererFactory;
        _logger = logger;
    }

    public async Task ApplyImageAsync(string imagePath, ScaleMode scaleMode)
    {
        if (!File.Exists(imagePath))
        {
            _logger.LogWarning("Image file not found: {Path}", imagePath);
            throw new FileNotFoundException("Wallpaper image not found.", imagePath);
        }

        ClearWallpaper();

        try
        {
            var renderer = _rendererFactory(WallpaperType.Image);
            await renderer.InitializeAsync();

            var options = new PlaybackOptions(FitMode: scaleMode);
            await renderer.LoadAsync(imagePath, options);

            var screen = _desktopHost.GetPrimaryScreenBounds();
            var hwnd = renderer.GetWindowHandle();

            _logger.LogInformation(
                "Embedding at WorkerW offset ({X},{Y}), size {W}x{H}",
                screen.X, screen.Y, screen.Width, screen.Height);

            _desktopHost.EmbedWindow(hwnd, screen.X, screen.Y, screen.Width, screen.Height);

            _currentRenderer = renderer;
            CurrentWallpaperPath = imagePath;
            CurrentScaleMode = scaleMode;

            SaveConfig();
            _logger.LogInformation("Applied image wallpaper: {Path} ({Mode})", imagePath, scaleMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply image wallpaper: {Path}", imagePath);
            throw;
        }
    }

    public void ClearWallpaper()
    {
        if (_currentRenderer is null) return;

        try
        {
            _currentRenderer.Stop();

            var hwnd = _currentRenderer.GetWindowHandle();
            if (hwnd != IntPtr.Zero)
                _desktopHost.DetachWindow(hwnd);

            _currentRenderer.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while clearing wallpaper");
        }
        finally
        {
            _currentRenderer = null;
            CurrentWallpaperPath = null;
        }

        _configService.Set<string?>("wallpaper.path", null);
        _configService.Save();

        _logger.LogInformation("Wallpaper cleared");
    }

    public async Task RestoreAsync()
    {
        var path = _configService.Get<string>("wallpaper.path");
        var modeValue = _configService.Get<int?>("wallpaper.scaleMode");

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _logger.LogInformation("No wallpaper to restore");
            return;
        }

        var mode = modeValue.HasValue ? (ScaleMode)modeValue.Value : ScaleMode.Fill;

        _logger.LogInformation("Restoring wallpaper: {Path} ({Mode})", path, mode);
        await ApplyImageAsync(path, mode);
    }

    private void SaveConfig()
    {
        _configService.Set("wallpaper.path", CurrentWallpaperPath);
        _configService.Set("wallpaper.scaleMode", (int)CurrentScaleMode);
        _configService.Save();
    }

    public (int Width, int Height) GetPrimaryScreenResolution()
    {
        var bounds = _desktopHost.GetPrimaryScreenBounds();
        return (bounds.Width, bounds.Height);
    }

    public void Dispose()
    {
        try
        {
            if (_currentRenderer is not null)
            {
                _currentRenderer.Stop();
                var hwnd = _currentRenderer.GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                    _desktopHost.DetachWindow(hwnd);
                _currentRenderer.Dispose();
                _currentRenderer = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during WallpaperService disposal");
        }
    }
}
