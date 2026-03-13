namespace AuroraDesk.Shared.Helpers;

public static class PathHelper
{
    public static string GetAppDataPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraDesk");

    public static string GetConfigPath() =>
        Path.Combine(GetAppDataPath(), "config.json");

    public static string GetLogPath() =>
        Path.Combine(GetAppDataPath(), "logs");

    public static string GetCachePath() =>
        Path.Combine(GetAppDataPath(), "cache");

    public static string GetThumbnailPath() =>
        Path.Combine(GetCachePath(), "thumbnails");

    public static string GetLibraryDataPath() =>
        Path.Combine(GetAppDataPath(), "library.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(GetAppDataPath());
        Directory.CreateDirectory(GetLogPath());
        Directory.CreateDirectory(GetCachePath());
        Directory.CreateDirectory(GetThumbnailPath());
    }
}
