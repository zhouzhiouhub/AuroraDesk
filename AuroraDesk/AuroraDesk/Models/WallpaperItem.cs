using System;

namespace AuroraDesk.Models
{
    public class WallpaperItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public string LaunchPath { get; set; } = string.Empty; // html 文件绝对路径
    }
}


