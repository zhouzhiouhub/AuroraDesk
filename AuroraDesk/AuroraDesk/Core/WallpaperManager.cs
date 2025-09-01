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
        private static readonly Dictionary<string, IntPtr> s_nativeHosts = new();

        // 预览窗口（不进行重父化，避免系统级句柄问题）
        private static Window? s_previewWindow;
        private static WebView2? s_previewWebView;

        // 当前首选预览显示器（由主窗口的显示器选择界面更新）
        public static string? PreferredPreviewMonitorDeviceName { get; set; }

        // 预览窗口的最大尺寸（由主窗口同步，限制预览不超过主窗口大小）
        private static int s_previewMaxWidth = 1000;
        private static int s_previewMaxHeight = 700;
        private static double s_previewDesiredScale = 1.0;

        public static void SetPreviewMaxSize(int maxWidth, int maxHeight)
        {
            if (maxWidth > 0) s_previewMaxWidth = maxWidth;
            if (maxHeight > 0) s_previewMaxHeight = maxHeight;
        }

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

            // 若已有宿主：健壮性检查；必要时重建
            if (s_hosts.TryGetValue(monitorDeviceName, out var existing))
            {
                try
                {
                    if (existing.Window == null || existing.WebView == null || existing.Window.AppWindow == null)
                    {
                        s_hosts.Remove(monitorDeviceName);
                    }
                    else
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
                }
                catch
                {
                    s_hosts.Remove(monitorDeviceName);
                }
            }

            var w = new Window();
            var web = new WebView2();

            if (w == null || web == null)
            {
                return;
            }

            // 保底：白屏时使用黑色背景，避免桌面被纯白覆盖
            try
            {
                web.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            }
            catch { }

            if (initialSource != null)
            {
                try { web.Source = initialSource; } catch { }
            }

            w.Content = web;

            // 监听 WebView2 初始化/导航失败，回退到本地黑色空页
            try
            {
                web.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess)
                    {
                        try { web.Source = new Uri("about:blank"); } catch { }
                    }
                };
                web.CoreWebView2Initialized += (s, e) =>
                {
                    try
                    {
                        if (web.CoreWebView2 == null)
                        {
                            web.Source = new Uri("about:blank");
                        }
                    }
                    catch { }
                };
            }
            catch { }

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

            // 先激活窗口，再在消息队列中延迟执行重父化，避免过早操作导致 WinUI 内部异常
            w.Activate();
            try
            {
                // 默认禁用重父化，除非显式设置 AURORADESK_DISABLE_REPARENT=0
                var disableReparent = true;
                try
                {
                    var env = Environment.GetEnvironmentVariable("AURORADESK_DISABLE_REPARENT");
                    if (!string.IsNullOrEmpty(env))
                    {
                        disableReparent = !(string.Equals(env, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(env, "false", StringComparison.OrdinalIgnoreCase));
                    }
                }
                catch { }

                if (!disableReparent)
                {
                    _ = w.DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            if (workerw != IntPtr.Zero)
                            {
                                var hostHwnd = DesktopHost.CreateChildHostOnWorkerW(out var ww, out var rc);
                                if (hostHwnd != IntPtr.Zero)
                                {
                                    s_nativeHosts[monitorDeviceName] = hostHwnd;
                                    Win32Window.AttachToParent(w, hostHwnd);
                                }
                                else
                                {
                                    Win32Window.AttachToParent(w, workerw);
                                }
                            }
                        }
                        catch { }
                    });
                }
            }
            catch { }

            // 初始加载失败保护：如果 2 秒后仍为空，则在 UI 线程重试一次导航
            try
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(2000);
                        _ = w.DispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                if (web != null && web.Source == null && initialSource != null)
                                {
                                    web.Source = initialSource;
                                }
                            }
                            catch { }
                        });
                    }
                    catch { }
                });
            }
            catch { }

            try { s_hosts[monitorDeviceName] = (w, web); } catch { }

            // 更新单窗口兼容引用（主显示器时）
            if (monitor != null && monitor.Primary)
            {
                HostWindow = w;
                HostWebView = web;
            }
        }

        /// <summary>
        /// 关闭并清理所有壁纸宿主窗口与预览窗口。
        /// </summary>
        public static void CloseAll()
        {
            try
            {
                // 关闭预览
                try { s_previewWindow?.Close(); } catch { }
                s_previewWebView = null;
                s_previewWindow = null;

                // 关闭各显示器的宿主窗口
                foreach (var kv in s_hosts.Values)
                {
                    try
                    {
                        try { Win32Window.DetachFromParent(kv.Window); } catch { }
                        kv.Window.Close();
                    }
                    catch { }
                }
                s_hosts.Clear();
                // 尝试销毁本地宿主窗口
                try
                {
                    foreach (var h in s_nativeHosts.Values)
                    {
                        try { NativeMethods.DestroyWindow(h); } catch { }
                    }
                }
                catch { }
                s_nativeHosts.Clear();

                HostWebView = null;
                HostWindow = null;
            }
            catch { }
        }

        /// <summary>
        /// 显示安全预览窗口：普通 WinUI 窗口承载 WebView2，不挂载到 WorkerW。
        /// 再次调用会复用窗口并切换 Source。
        /// </summary>
        public static void ShowPreview(Uri? initialSource)
        {
            // 复用已有窗口
            if (s_previewWindow != null && s_previewWebView != null)
            {
                if (initialSource != null)
                {
                    s_previewWebView.Source = initialSource;
                }
                // 重算缩放：使用首选或主显示器
                try
                {
                    var monitors = MonitorManager.GetAll();
                    var target = (!string.IsNullOrEmpty(PreferredPreviewMonitorDeviceName)
                        ? monitors.FirstOrDefault(m => m.DeviceName == PreferredPreviewMonitorDeviceName)
                        : null) ?? monitors.FirstOrDefault(m => m.Primary) ?? monitors.FirstOrDefault();
                    if (target != null)
                    {
                        var size = s_previewWindow.AppWindow.Size;
                        var scale = Math.Min(
                            Math.Max(0.1, (double)size.Width / Math.Max(1, target.Bounds.Width)),
                            Math.Max(0.1, (double)size.Height / Math.Max(1, target.Bounds.Height))
                        );
                        s_previewDesiredScale = Math.Min(1.0, scale);
                        TryApplyPreviewScale(s_previewWebView, s_previewDesiredScale, size.Width, size.Height, target.Bounds.Width, target.Bounds.Height);
                    }
                }
                catch { }
                s_previewWindow.Activate();
                return;
            }

            var w = new Window();
            var web = new WebView2();
            w.Title = "AuroraDesk 预览";
            try
            {
                // 使用与主窗口相同的图标（若存在）
                var baseDir = AppContext.BaseDirectory;
                var iconPath = System.IO.Path.Combine(baseDir, "Assets", "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    w.AppWindow.SetIcon(iconPath);
                }
            }
            catch { }
            if (initialSource != null)
            {
                web.Source = initialSource;
            }
            w.Content = web;

            // 合理的默认大小并居中到主显示器工作区
            try
            {
                var work = Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea;
                int width = Math.Max(640, work.Width * 3 / 5);
                int height = Math.Max(400, work.Height * 3 / 5);

                // 比例缩小，限制不超过主窗口
                double scale = Math.Min(1.0,
                    Math.Min((double)s_previewMaxWidth / Math.Max(1, width),
                             (double)s_previewMaxHeight / Math.Max(1, height)));
                width = (int)Math.Max(320, width * scale);
                height = (int)Math.Max(240, height * scale);
                int x = work.X + (work.Width - width) / 2;
                int y = work.Y + (work.Height - height) / 2;
                w.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));

                // 将内容按显示器分辨率比例缩小
                try
                {
                    var target = MonitorManager.GetAll().FirstOrDefault(m => m.Primary) ?? MonitorManager.GetAll().FirstOrDefault();
                    if (target != null)
                    {
                        var contentScale = Math.Min(
                            Math.Max(0.1, (double)width / Math.Max(1, target.Bounds.Width)),
                            Math.Max(0.1, (double)height / Math.Max(1, target.Bounds.Height))
                        );
                        s_previewDesiredScale = Math.Min(1.0, contentScale);
                        TryApplyPreviewScale(web, s_previewDesiredScale, width, height, target.Bounds.Width, target.Bounds.Height);
                    }
                }
                catch { }
            }
            catch { }

            w.Closed += (_, __) =>
            {
                s_previewWebView = null;
                s_previewWindow = null;
            };

            w.Activate();
            s_previewWindow = w;
            s_previewWebView = web;

            // 若 CoreWebView2 尚未就绪，初始化后再应用缩放
            try
            {
                var size = s_previewWindow.AppWindow.Size;
                var target = MonitorManager.GetAll().FirstOrDefault(m => m.Primary) ?? MonitorManager.GetAll().FirstOrDefault();
                if (target != null)
                {
                    TryApplyPreviewScale(s_previewWebView, s_previewDesiredScale, size.Width, size.Height, target.Bounds.Width, target.Bounds.Height);
                }
            }
            catch { }
        }

        /// <summary>
        /// 关闭预览窗口（若存在）。
        /// </summary>
        public static void ClosePreview()
        {
            try
            {
                s_previewWindow?.Close();
            }
            catch { }
            finally
            {
                s_previewWebView = null;
                s_previewWindow = null;
            }
        }

        /// <summary>
        /// 在指定显示器上展示预览窗口（按显示器工作区居中，缩放到合适大小）。
        /// </summary>
        public static void ShowPreviewForMonitor(Uri? source, string monitorDeviceName)
        {
            ShowPreview(source);
            try
            {
                var monitor = MonitorManager.GetAll().FirstOrDefault(m => m.DeviceName == monitorDeviceName);
                if (monitor == null || s_previewWindow == null) return;

                var b = monitor.Bounds;
                int width = Math.Max(400, b.Width * 3 / 5);
                int height = Math.Max(300, b.Height * 3 / 5);

                // 比例缩小，限制不超过主窗口
                double scale = Math.Min(1.0,
                    Math.Min((double)s_previewMaxWidth / Math.Max(1, width),
                             (double)s_previewMaxHeight / Math.Max(1, height)));
                width = (int)Math.Max(320, width * scale);
                height = (int)Math.Max(240, height * scale);
                int x = b.X + (b.Width - width) / 2;
                int y = b.Y + (b.Height - height) / 2;
                s_previewWindow.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));

                // 缩放页面内容与显示器分辨率成比例
                try
                {
                    s_previewDesiredScale = Math.Min(1.0, Math.Min(
                        Math.Max(0.1, (double)width / Math.Max(1, b.Width)),
                        Math.Max(0.1, (double)height / Math.Max(1, b.Height))));
                    if (s_previewWebView != null)
                    {
                        TryApplyPreviewScale(s_previewWebView, s_previewDesiredScale, width, height, b.Width, b.Height);
                    }
                }
                catch { }

                // 更新标题为“预览 - 设备名 (分辨率)”
                var orient = b.Width >= b.Height ? "横屏" : "竖屏";
                s_previewWindow.Title = $"AuroraDesk 预览 - {monitor.DeviceName} ({b.Width}x{b.Height}, {orient})";
                s_previewWindow.AppWindow.Title = s_previewWindow.Title;
            }
            catch { }
        }

        private static void TryApplyPreviewScale(WebView2 web, double scale, int previewWidth, int previewHeight, int basisWidth, int basisHeight)
        {
            if (web == null) return;
            try
            {
                async void Apply()
                {
                    // 计算偏移，使缩放后的内容在预览窗口内居中
                    double offsetX = Math.Max(0, (previewWidth - previewWidth * scale) / 2.0);
                    double offsetY = Math.Max(0, (previewHeight - previewHeight * scale) / 2.0);
                    string sx = offsetX.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string sy = offsetY.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string k = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var css = "html,body{margin:0;padding:0;height:100%;overflow:hidden;background:#000;} " +
                              "html{transform-origin:0 0;transform:translate(" + sx + "px," + sy + "px) scale(" + k + ");}";
                    var js = "(function(){try{var s=document.getElementById('auroradesk_preview_scale');if(!s){s=document.createElement('style');s.id='auroradesk_preview_scale';document.documentElement.appendChild(s);}s.textContent='" + css.Replace("\\", "\\\\").Replace("'", "\\'") + "';}catch(e){}})();";
                    try { await web.ExecuteScriptAsync(js); } catch { }
                }

                if (web.CoreWebView2 != null)
                {
                    _ = web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("(function(){try{var s=document.getElementById('auroradesk_preview_scale');if(!s){s=document.createElement('style');s.id='auroradesk_preview_scale';document.documentElement.appendChild(s);} }catch(e){}})();");
                    Apply();
                }
                else
                {
                    web.CoreWebView2Initialized += (s, e) =>
                    {
                        try
                        {
                            _ = web.CoreWebView2!.AddScriptToExecuteOnDocumentCreatedAsync("(function(){try{var s=document.getElementById('auroradesk_preview_scale');if(!s){s=document.createElement('style');s.id='auroradesk_preview_scale';document.documentElement.appendChild(s);} }catch(e){}})();");
                            Apply();
                        }
                        catch { }
                    };
                }
            }
            catch { }
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
                try { if (kv.WebView != null) kv.WebView.Source = source; } catch { }
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


