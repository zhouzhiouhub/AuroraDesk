using System;
using System.IO;
using System.Runtime.Versioning;

namespace AuroraDesk.Utilities
{
    /// <summary>
    /// Helper for setting system desktop wallpaper for image files via SystemParametersInfo.
    /// </summary>
    internal static class SystemWallpaper
    {
        private static readonly string[] s_directSupport = new[] { ".bmp", ".jpg", ".jpeg", ".png" };

        [SupportedOSPlatform("windows")] 
        public static bool TrySet(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return false;

            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            try
            {
                string toApply = imagePath;
                if (ext == ".bmp")
                {
                    // use directly
                }
                else if (Array.IndexOf(s_directSupport, ext) >= 0)
                {
                    // Most modern Windows accept JPG/PNG. Try directly first.
                }
                else
                {
                    // Fallback: convert to BMP (skip formats we can't decode)
#pragma warning disable CA1416
                    try
                    {
                        if (ext == ".gif" || ext == ".webp")
                        {
                            return false;
                        }
                        using (var img = System.Drawing.Image.FromFile(imagePath))
                        {
                            var tmpDir = Path.Combine(Path.GetTempPath(), "AuroraDesk", "wallpaper");
                            Directory.CreateDirectory(tmpDir);
                            var bmpPath = Path.Combine(tmpDir, "wallpaper.bmp");
                            img.Save(bmpPath, System.Drawing.Imaging.ImageFormat.Bmp);
                            toApply = bmpPath;
                        }
                    }
                    catch
                    {
                        return false;
                    }
#pragma warning restore CA1416
                }

                uint flags = Core.NativeMethods.SPIF_UPDATEINIFILE | Core.NativeMethods.SPIF_SENDCHANGE;
                bool ok = Core.NativeMethods.SystemParametersInfo(Core.NativeMethods.SPI_SETDESKWALLPAPER, 0, toApply, flags);
                return ok;
            }
            catch
            {
                return false;
            }
        }
    }
}


