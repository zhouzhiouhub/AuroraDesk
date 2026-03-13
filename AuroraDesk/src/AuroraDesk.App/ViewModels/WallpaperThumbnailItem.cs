using System.Windows.Media.Imaging;
using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuroraDesk.App.ViewModels;

public partial class WallpaperThumbnailItem : ObservableObject
{
    public WallpaperItem Model { get; }
    public bool IsBuiltIn { get; }

    public string Id => Model.Id;
    public string Title => Model.Title;
    public string SourcePath => Model.SourcePath;
    public string Resolution => Model.Width > 0 && Model.Height > 0
        ? $"{Model.Width} × {Model.Height}"
        : "未知";
    public string TypeLabel => Model.Type switch
    {
        WallpaperType.Image => "图片",
        WallpaperType.Video => "视频",
        WallpaperType.Html => "HTML",
        _ => "未知",
    };

    public string TypeGlyph => Model.Type switch
    {
        WallpaperType.Image => "\uE91B",
        WallpaperType.Video => "\uE714",
        WallpaperType.Html => "\uEB41",
        _ => "\uE8A5",
    };

    [ObservableProperty]
    private BitmapImage? thumbnail;

    [ObservableProperty]
    private bool isSelected;

    public WallpaperThumbnailItem(WallpaperItem model, bool isBuiltIn = false)
    {
        Model = model;
        IsBuiltIn = isBuiltIn;
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
