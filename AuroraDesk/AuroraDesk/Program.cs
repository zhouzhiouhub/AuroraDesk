using System;
using Microsoft.UI.Xaml;
using System.Runtime.Versioning;

namespace AuroraDesk
{
    public static class Program
    {
        [STAThread]
        [SupportedOSPlatform("windows10.0.17763")]
        static void Main(string[] args)
        {
            try
            {
                // Workaround: mitigate GPU/driver related crashes
                try { 
                    Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--disable-gpu --enable-features=SharedArrayBuffer,NetworkServiceInProcess"); 
                    } 
                catch { }
                // 默认禁用将壁纸窗口挂到桌面（避免潜在崩溃）。如需开启可传入 --reparent 或预先设置环境变量。
                try
                {
                    var existing = Environment.GetEnvironmentVariable("AURORADESK_DISABLE_REPARENT");
                    bool enableReparentArg = false;
                    try
                    {
                        if (args != null)
                        {
                            foreach (var a in args)
                            {
                                if (string.Equals(a, "--reparent", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/reparent", StringComparison.OrdinalIgnoreCase))
                                {
                                    enableReparentArg = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                    if (string.IsNullOrEmpty(existing))
                    {
                        // 未显式设置时：默认禁用；仅在传入 --reparent 时启用
                        Environment.SetEnvironmentVariable("AURORADESK_DISABLE_REPARENT", enableReparentArg ? "0" : "1");
                    }
                }
                catch { }
                // Initialize WinRT COM wrappers early (some environments need this to avoid native crashes)
                try { global::WinRT.ComWrappersSupport.InitializeComWrappers(); } catch { }
                Application.Start(p => { new App(); });
            }
            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == -2147221164) // REGDB_E_CLASSNOTREG
            {
                ShowErrorDialog(
                    "运行时组件缺失",
                    "应用程序无法启动，因为缺少必要的Windows运行时组件。\n\n" +
                    "请尝试以下解决方案：\n" +
                    "1. 安装最新的 Microsoft Visual C++ Redistributable\n" +
                    "2. 安装 Windows App SDK Runtime\n" +
                    "3. 确保Windows系统已更新到最新版本\n\n" +
                    $"错误代码: {ex.HResult:X} ({ex.Message})"
                );
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                ShowErrorDialog(
                    "应用程序启动失败",
                    $"应用程序启动时遇到错误：\n\n{ex.Message}\n\n" +
                    "请联系开发者获取支持。"
                );
                Environment.Exit(1);
            }
        }

        private static void ShowErrorDialog(string title, string message)
        {
            try
            {
                System.Windows.Forms.MessageBox.Show(
                    message,
                    title,
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
            }
            catch
            {
                try
                {
                    Console.WriteLine($"{title}: {message}");
                    Console.ReadKey();
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine($"{title}: {message}");
                }
            }
        }
    }
}