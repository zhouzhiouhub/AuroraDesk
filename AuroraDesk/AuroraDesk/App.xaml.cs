using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Graphics.Display;
using AuroraDesk.Core;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AuroraDesk
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // 直接展示画廊窗口，由用户点击后再挂载壁纸
            new MainWindow().Activate();
        }

        private void CreateNormalWindow()
        {
            var w = new Window();
            var web = new WebView2();

            var baseDir = AppContext.BaseDirectory;
            var htmlPath = Path.Combine(baseDir, "Library", "wallpapers", "Eyes", "eyes.html");
            if (!File.Exists(htmlPath))
            {
                htmlPath = Path.Combine(baseDir, "Library", "wallpapers", "0wj1biqk.f41", "index.html");
            }
            if (!File.Exists(htmlPath))
            {
                htmlPath = Path.Combine(baseDir, "Library", "wallpapers", "Rain", "rains.html");
            }

            web.Source = new Uri(htmlPath);
            w.Content = web;
            w.Activate();
        }
    }
}
