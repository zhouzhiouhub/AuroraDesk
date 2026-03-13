using System.Text.Json;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;
using AuroraDesk.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace AuroraDesk.Infrastructure.Library;

public sealed class JsonWallpaperLibrary : IWallpaperLibrary
{
    private readonly string _filePath;
    private readonly ILogger<JsonWallpaperLibrary> _logger;
    private List<WallpaperItem> _items = [];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonWallpaperLibrary(ILogger<JsonWallpaperLibrary> logger)
        : this(PathHelper.GetLibraryDataPath(), logger) { }

    public JsonWallpaperLibrary(string filePath, ILogger<JsonWallpaperLibrary> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public IReadOnlyList<WallpaperItem> GetAll() => _items.AsReadOnly();

    public WallpaperItem? GetById(string id) =>
        _items.FirstOrDefault(i => i.Id == id);

    public void Add(WallpaperItem item)
    {
        _items.Add(item);
        Save();
        _logger.LogInformation("Added wallpaper to library: {Title} ({Id})", item.Title, item.Id);
    }

    public void AddRange(IEnumerable<WallpaperItem> items)
    {
        var list = items.ToList();
        _items.AddRange(list);
        Save();
        _logger.LogInformation("Added {Count} wallpapers to library", list.Count);
    }

    public void Remove(string id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;

        _items.Remove(item);

        if (!string.IsNullOrEmpty(item.ThumbnailPath) && File.Exists(item.ThumbnailPath))
        {
            try { File.Delete(item.ThumbnailPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete thumbnail: {Path}", item.ThumbnailPath); }
        }

        Save();
        _logger.LogInformation("Removed wallpaper from library: {Id}", id);
    }

    public bool ExistsByPath(string sourcePath) =>
        _items.Any(i => string.Equals(i.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_items, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save wallpaper library");
        }
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _items = [];
            _logger.LogInformation("No wallpaper library file found, starting empty");
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _items = JsonSerializer.Deserialize<List<WallpaperItem>>(json, SerializerOptions) ?? [];
            _logger.LogInformation("Loaded {Count} wallpapers from library", _items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallpaper library, starting empty");
            _items = [];
        }
    }
}
