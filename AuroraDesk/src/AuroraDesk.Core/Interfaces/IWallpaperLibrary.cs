using AuroraDesk.Core.Models;

namespace AuroraDesk.Core.Interfaces;

public interface IWallpaperLibrary
{
    IReadOnlyList<WallpaperItem> GetAll();
    WallpaperItem? GetById(string id);
    void Add(WallpaperItem item);
    void AddRange(IEnumerable<WallpaperItem> items);
    void Remove(string id);
    bool ExistsByPath(string sourcePath);
    void Save();
    void Load();
}
