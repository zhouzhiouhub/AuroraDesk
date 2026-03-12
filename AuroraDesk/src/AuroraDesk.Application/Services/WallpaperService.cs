using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;
using Microsoft.Extensions.Logging;

namespace AuroraDesk.Application.Services;

public class MonitorWallpaperConfig
{
    public string DeviceName { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public int ScaleMode { get; set; }
}

public sealed class WallpaperService : IDisposable
{
    private readonly IDesktopHost _desktopHost;
    private readonly IConfigService _configService;
    private readonly Func<WallpaperType, IWallpaperRenderer> _rendererFactory;
    private readonly ILogger<WallpaperService> _logger;

    private readonly Dictionary<string, IWallpaperRenderer> _renderers = new();
    private readonly Dictionary<string, (string Path, ScaleMode Mode)> _assignments = new();

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

    public List<MonitorProfile> GetAllMonitors() => _desktopHost.GetAllMonitors();

    public bool HasAssignment(string monitorId) => _assignments.ContainsKey(monitorId);

    public (string Path, ScaleMode Mode)? GetAssignment(string monitorId)
        => _assignments.TryGetValue(monitorId, out var a) ? a : null;

    public bool IsMonitorActive(string monitorId) => _renderers.ContainsKey(monitorId);

    public async Task ApplyImageAsync(string monitorId, string imagePath, ScaleMode scaleMode)
    {
        if (!File.Exists(imagePath))
        {
            _logger.LogWarning("Image file not found: {Path}", imagePath);
            throw new FileNotFoundException("Wallpaper image not found.", imagePath);
        }

        var monitors = _desktopHost.GetAllMonitors();
        var monitor = monitors.FirstOrDefault(m => m.MonitorId == monitorId);
        if (monitor is null)
        {
            _logger.LogWarning("Monitor not found: {Id}", monitorId);
            throw new InvalidOperationException($"Monitor '{monitorId}' not found.");
        }

        ClearMonitor(monitorId);

        try
        {
            var renderer = _rendererFactory(WallpaperType.Image);
            await renderer.InitializeAsync();

            var options = new PlaybackOptions(FitMode: scaleMode);
            await renderer.LoadAsync(imagePath, options);

            var hwnd = renderer.GetWindowHandle();
            var b = monitor.Bounds;

            _logger.LogInformation(
                "Embedding on {Dev} at ({X},{Y}) {W}x{H}",
                monitorId, b.X, b.Y, b.Width, b.Height);

            _desktopHost.EmbedWindow(hwnd, b.X, b.Y, b.Width, b.Height);

            _renderers[monitorId] = renderer;
            _assignments[monitorId] = (imagePath, scaleMode);

            SaveConfig();
            _logger.LogInformation("Applied wallpaper on {Dev}: {Path} ({Mode})", monitorId, imagePath, scaleMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply wallpaper on {Dev}: {Path}", monitorId, imagePath);
            throw;
        }
    }

    public void ClearMonitor(string monitorId)
    {
        if (!_renderers.TryGetValue(monitorId, out var renderer)) return;

        try
        {
            renderer.Stop();
            var hwnd = renderer.GetWindowHandle();
            if (hwnd != IntPtr.Zero)
                _desktopHost.DetachWindow(hwnd);
            renderer.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error clearing wallpaper on {Dev}", monitorId);
        }
        finally
        {
            _renderers.Remove(monitorId);
            _assignments.Remove(monitorId);
        }

        SaveConfig();
        _logger.LogInformation("Wallpaper cleared on {Dev}", monitorId);
    }

    public void ClearAllMonitors()
    {
        foreach (var monitorId in _renderers.Keys.ToList())
        {
            try
            {
                var renderer = _renderers[monitorId];
                renderer.Stop();
                var hwnd = renderer.GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                    _desktopHost.DetachWindow(hwnd);
                renderer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing wallpaper on {Dev}", monitorId);
            }
        }

        _renderers.Clear();
        _assignments.Clear();
        SaveConfig();
        _logger.LogInformation("All wallpapers cleared");
    }

    public async Task RestoreAsync()
    {
        var configs = _configService.Get<List<MonitorWallpaperConfig>>("wallpaper.monitors");
        if (configs is null || configs.Count == 0)
        {
            _logger.LogInformation("No wallpaper assignments to restore");
            return;
        }

        var currentMonitors = _desktopHost.GetAllMonitors();

        foreach (var cfg in configs)
        {
            if (string.IsNullOrEmpty(cfg.ImagePath) || !File.Exists(cfg.ImagePath))
                continue;

            var monitor = currentMonitors.FirstOrDefault(m => m.DeviceName == cfg.DeviceName);
            if (monitor is null)
            {
                _logger.LogInformation("Skipping restore for disconnected monitor: {Dev}", cfg.DeviceName);
                continue;
            }

            try
            {
                var mode = (ScaleMode)cfg.ScaleMode;
                _logger.LogInformation("Restoring wallpaper on {Dev}: {Path} ({Mode})", cfg.DeviceName, cfg.ImagePath, mode);
                await ApplyImageAsync(monitor.MonitorId, cfg.ImagePath, mode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore wallpaper on {Dev}", cfg.DeviceName);
            }
        }
    }

    public void RefreshMonitors()
    {
        var currentMonitors = _desktopHost.GetAllMonitors();
        var currentIds = currentMonitors.Select(m => m.MonitorId).ToHashSet();

        var staleIds = _renderers.Keys.Where(id => !currentIds.Contains(id)).ToList();
        foreach (var id in staleIds)
        {
            _logger.LogInformation("Monitor disconnected, cleaning up: {Dev}", id);
            ClearMonitor(id);
        }
    }

    private void SaveConfig()
    {
        var configs = _assignments.Select(kv => new MonitorWallpaperConfig
        {
            DeviceName = kv.Key,
            ImagePath = kv.Value.Path,
            ScaleMode = (int)kv.Value.Mode
        }).ToList();

        _configService.Set("wallpaper.monitors", configs);
        _configService.Save();
    }

    public void Dispose()
    {
        foreach (var (monitorId, renderer) in _renderers)
        {
            try
            {
                renderer.Stop();
                var hwnd = renderer.GetWindowHandle();
                if (hwnd != IntPtr.Zero)
                    _desktopHost.DetachWindow(hwnd);
                renderer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing renderer for {Dev}", monitorId);
            }
        }
        _renderers.Clear();
    }
}
