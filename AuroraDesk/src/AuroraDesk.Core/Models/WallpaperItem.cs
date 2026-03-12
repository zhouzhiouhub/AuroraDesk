using AuroraDesk.Core.Enums;

namespace AuroraDesk.Core.Models;

public record WallpaperItem(
    string Id,
    WallpaperType Type,
    string SourcePath,
    string Title,
    string? ThumbnailPath,
    TimeSpan? Duration,
    int Width,
    int Height,
    List<string> Tags,
    DateTime AddedTime);
