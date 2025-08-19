using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Graphics.Display;
using AuroraDesk.Core;

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
            try
            {
                // 1) 找到桌面 WorkerW
                var workerw = DesktopHost.GetWorkerW();
                
                if (workerw == IntPtr.Zero)
                {
                    // 如果找不到 WorkerW，创建普通窗口
                    CreateNormalWindow();
                    return;
                }

                // 2) 创建一个 WinUI 窗口作为"壁纸"
                var w = new Window();

                // 占位：WebView2 作为网页壁纸（先验证运行链路）
                var web = new WebView2();
                web.Source = new Uri("https://www.bing.com");

                w.Content = web;

                // 获取主显示器的尺寸
                var displayArea = DisplayArea.Primary;
                var workArea = displayArea.WorkArea;
                
                // 设置窗口位置和大小
                w.AppWindow.MoveAndResize(new RectInt32(workArea.X, workArea.Y, workArea.Width, workArea.Height));
                
                // 先激活窗口，确保窗口句柄已创建
                w.Activate();

                // 等待窗口完全加载后再进行 Win32 操作
                w.Activated += (sender, e) =>
                {
                    try
                    {
                        // 3) 把窗口挂到桌面图标背后
                        Win32Window.AttachToParent(w, workerw);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to attach to desktop: {ex.Message}");
                        // 如果挂载失败，窗口仍然可以正常显示
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnLaunched: {ex.Message}");
                // 发生错误时创建普通窗口
                CreateNormalWindow();
            }

            // 若还想显示配置窗口：取消下一行注释
            // new MainWindow().Activate();
        }

        private void CreateNormalWindow()
        {
            var w = new Window();
            var web = new WebView2();
            web.Source = new Uri("https://www.bing.com");
            w.Content = web;
            w.Activate();
        }
    }
}
