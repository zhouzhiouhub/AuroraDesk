using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AuroraDesk.Core;
using AuroraDesk.Models;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AuroraDesk
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly List<WallpaperItem> _items = new();
        private static readonly string[] ImageExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
        private static bool IsImageFile(string path) => ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        private static bool IsHtmlFile(string path) { var ext = Path.GetExtension(path).ToLowerInvariant(); return ext == ".html" || ext == ".htm"; }

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            // 固定初始大小 800x600
            Win32Window.SetSize(this, 800, 600);

            // 正常显示（可按需改成最大化/最小化）
            Win32Window.Show(this, NativeMethods.SW_SHOWNORMAL);

            // 设置窗口左上角图标为项目 Logo（优先 .ico，回退 .png）
            TrySetWindowIcon();

            LoadWallpapers();
            WallpaperList.ItemsSource = _items;
        }

        // 示例：最大化窗口
        private void Maximize()
        {
            Win32Window.Show(this, NativeMethods.SW_SHOWMAXIMIZED);
        }

        private void TrySetWindowIcon()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidates = new string[]
            {
                Path.Combine(baseDir, "Assets", "AppIcon.ico"),
                Path.Combine(baseDir, "Assets", "StoreLogo.scale-200.png"),
                Path.Combine(baseDir, "Assets", "Square44x44Logo.scale-200.png"),
                Path.Combine(baseDir, "Assets", "Square44x44Logo.scale-100.png"),
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    this.AppWindow.SetIcon(path);
                    break;
                }
            }
        }

        // 示例：最小化窗口
        private void Minimize()
        {
            Win32Window.Show(this, NativeMethods.SW_SHOWMINIMIZED);
        }

        private void LoadWallpapers()
        {
            var baseDir = AppContext.BaseDirectory;
            var wpRoot = Path.Combine(baseDir, "Library", "wallpapers");
            if (!Directory.Exists(wpRoot)) return;

            // 1) 扫描子目录（支持 LivelyInfo、html、图片/gif）
            foreach (var dir in Directory.GetDirectories(wpRoot))
            {
                // 优先支持 LivelyInfo.json
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

                // 回退A：寻找*.html
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

                // 回退B：寻找图片/gif
                var firstImage = Directory.EnumerateFiles(dir)
                    .FirstOrDefault(f => IsImageFile(f));
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

            // 2) 扫描根目录下的文件（html、gif、图片）
            foreach (var file in Directory.GetFiles(wpRoot))
            {
                if (!(IsHtmlFile(file) || IsImageFile(file))) continue;

                var title = Path.GetFileNameWithoutExtension(file);
                var thumb = IsImageFile(file) ? file : Path.Combine(baseDir, "Library", "wallpapers", "static", "gral.png");
                if (!File.Exists(thumb)) thumb = file; // 兜底

                _items.Add(new WallpaperItem
                {
                    Title = title,
                    Description = string.Empty,
                    ThumbnailPath = thumb,
                    LaunchPath = file
                });
            }
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

        private static string? FirstExisting(params string[] paths)
        {
            foreach (var p in paths)
            {
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string CreateImageWrapperHtml(string imagePath)
        {
            var safeName = Path.GetFileName(imagePath);
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
