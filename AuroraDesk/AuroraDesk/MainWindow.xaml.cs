using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AuroraDesk.Core;
using AuroraDesk.Models;
using System.Linq;
using AuroraDesk.Pages;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AuroraDesk
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private GalleryPage? _galleryPage;

        public MainWindow()
        {
            InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            Win32Window.SetSize(this, 900, 640);
            Win32Window.Show(this, NativeMethods.SW_SHOWNORMAL);
            TrySetWindowIcon();

            // 默认进入图库页
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().First(i => (string)i.Tag == "Gallery");
            NavigateTo("Gallery");
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

        // MainWindow 不再直接加载壁纸（改由 GalleryPage）。

        // ======= 事件：导航/搜索 =======
        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = (string)item.Tag;
                NavigateTo(tag);
            }
        }

        private void NavigateTo(string tag)
        {
            // 搜索框仅对图库有效：非图库时禁用并清空
            var isGallery = tag == "Gallery";
            SearchBox.IsEnabled = isGallery;
            if (!isGallery) SearchBox.Text = string.Empty;

            if (tag == "Gallery")
            {
                _galleryPage ??= new GalleryPage();
                ContentFrame.Content = _galleryPage;
            }
            else if (tag == "Settings")
            {
                ContentFrame.Content = CreateSettingsPlaceholder();
            }
            else if (tag == "Help")
            {
                ContentFrame.Content = CreateHelpPlaceholder();
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchBox.Text);
        }

        private void ApplyFilter(string? keyword)
        {
            _galleryPage?.ApplyFilter(keyword);
        }

        // ======= UI 构造（简单占位） =======

        private FrameworkElement CreateSettingsPlaceholder()
        {
            return new TextBlock { Text = "设置（规划）：全屏暂停、帧率上限、自启动等", Opacity = 0.8, Margin = new Thickness(16) };
        }

        private FrameworkElement CreateHelpPlaceholder()
        {
            return new TextBlock { Text = "帮助：快速上手 / 常见问题 / 日志位置", Opacity = 0.8, Margin = new Thickness(16) };
        }

        private async void OnAddWallpaperClicked(object sender, RoutedEventArgs e)
        {
            if (!SearchBox.IsEnabled)
            {
                // 非图库页也允许添加，但优先跳到图库页
                NavigateTo("Gallery");
            }

            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".html");
            picker.FileTypeFilter.Add(".htm");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".gif");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            // 简单处理：复制到 Library/wallpapers/Downloads 下并刷新
            var baseDir = AppContext.BaseDirectory;
            var targetDir = Path.Combine(baseDir, "Library", "wallpapers", "Downloads");
            Directory.CreateDirectory(targetDir);
            var targetPath = Path.Combine(targetDir, file.Name);
            using (var src = await file.OpenStreamForReadAsync())
            using (var dst = File.Create(targetPath))
            {
                await src.CopyToAsync(dst);
            }

            // 让图库刷新（重新创建页面或触发其内部重载）
            _galleryPage = new GalleryPage();
            ContentFrame.Content = _galleryPage;
        }

        // 壁纸点击处理已在 GalleryPage 中实现

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
