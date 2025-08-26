using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using AuroraDesk.Models;
using AuroraDesk.Core;

namespace AuroraDesk.Pages
{
    public sealed partial class GalleryPage : Page
    {
        private readonly ObservableCollection<WallpaperItem> _items = new();
        private string? _lastKeyword;

        private sealed class MonitorApplyContext
        {
            public WallpaperItem Item { get; }
            public string DeviceName { get; }
            public MonitorApplyContext(WallpaperItem item, string deviceName)
            {
                Item = item;
                DeviceName = deviceName;
            }
        }

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
                    var thumbPath = FindThumbnailForDir(dir)
                        ?? FirstExisting(
                            Path.Combine(baseDir, "Library", "wallpapers", "static", "girl.png")
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
                var thumb = IsImageFile(file) ? file : Path.Combine(baseDir, "Library", "wallpapers", "static", "girl.png");
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

        private static string? FindThumbnailForDir(string dir)
        {
            // 1) 常见命名优先
            var preferred = new[]
            {
                "thumbnail.jpg","thumbnail.png","preview.gif","preview.jpg","preview.png",
                "logo.png","logo.jpg","cover.jpg","cover.png",
                Path.Combine("lively_theme","thumbnail.jpg"),
                Path.Combine("lively_theme","background.jpg")
            };
            foreach (var name in preferred)
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return p;
            }

            // 2) 递归寻找任意图片
            var any = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f => IsImageFile(f));
            return any;
        }

        private void WallpaperList_ItemClick(object sender, ItemClickEventArgs e)
        {
            try
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
            catch (Exception ex)
            {
                _ = ShowErrorAsync("设置壁纸出错", ex);
            }
        }

        private void OnGalleryItemFlyoutOpening(object sender, object e)
        {
            if (sender is not MenuFlyout flyout)
            {
                return;
            }

            // 第一个子菜单：设为壁纸到显示器
            var sub = flyout.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault();
            if (sub == null)
            {
                return;
            }

            // 从 Tag 取出当前 WallpaperItem
            var item = sub.Tag as WallpaperItem;
            if (item == null)
            {
                // 尝试从其它菜单项获取
                var anyWithTag = flyout.Items.OfType<MenuFlyoutItem>().FirstOrDefault(i => i.Tag is WallpaperItem);
                item = anyWithTag?.Tag as WallpaperItem;
            }
            if (item == null)
            {
                return;
            }

            sub.Items.Clear();
            var monitors = MonitorManager.GetAll();
            foreach (var m in monitors)
            {
                var orient = m.Bounds.Width >= m.Bounds.Height ? "横屏" : "竖屏";
                var label = m.Primary
                    ? $"主显示器 ({m.Bounds.Width}x{m.Bounds.Height}, {orient})"
                    : $"{m.DeviceName} ({m.Bounds.Width}x{m.Bounds.Height}, {orient})";
                var mi = new MenuFlyoutItem { Text = label };
                mi.Tag = new MonitorApplyContext(item, m.DeviceName);
                mi.Click += OnSetWallpaperForMonitorClick;
                sub.Items.Add(mi);
            }

            // 第二个子菜单：预览到显示器
            var previewSub = flyout.Items.OfType<MenuFlyoutSubItem>().Skip(1).FirstOrDefault();
            if (previewSub != null)
            {
                previewSub.Items.Clear();
                previewSub.Tag = item;
                foreach (var m in monitors)
                {
                    var orient = m.Bounds.Width >= m.Bounds.Height ? "横屏" : "竖屏";
                    var label = m.Primary
                        ? $"主显示器 ({m.Bounds.Width}x{m.Bounds.Height}, {orient})"
                        : $"{m.DeviceName} ({m.Bounds.Width}x{m.Bounds.Height}, {orient})";
                    var mi = new MenuFlyoutItem { Text = label };
                    mi.Tag = new MonitorApplyContext(item, m.DeviceName);
                    mi.Click += OnPreviewForMonitorClick;
                    previewSub.Items.Add(mi);
                }
            }
        }

        private void OnSetWallpaperForMonitorClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not MenuFlyoutItem mi) return;
                if (mi.Tag is not MonitorApplyContext ctx) return;

                var path = ctx.Item.LaunchPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string toOpen = path;
                if (IsImageFile(path))
                {
                    toOpen = CreateImageWrapperHtml(path);
                }
                var uri = new Uri(toOpen);
                WallpaperManager.InitializeOrAttachForMonitor(uri, ctx.DeviceName);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("设置壁纸到指定显示器出错", ex);
            }
        }

        private void OnSetAsWallpaperAllClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not MenuFlyoutItem mi) return;
                if (mi.Tag is not WallpaperItem item) return;

                var path = item.LaunchPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string toOpen = path;
                if (IsImageFile(path))
                {
                    toOpen = CreateImageWrapperHtml(path);
                }
                var uri = new Uri(toOpen);
                WallpaperManager.InitializeOrAttachForAllMonitors(uri);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("为所有显示器设置壁纸出错", ex);
            }
        }

        public void ApplyFilter(string? keyword)
        {
            IEnumerable<WallpaperItem> src = _items;
            _lastKeyword = keyword;
            var k = (keyword ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(k))
            {
                src = _items.Where(i => i.Title.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            WallpaperList.ItemsSource = src;
        }

        public void ReloadAll()
        {
            _items.Clear();
            LoadWallpapers();
            ApplyFilter(_lastKeyword);
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

        private void OnSetAsWallpaperClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem m && m.Tag is WallpaperItem item)
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
            catch (Exception ex)
            {
                _ = ShowErrorAsync("设置壁纸出错", ex);
            }
        }

        private void OnPreviewClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem m && m.Tag is WallpaperItem item)
                {
                    var path = item.LaunchPath;
                    string toOpen = path;
                    if (IsImageFile(path))
                    {
                        toOpen = CreateImageWrapperHtml(path);
                    }
                    var uri = new Uri(toOpen);
                    var prefer = WallpaperManager.PreferredPreviewMonitorDeviceName;
                    if (!string.IsNullOrEmpty(prefer))
                    {
                        WallpaperManager.ShowPreviewForMonitor(uri, prefer);
                    }
                    else
                    {
                        WallpaperManager.ShowPreview(uri);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("预览壁纸出错", ex);
            }
        }

        private void OnPreviewForMonitorClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not MenuFlyoutItem mi) return;
                if (mi.Tag is not MonitorApplyContext ctx) return;
                var path = ctx.Item.LaunchPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                string toOpen = path;
                if (IsImageFile(path))
                {
                    toOpen = CreateImageWrapperHtml(path);
                }
                var uri = new Uri(toOpen);
                WallpaperManager.ShowPreviewForMonitor(uri, ctx.DeviceName);
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("预览到指定显示器出错", ex);
            }
        }

        private async void OnOpenLocationClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem m && m.Tag is WallpaperItem item)
            {
                var path = item.LaunchPath;
                try
                {
                    var folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        await Launcher.LaunchFolderPathAsync(folder);
                    }
                }
                catch { }
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem m && m.Tag is WallpaperItem item)
            {
                try
                {
                    var path = item.LaunchPath;
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch { }

                _items.Remove(item);
                ApplyFilter(_lastKeyword);
            }
        }

        private async System.Threading.Tasks.Task ShowErrorAsync(string title, Exception ex)
        {
            try
            {
                var dlg = new ContentDialog
                {
                    Title = title,
                    Content = new ScrollViewer
                    {
                        Content = new TextBlock { Text = ex.ToString(), TextWrapping = TextWrapping.Wrap },
                        MaxHeight = 320
                    },
                    PrimaryButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
            }
            catch { }
        }
    }
}


