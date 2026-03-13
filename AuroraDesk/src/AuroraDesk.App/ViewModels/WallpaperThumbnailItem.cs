using System.Windows.Media.Imaging;
using AuroraDesk.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuroraDesk.App.ViewModels;

public partial class WallpaperThumbnailItem : ObservableObject
{
    public WallpaperItem Model { get; }

    public string Id => Model.Id;
    public string Title => Model.Title;
    public string SourcePath => Model.SourcePath;
    public string Resolution => $"{Model.Width} × {Model.Height}";

    [ObservableProperty]
    private BitmapImage? thumbnail;

    [ObservableProperty]
    private bool isSelected;

    public WallpaperThumbnailItem(WallpaperItem model)
    {
        Model = model;
    }

    public void LoadThumbnail()
    {
        var path = Model.ThumbnailPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            path = Model.SourcePath;
            if (!System.IO.File.Exists(path)) return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 200;
            bitmap.EndInit();
            bitmap.Freeze();
            Thumbnail = bitmap;
        }
        catch
        {
            Thumbnail = null;
        }
    }
}
