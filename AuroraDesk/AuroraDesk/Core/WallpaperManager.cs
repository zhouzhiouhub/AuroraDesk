using System;
using System.Collections.Generic;
using System.Linq;
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
        // 兼容单窗口（主显示器）旧接口
        public static Window? HostWindow { get; private set; }
        public static WebView2? HostWebView { get; private set; }

        // 多显示器：按设备名管理宿主
        private static readonly Dictionary<string, (Window Window, WebView2 WebView)> s_hosts = new();

        public static void InitializeOrAttachDesktopWallpaper(Uri? initialSource)
        {
            // 仍然以主显示器为目标，保持旧行为
            var all = MonitorManager.GetAll();
            var primary = all.FirstOrDefault(m => m.Primary) ?? all.FirstOrDefault();
            if (primary == null)
            {
                return;
            }
            InitializeOrAttachForMonitor(initialSource, primary.DeviceName);
            if (s_hosts.TryGetValue(primary.DeviceName, out var host))
            {
                HostWindow = host.Window;
                HostWebView = host.WebView;
            }
        }

        public static void InitializeOrAttachForAllMonitors(Uri? initialSource)
        {
            var monitors = MonitorManager.GetAll();
            foreach (var m in monitors)
            {
                InitializeOrAttachForMonitor(initialSource, m.DeviceName);
            }
        }

        public static void InitializeOrAttachForMonitor(Uri? initialSource, string monitorDeviceName)
        {
            if (string.IsNullOrWhiteSpace(monitorDeviceName)) return;

            // 1) 找到桌面 WorkerW
            var workerw = DesktopHost.GetWorkerW();

            // 若已有宿主，仅切换源并同步尺寸
            if (s_hosts.TryGetValue(monitorDeviceName, out var existing))
            {
                if (initialSource != null)
                {
                    existing.WebView.Source = initialSource;
                }
                var mi = MonitorManager.GetAll().FirstOrDefault(m => m.DeviceName == monitorDeviceName);
                if (mi != null)
                {
                    existing.Window.AppWindow.MoveAndResize(mi.Bounds);
                }
                return;
            }

            var w = new Window();
            var web = new WebView2();

            if (initialSource != null)
            {
                web.Source = initialSource;
            }

            w.Content = web;

            // 定位到目标显示器的完整区域
            var monitor = MonitorManager.GetAll().FirstOrDefault(m => m.DeviceName == monitorDeviceName);
            if (monitor != null)
            {
                var b = monitor.Bounds;
                w.AppWindow.MoveAndResize(new RectInt32(b.X, b.Y, b.Width, b.Height));
            }
            else
            {
                // 回退到主显示器工作区
                var displayArea = DisplayArea.Primary;
                var workArea = displayArea.WorkArea;
                w.AppWindow.MoveAndResize(new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height));
            }

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

            w.Activate();

            s_hosts[monitorDeviceName] = (w, web);

            // 更新单窗口兼容引用（主显示器时）
            if (monitor != null && monitor.Primary)
            {
                HostWindow = w;
                HostWebView = web;
            }
        }

        public static void SetWallpaper(Uri source)
        {
            if (HostWebView == null)
                return;

            HostWebView.Source = source;
        }

        public static void SetWallpaperForAll(Uri source)
        {
            foreach (var kv in s_hosts.Values)
            {
                kv.WebView.Source = source;
            }
        }

        public static void SetWallpaperForMonitor(string monitorDeviceName, Uri source)
        {
            if (s_hosts.TryGetValue(monitorDeviceName, out var host))
            {
                host.WebView.Source = source;
            }
            else
            {
                InitializeOrAttachForMonitor(source, monitorDeviceName);
            }
        }
    }
}


