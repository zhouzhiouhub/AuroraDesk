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
using AuroraDesk.Core;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System.Text.RegularExpressions;

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

            // 初始化当前显示器编号：主显示器 = 1
            TryUpdateMonitorNumberToPrimary();
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
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".webp");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            // 自定义壁纸：为每个文件创建独立目录（以文件名区分），避免同目录仅识别一项
            var baseDir = AppContext.BaseDirectory;
            var wpRoot = Path.Combine(baseDir, "Library", "wallpapers");
            Directory.CreateDirectory(wpRoot);

            var baseName = Path.GetFileNameWithoutExtension(file.Name);
            var safeBase = SanitizeDirectoryName(baseName);
            var dirName = $"User_{safeBase}";
            var targetDir = Path.Combine(wpRoot, dirName);
            int suffix = 1;
            while (Directory.Exists(targetDir))
            {
                dirName = $"User_{safeBase}_{suffix++}";
                targetDir = Path.Combine(wpRoot, dirName);
            }
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

        private static string SanitizeDirectoryName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitizedChars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            var sanitized = new string(sanitizedChars).Trim('_', ' ', '.');
            if (string.IsNullOrEmpty(sanitized)) return "Imported";
            return sanitized;
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

        private void OnMonitorFlyoutOpening(object sender, object e)
        {
            if (sender is not MenuFlyout flyout) return;
            flyout.Items.Clear();

            var monitors = MonitorManager.GetAll();
            foreach (var m in monitors)
            {
                var orient = m.Bounds.Width >= m.Bounds.Height ? "横屏" : "竖屏";
                var label = m.Primary
                    ? $"主显示器 ({m.Bounds.Width}x{m.Bounds.Height}, {orient})"
                    : $"{m.DeviceName} ({m.Bounds.Width}x{m.Bounds.Height}, {orient})";
                var mi = new MenuFlyoutItem { Text = label };
                mi.Tag = m;
                mi.Click += OnMonitorItemClick;
                flyout.Items.Add(mi);
            }
        }

        private async void OnMonitorItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem mi) return;
            if (mi.Tag is not MonitorInfo m) return;

            var orient = m.Bounds.Width >= m.Bounds.Height ? "横屏" : "竖屏";
            var content = $"设备名: {m.DeviceName}\n主显示器: {(m.Primary ? "是" : "否")}\n分辨率: {m.Bounds.Width} x {m.Bounds.Height} ({orient})\n位置: X={m.Bounds.X}, Y={m.Bounds.Y}";
            var dlg = new ContentDialog
            {
                Title = "显示器信息",
                Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            await dlg.ShowAsync();

            // 更新按钮上的编号
            UpdateMonitorNumberFor(m);
        }

        private async void OnMonitorButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var monitors = MonitorManager.GetAll().ToList();
                if (monitors.Count == 0) return;

                // 计算显示器并排布局的缩放与偏移
                int minX = monitors.Min(m => m.Bounds.X);
                int minY = monitors.Min(m => m.Bounds.Y);
                int maxX = monitors.Max(m => m.Bounds.X + m.Bounds.Width);
                int maxY = monitors.Max(m => m.Bounds.Y + m.Bounds.Height);

                double canvasWidth = 520;
                double canvasHeight = 260;
                double unionWidth = Math.Max(1, maxX - minX);
                double unionHeight = Math.Max(1, maxY - minY);
                double scale = Math.Min(canvasWidth / unionWidth, canvasHeight / unionHeight);

                var canvas = new Canvas { Width = canvasWidth, Height = canvasHeight };

                // 背景与说明
                var root = new StackPanel { Spacing = 12 };
                root.Children.Add(canvas);

                var info = new TextBlock { Opacity = 0.7, Text = "点击方框选择显示器" };
                root.Children.Add(info);

                var dlg = new ContentDialog
                {
                    Title = "选择显示器",
                    Content = root,
                    PrimaryButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };

                // 绘制每个显示器
                var borderBrush = new SolidColorBrush(Colors.Gray);
                var primaryBrush = new SolidColorBrush(Colors.DeepSkyBlue);
                var fillBrush = new SolidColorBrush(Colors.Black) { Opacity = 0.08 };

                // 用于在点击时强调
                Border? currentSelected = null;

                var ordered = monitors
                    .OrderByDescending(m => m.Primary)
                    .ThenBy(m => m.Bounds.X)
                    .ThenBy(m => m.Bounds.Y)
                    .ToList();

                for (int i = 0; i < ordered.Count; i++)
                {
                    var m = ordered[i];
                    double left = (m.Bounds.X - minX) * scale;
                    double top = (m.Bounds.Y - minY) * scale;
                    double width = Math.Max(20, m.Bounds.Width * scale);
                    double height = Math.Max(20, m.Bounds.Height * scale);

                    var border = new Border
                    {
                        Width = width,
                        Height = height,
                        CornerRadius = new CornerRadius(6),
                        BorderThickness = new Thickness(m.Primary ? 2 : 1),
                        BorderBrush = m.Primary ? primaryBrush : borderBrush,
                        Background = fillBrush
                    };

                    var number = new TextBlock
                    {
                        Text = (TryGetDisplayIndex(m.DeviceName) ?? (i + 1)).ToString(),
                        FontSize = 18,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(6)
                    };
                    border.Child = number;

                    border.Tapped += (s, _) =>
                    {
                        currentSelected?.SetValue(Border.BorderBrushProperty, currentSelected.Tag as Brush);
                        currentSelected = (Border)s!;
                        currentSelected.Tag = currentSelected.BorderBrush;
                        currentSelected.BorderBrush = new SolidColorBrush(Colors.Black);
                        UpdateMonitorNumberFor(m);
                    };

                    Canvas.SetLeft(border, left);
                    Canvas.SetTop(border, top);
                    canvas.Children.Add(border);
                }

                await dlg.ShowAsync();
            }
            catch { }
        }

        private void TryUpdateMonitorNumberToPrimary()
        {
            try
            {
                var monitors = MonitorManager.GetAll();
                if (monitors.Count == 0) return;
                var ordered = monitors
                    .OrderByDescending(m => m.Primary)
                    .ThenBy(m => m.Bounds.X)
                    .ThenBy(m => m.Bounds.Y)
                    .ToList();
                var index = 1; // 从1开始
                var primary = ordered.FirstOrDefault();
                if (primary != null)
                {
                    var primaryIndex = TryGetDisplayIndex(primary.DeviceName) ?? (ordered.IndexOf(primary) + 1);
                    MonitorNumber.Text = primaryIndex.ToString();
                }
                else
                {
                    MonitorNumber.Text = index.ToString();
                }
            }
            catch { }
        }

        private void UpdateMonitorNumberFor(MonitorInfo selected)
        {
            try
            {
                var monitors = MonitorManager.GetAll()
                    .OrderByDescending(m => m.Primary)
                    .ThenBy(m => m.Bounds.X)
                    .ThenBy(m => m.Bounds.Y)
                    .ToList();
                var osIndex = TryGetDisplayIndex(selected.DeviceName);
                if (osIndex.HasValue)
                {
                    MonitorNumber.Text = osIndex.Value.ToString();
                    return;
                }
                var idx = monitors.FindIndex(m => m.DeviceName == selected.DeviceName);
                if (idx >= 0) MonitorNumber.Text = (idx + 1).ToString();
            }
            catch { }
        }

        private static int? TryGetDisplayIndex(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return null;
            var m = Regex.Match(deviceName, @"DISPLAY(?<n>\d+)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups["n"].Value, out var n)) return n;
            return null;
        }
    }
}
