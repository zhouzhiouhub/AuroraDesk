using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Graphics.Display;

namespace AuroraDesk.Core
{
    /// <summary>
    /// 壁纸宿主管理：负责在桌面图标后创建/管理一个挂载窗口，并可在其中切换 WebView2 壁纸源。
    /// </summary>
    public static class WallpaperManager
    {
        public static Window? HostWindow { get; private set; }
        public static WebView2? HostWebView { get; private set; }

        public static void InitializeOrAttachDesktopWallpaper(Uri? initialSource)
        {
            // 1) 找到桌面 WorkerW
            var workerw = DesktopHost.GetWorkerW();

            var w = new Window();
            var web = new WebView2();

            if (initialSource != null)
            {
                web.Source = initialSource;
            }

            w.Content = web;

            // 获取主显示器工作区，充满桌面
            var displayArea = DisplayArea.Primary;
            var workArea = displayArea.WorkArea;
            w.AppWindow.MoveAndResize(new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height));

            w.Activate();

            w.Activated += (sender, e) =>
            {
                try
                {
                    if (workerw != IntPtr.Zero)
                    {
                        Win32Window.AttachToParent(w, workerw);
                    }
                }
                catch
                {
                    // 忽略：如果挂载失败，窗口仍可显示
                }
            };

            HostWindow = w;
            HostWebView = web;
        }

        public static void SetWallpaper(Uri source)
        {
            if (HostWebView == null)
                return;

            HostWebView.Source = source;
        }
    }
}


