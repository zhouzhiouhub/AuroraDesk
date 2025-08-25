using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AuroraDesk.Models;
using AuroraDesk.Core;

namespace AuroraDesk.Pages
{
    public sealed partial class GalleryPage : Page
    {
        private readonly List<WallpaperItem> _items = new();

        public GalleryPage()
        {
            this.InitializeComponent();
            LoadWallpapers();
            WallpaperList.ItemsSource = _items;
        }

        private static readonly string[] ImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
        private static bool IsImageFile(string path) => ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        private static bool IsHtmlFile(string path) { var ext = Path.GetExtension(path).ToLowerInvariant(); return ext == ".html" || ext == ".htm"; }

        private void LoadWallpapers()
        {
            var baseDir = AppContext.BaseDirectory;
            var wpRoot = Path.Combine(baseDir, "Library", "wallpapers");
            if (!Directory.Exists(wpRoot)) return;

            foreach (var dir in Directory.GetDirectories(wpRoot))
            {
                var livelyInfo = Path.Combine(dir, "LivelyInfo.json");
                if (File.Exists(livelyInfo))
                {
                    try
                    {
                        using var fs = File.OpenRead(livelyInfo);
                        using var doc = JsonDocument.Parse(fs);
                        var root = doc.RootElement;
                        var title = root.TryGetProperty("Title", out var t) ? t.GetString() ?? Path.GetFileName(dir) : Path.GetFileName(dir);
                        var desc = root.TryGetProperty("Desc", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                        var thumb = root.TryGetProperty("Thumbnail", out var th) ? th.GetString() : null;
                        var fileName = root.TryGetProperty("FileName", out var f) ? f.GetString() : null;

                        var item = new WallpaperItem
                        {
                            Title = title,
                            Description = desc,
                            ThumbnailPath = thumb != null ? Path.Combine(dir, thumb) : string.Empty,
                            LaunchPath = fileName != null ? Path.Combine(dir, fileName) : string.Empty
                        };

                        if (File.Exists(item.LaunchPath))
                        {
                            _items.Add(item);
                            continue;
                        }
                    }
                    catch { }
                }

                var html = Directory.GetFiles(dir, "*.htm*").FirstOrDefault();
                if (string.IsNullOrEmpty(html))
                {
                    var indexHtml = Path.Combine(dir, "index.html");
                    if (File.Exists(indexHtml)) html = indexHtml;
                }
                if (!string.IsNullOrEmpty(html))
                {
                    var thumbPath = FirstExisting(
                        Path.Combine(dir, "thumbnail.jpg"),
                        Path.Combine(dir, "preview.gif"),
                        Path.Combine(baseDir, "Library", "wallpapers", "static", "gral.png")
                    );
                    _items.Add(new WallpaperItem
                    {
                        Title = Path.GetFileName(dir),
                        Description = string.Empty,
                        ThumbnailPath = thumbPath ?? string.Empty,
                        LaunchPath = html
                    });
                    continue;
                }

                var firstImage = Directory.EnumerateFiles(dir).FirstOrDefault(f => IsImageFile(f));
                if (!string.IsNullOrEmpty(firstImage))
                {
                    var thumb = firstImage;
                    _items.Add(new WallpaperItem
                    {
                        Title = Path.GetFileName(dir),
                        Description = string.Empty,
                        ThumbnailPath = thumb,
                        LaunchPath = firstImage
                    });
                }
            }

            foreach (var file in Directory.GetFiles(wpRoot))
            {
                if (!(IsHtmlFile(file) || IsImageFile(file))) continue;
                var title = Path.GetFileNameWithoutExtension(file);
                var thumb = IsImageFile(file) ? file : Path.Combine(baseDir, "Library", "wallpapers", "static", "gral.png");
                if (!File.Exists(thumb)) thumb = file;
                _items.Add(new WallpaperItem
                {
                    Title = title,
                    Description = string.Empty,
                    ThumbnailPath = thumb,
                    LaunchPath = file
                });
            }
        }

        private static string? FirstExisting(params string[] paths)
        {
            foreach (var p in paths)
            {
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private void WallpaperList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is WallpaperItem item && File.Exists(item.LaunchPath))
            {
                var path = item.LaunchPath;
                string toOpen = path;
                if (IsImageFile(path))
                {
                    toOpen = CreateImageWrapperHtml(path);
                }
                var uri = new Uri(toOpen);
                WallpaperManager.InitializeOrAttachDesktopWallpaper(uri);
            }
        }

        public void ApplyFilter(string? keyword)
        {
            IEnumerable<WallpaperItem> src = _items;
            var k = (keyword ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(k))
            {
                src = _items.Where(i => i.Title.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            WallpaperList.ItemsSource = src;
        }

        private static string CreateImageWrapperHtml(string imagePath)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "AuroraDesk", "wrappers");
            Directory.CreateDirectory(tempDir);
            var wrapperPath = Path.Combine(tempDir, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(imagePath)).Replace('\\', '_').Replace('/', '_') + ".html");
            var html = "<!doctype html><html><head><meta charset=\"utf-8\"><style>html,body{margin:0;height:100%;background:#000}img{position:fixed;inset:0;width:100%;height:100%;object-fit:cover}</style></head><body>" +
                       "<img src=\"" + new Uri(imagePath).AbsoluteUri + "\" alt=\"wallpaper\"></body></html>";
            File.WriteAllText(wrapperPath, html);
            return wrapperPath;
        }
    }
}


