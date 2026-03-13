using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using AuroraDesk.Application.Services;
using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;
using AuroraDesk.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace AuroraDesk.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public enum LibraryScope
    {
        BuiltIn,
        User,
    }

    private readonly WallpaperService _wallpaperService;
    private readonly IWallpaperLibrary _wallpaperLibrary;
    private readonly ILogger<MainViewModel> _logger;
    private readonly List<WallpaperThumbnailItem> _builtInLibraryItems = [];
    private readonly List<WallpaperThumbnailItem> _userLibraryItems = [];

    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

    private static readonly HashSet<string> SupportedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mkv", ".mov", ".avi" };

    private static readonly HashSet<string> SupportedHtmlExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".html", ".htm" };

    private static readonly HashSet<string> SupportedExtensions = new(
        SupportedImageExtensions
            .Concat(SupportedVideoExtensions)
            .Concat(SupportedHtmlExtensions),
        StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<MonitorDisplayItem> Monitors { get; } = new();
    public ObservableCollection<WallpaperThumbnailItem> LibraryItems { get; } = new();

    [ObservableProperty]
    private MonitorDisplayItem? selectedMonitor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyWallpaperCommand))]
    private string? selectedImagePath;

    [ObservableProperty]
    private WallpaperType selectedWallpaperType = WallpaperType.Image;

    [ObservableProperty]
    private ScaleMode selectedScaleMode = ScaleMode.Fill;

    [ObservableProperty]
    private string statusText = "就绪";

    [ObservableProperty]
    private bool isWallpaperActive;

    [ObservableProperty]
    private BitmapImage? previewImage;

    [ObservableProperty]
    private bool isApplying;

    [ObservableProperty]
    private bool isImporting;

    [ObservableProperty]
    private WallpaperThumbnailItem? selectedLibraryItem;

    [ObservableProperty]
    private LibraryScope selectedLibraryScope = LibraryScope.BuiltIn;

    public ScaleMode[] AvailableScaleModes { get; } =
        [ScaleMode.Fill, ScaleMode.Fit, ScaleMode.Stretch, ScaleMode.Center];

    public string? SelectedFileName => string.IsNullOrEmpty(SelectedImagePath)
        ? null
        : Path.GetFileName(SelectedImagePath);

    public string SelectedWallpaperTypeText => SelectedWallpaperType switch
    {
        WallpaperType.Image => "图片",
        WallpaperType.Video => "视频",
        WallpaperType.Html => "HTML",
        _ => "未知",
    };

    public string PreviewPlaceholderTitle => string.IsNullOrEmpty(SelectedImagePath)
        ? "未选择壁纸"
        : "当前无法预览";

    public string PreviewPlaceholderHint => string.IsNullOrEmpty(SelectedImagePath)
        ? "选择壁纸后，点击应用到显示器"
        : SelectedWallpaperType switch
        {
            WallpaperType.Video => "当前版本暂不在此区域渲染视频预览",
            WallpaperType.Html => "当前版本暂不在此区域渲染 HTML 预览",
            _ => "选择壁纸后，点击应用到显示器",
        };

    public bool IsBuiltInLibrarySelected => SelectedLibraryScope == LibraryScope.BuiltIn;
    public bool IsUserLibrarySelected => SelectedLibraryScope == LibraryScope.User;

    public string LibraryDescription => IsBuiltInLibrarySelected
        ? "AuroraDesk 内置壁纸库"
        : "导入并管理你的自定义壁纸";

    public string EmptyLibraryTitle => IsBuiltInLibrarySelected
        ? "内置壁纸库为空"
        : "自定义壁纸库为空";

    public string EmptyLibraryHint => IsBuiltInLibrarySelected
        ? "将资源放入 Library/wallpapers 后可在此显示"
        : "导入文件或文件夹后即可开始使用";

    public string ScaleModeDescription => SelectedScaleMode switch
    {
        ScaleMode.Fill => "铺满屏幕，按比例裁切边缘避免黑边",
        ScaleMode.Fit => "完整显示图片，比例不一致时保留黑边",
        ScaleMode.Stretch => "拉伸到完全填满，可能产生形变",
        ScaleMode.Center => "按原始尺寸居中显示",
        _ => ""
    };

    public Stretch PreviewStretch => SelectedScaleMode switch
    {
        ScaleMode.Fill => Stretch.UniformToFill,
        ScaleMode.Fit => Stretch.Uniform,
        ScaleMode.Stretch => Stretch.Fill,
        ScaleMode.Center => Stretch.None,
        _ => Stretch.UniformToFill,
    };

    public string SelectedMonitorInfo
    {
        get
        {
            if (SelectedMonitor is null) return "未选择显示器";
            var p = SelectedMonitor.Profile;
            var label = p.IsPrimary ? "主显示器" : $"显示器 {SelectedMonitor.Index}";
            return $"{label}  {p.Bounds.Width} × {p.Bounds.Height}";
        }
    }

    public MainViewModel(
        WallpaperService wallpaperService,
        IWallpaperLibrary wallpaperLibrary,
        ILogger<MainViewModel> logger)
    {
        _wallpaperService = wallpaperService;
        _wallpaperLibrary = wallpaperLibrary;
        _logger = logger;
        LoadMonitors();
        LoadLibrary();
    }

    partial void OnSelectedImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(SelectedFileName));
        OnPropertyChanged(nameof(PreviewPlaceholderTitle));
        OnPropertyChanged(nameof(PreviewPlaceholderHint));
        if (!string.IsNullOrWhiteSpace(value))
        {
            SelectedWallpaperType = DetectWallpaperType(value);
        }
    }

    partial void OnSelectedWallpaperTypeChanged(WallpaperType value)
    {
        OnPropertyChanged(nameof(SelectedWallpaperTypeText));
        OnPropertyChanged(nameof(PreviewPlaceholderHint));
    }

    partial void OnSelectedScaleModeChanged(ScaleMode value)
    {
        OnPropertyChanged(nameof(ScaleModeDescription));
        OnPropertyChanged(nameof(PreviewStretch));
    }

    partial void OnSelectedLibraryScopeChanged(LibraryScope value)
    {
        OnPropertyChanged(nameof(IsBuiltInLibrarySelected));
        OnPropertyChanged(nameof(IsUserLibrarySelected));
        OnPropertyChanged(nameof(LibraryDescription));
        OnPropertyChanged(nameof(EmptyLibraryTitle));
        OnPropertyChanged(nameof(EmptyLibraryHint));
        RefreshActiveLibraryItems();
        ClearLibrarySelection();
    }

    partial void OnSelectedMonitorChanged(MonitorDisplayItem? oldValue, MonitorDisplayItem? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;

        OnPropertyChanged(nameof(SelectedMonitorInfo));
        LoadStateForSelectedMonitor();
    }

    public void SelectMonitor(MonitorDisplayItem item)
    {
        SelectedMonitor = item;
    }

    public void LoadMonitors()
    {
        const double targetWidth = 320;
        const double targetHeight = 100;

        var profiles = _wallpaperService.GetAllMonitors();
        if (profiles.Count == 0) return;

        int minX = profiles.Min(p => p.Bounds.X);
        int minY = profiles.Min(p => p.Bounds.Y);
        int maxX = profiles.Max(p => p.Bounds.X + p.Bounds.Width);
        int maxY = profiles.Max(p => p.Bounds.Y + p.Bounds.Height);

        double totalW = maxX - minX;
        double totalH = maxY - minY;
        double scale = Math.Min(targetWidth / totalW, targetHeight / totalH) * 0.9;

        double offsetX = (targetWidth - totalW * scale) / 2;
        double offsetY = (targetHeight - totalH * scale) / 2;

        Monitors.Clear();
        int index = 1;
        foreach (var profile in profiles)
        {
            var item = new MonitorDisplayItem(
                profile, index++,
                (profile.Bounds.X - minX) * scale + offsetX,
                (profile.Bounds.Y - minY) * scale + offsetY,
                profile.Bounds.Width * scale,
                profile.Bounds.Height * scale);

            item.HasWallpaper = _wallpaperService.IsMonitorActive(profile.MonitorId);
            Monitors.Add(item);
        }

        SelectedMonitor = Monitors.FirstOrDefault(m => m.IsPrimary) ?? Monitors.FirstOrDefault();
    }

    private void LoadStateForSelectedMonitor()
    {
        if (SelectedMonitor is null) return;

        var assignment = _wallpaperService.GetAssignment(SelectedMonitor.Profile.MonitorId);
        if (assignment.HasValue)
        {
            SelectedImagePath = assignment.Value.Path;
            SelectedScaleMode = assignment.Value.Mode;
            SelectedWallpaperType = assignment.Value.Type;
            IsWallpaperActive = true;
            LoadPreview(assignment.Value.Path, assignment.Value.Type);
            UpdateStatus();
        }
        else
        {
            SelectedImagePath = null;
            SelectedScaleMode = ScaleMode.Fill;
            IsWallpaperActive = false;
            PreviewImage = null;
            StatusText = "就绪";
        }
    }

    [RelayCommand]
    private void SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择壁纸文件",
            Filter = "壁纸文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.mp4;*.webm;*.mkv;*.mov;*.avi;*.html;*.htm|图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|视频文件|*.mp4;*.webm;*.mkv;*.mov;*.avi|HTML 文件|*.html;*.htm|所有文件|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedImagePath = dialog.FileName;
            LoadPreview(dialog.FileName, DetectWallpaperType(dialog.FileName));
            ClearLibrarySelection();
        }
    }

    [RelayCommand]
    private void ShowBuiltInLibrary()
    {
        SelectedLibraryScope = LibraryScope.BuiltIn;
    }

    [RelayCommand]
    private void ShowUserLibrary()
    {
        SelectedLibraryScope = LibraryScope.User;
    }

    [RelayCommand]
    private async Task ImportToLibrary()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入壁纸到资源库",
            Filter = "壁纸文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.mp4;*.webm;*.mkv;*.mov;*.avi;*.html;*.htm|所有文件|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0) return;

        IsImporting = true;
        StatusText = "正在导入壁纸...";

        try
        {
            var newItems = new List<WallpaperItem>();

            await Task.Run(() =>
            {
                foreach (var file in dialog.FileNames)
                {
                    if (_wallpaperLibrary.ExistsByPath(file)) continue;
                    if (!SupportedExtensions.Contains(Path.GetExtension(file))) continue;

                    var item = CreateWallpaperItem(file);
                    if (item is not null) newItems.Add(item);
                }

                if (newItems.Count > 0)
                    _wallpaperLibrary.AddRange(newItems);
            });

            foreach (var item in newItems)
            {
                var thumbItem = new WallpaperThumbnailItem(item, isBuiltIn: false);
                thumbItem.LoadThumbnail();
                _userLibraryItems.Add(thumbItem);
                if (IsUserLibrarySelected)
                    LibraryItems.Add(thumbItem);
            }

            StatusText = newItems.Count > 0
                ? $"已导入 {newItems.Count} 个壁纸"
                : "没有可导入的新壁纸（已跳过重复项）";
        }
        catch (Exception ex)
        {
            StatusText = $"导入失败：{ex.Message}";
            _logger.LogError(ex, "Failed to import wallpapers");
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private async Task ImportFolderToLibrary()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要扫描壁纸的文件夹",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        IsImporting = true;
        StatusText = "正在扫描文件夹...";

        try
        {
            var newItems = new List<WallpaperItem>();

            await Task.Run(() =>
            {
                var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

                foreach (var file in files)
                {
                    if (_wallpaperLibrary.ExistsByPath(file)) continue;

                    var item = CreateWallpaperItem(file);
                    if (item is not null) newItems.Add(item);
                }

                if (newItems.Count > 0)
                    _wallpaperLibrary.AddRange(newItems);
            });

            foreach (var item in newItems)
            {
                var thumbItem = new WallpaperThumbnailItem(item, isBuiltIn: false);
                thumbItem.LoadThumbnail();
                _userLibraryItems.Add(thumbItem);
                if (IsUserLibrarySelected)
                    LibraryItems.Add(thumbItem);
            }

            StatusText = newItems.Count > 0
                ? $"已从文件夹导入 {newItems.Count} 个壁纸"
                : "文件夹中未发现可导入的新壁纸";
        }
        catch (Exception ex)
        {
            StatusText = $"文件夹导入失败：{ex.Message}";
            _logger.LogError(ex, "Failed to import folder");
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private void RemoveFromLibrary(WallpaperThumbnailItem? item)
    {
        if (item is null) return;
        if (item.IsBuiltIn)
        {
            StatusText = "内置壁纸不支持移除";
            return;
        }

        _wallpaperLibrary.Remove(item.Id);
        _userLibraryItems.Remove(item);
        LibraryItems.Remove(item);

        if (SelectedLibraryItem == item)
        {
            SelectedLibraryItem = null;
            SelectedImagePath = null;
            PreviewImage = null;
        }

        StatusText = $"已从资源库移除 \"{item.Title}\"";
    }

    public void SelectLibraryItem(WallpaperThumbnailItem item)
    {
        if (SelectedLibraryItem is not null)
            SelectedLibraryItem.IsSelected = false;

        SelectedLibraryItem = item;
        item.IsSelected = true;
        SelectedImagePath = item.SourcePath;
        SelectedWallpaperType = item.Model.Type;
        LoadPreview(item.SourcePath, item.Model.Type);
    }

    private void ClearLibrarySelection()
    {
        if (SelectedLibraryItem is not null)
            SelectedLibraryItem.IsSelected = false;
        SelectedLibraryItem = null;
    }

    private void LoadLibrary()
    {
        _builtInLibraryItems.Clear();
        _userLibraryItems.Clear();

        foreach (var item in LoadBuiltInLibraryItems())
        {
            var thumbItem = new WallpaperThumbnailItem(item, isBuiltIn: true);
            thumbItem.LoadThumbnail();
            _builtInLibraryItems.Add(thumbItem);
        }

        foreach (var item in _wallpaperLibrary.GetAll())
        {
            var thumbItem = new WallpaperThumbnailItem(item, isBuiltIn: false);
            thumbItem.LoadThumbnail();
            _userLibraryItems.Add(thumbItem);
        }

        if (_builtInLibraryItems.Count == 0 && _userLibraryItems.Count > 0)
            SelectedLibraryScope = LibraryScope.User;
        else
            RefreshActiveLibraryItems();
    }

    private void RefreshActiveLibraryItems()
    {
        LibraryItems.Clear();
        var source = IsBuiltInLibrarySelected ? _builtInLibraryItems : _userLibraryItems;
        foreach (var item in source)
            LibraryItems.Add(item);
    }

    private IReadOnlyList<WallpaperItem> LoadBuiltInLibraryItems()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in GetBuiltInWallpaperDirectories())
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (!SupportedExtensions.Contains(Path.GetExtension(file))) continue;
                files.Add(file);
            }
        }

        return files
            .Select(CreateBuiltInWallpaperItem)
            .Where(item => item is not null)
            .Cast<WallpaperItem>()
            .ToList();
    }

    private static IEnumerable<string> GetBuiltInWallpaperDirectories()
    {
        var baseDir = AppContext.BaseDirectory;
        return new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "Library", "wallpapers")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Library", "wallpapers")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Library", "wallpapers")),
        }.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private WallpaperItem? CreateBuiltInWallpaperItem(string filePath)
    {
        return CreateWallpaperItem(filePath, generateThumbnail: false);
    }

    private WallpaperItem? CreateWallpaperItem(string filePath, bool generateThumbnail = true)
    {
        try
        {
            var type = DetectWallpaperType(filePath);
            var id = Guid.NewGuid().ToString("N")[..12];
            var title = Path.GetFileNameWithoutExtension(filePath);

            int width = 0, height = 0;

            if (type == WallpaperType.Image)
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    var decoder = BitmapDecoder.Create(
                        stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                    if (decoder.Frames.Count > 0)
                    {
                        width = decoder.Frames[0].PixelWidth;
                        height = decoder.Frames[0].PixelHeight;
                    }
                }
                catch { /* dimensions unknown, not critical */ }
            }

            var thumbnailPath = generateThumbnail && type == WallpaperType.Image
                ? GenerateThumbnail(filePath, id)
                : null;

            return new WallpaperItem(
                id, type, filePath, title,
                thumbnailPath, null, width, height, [], DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create wallpaper item for {Path}", filePath);
            return null;
        }
    }

    private string? GenerateThumbnail(string sourcePath, string id)
    {
        try
        {
            var thumbDir = PathHelper.GetThumbnailPath();
            Directory.CreateDirectory(thumbDir);
            var thumbPath = Path.Combine(thumbDir, $"{id}.jpg");

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 240;
            bitmap.EndInit();
            bitmap.Freeze();

            var encoder = new JpegBitmapEncoder { QualityLevel = 80 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = File.Create(thumbPath);
            encoder.Save(fs);

            return thumbPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail for {Path}", sourcePath);
            return null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyWallpaper))]
    private async Task ApplyWallpaper()
    {
        if (string.IsNullOrEmpty(SelectedImagePath) || SelectedMonitor is null) return;

        IsApplying = true;
        StatusText = "正在应用壁纸...";

        try
        {
            await _wallpaperService.ApplyWallpaperAsync(
                SelectedMonitor.Profile.MonitorId,
                SelectedWallpaperType,
                SelectedImagePath,
                SelectedScaleMode);

            SelectedMonitor.HasWallpaper = true;
            IsWallpaperActive = true;
            UpdateStatus();
        }
        catch (FileNotFoundException)
        {
            StatusText = "文件不存在，壁纸资源可能已被移动或删除";
            IsWallpaperActive = false;
        }
        catch (Exception ex)
        {
            StatusText = $"应用失败：{ex.Message}";
            IsWallpaperActive = false;
            _logger.LogError(ex, "Failed to apply wallpaper from UI");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private bool CanApplyWallpaper() => !string.IsNullOrEmpty(SelectedImagePath);

    [RelayCommand]
    private void ClearWallpaper()
    {
        if (SelectedMonitor is null) return;

        _wallpaperService.ClearMonitor(SelectedMonitor.Profile.MonitorId);
        SelectedMonitor.HasWallpaper = false;
        IsWallpaperActive = false;
        StatusText = "已清除该显示器壁纸";
    }

    public void RefreshState()
    {
        LoadMonitors();
    }

    public void OnDisplaySettingsChanged()
    {
        _wallpaperService.RefreshMonitors();
        LoadMonitors();
    }

    private void UpdateStatus()
    {
        if (SelectedMonitor is null) return;

        var assignment = _wallpaperService.GetAssignment(SelectedMonitor.Profile.MonitorId);
        if (assignment.HasValue)
        {
            var fileName = Path.GetFileName(assignment.Value.Path);
            var monLabel = SelectedMonitor.IsPrimary ? "主显示器" : $"显示器 {SelectedMonitor.Index}";
            var typeText = assignment.Value.Type switch
            {
                WallpaperType.Image => "图片",
                WallpaperType.Video => "视频",
                WallpaperType.Html => "HTML",
                _ => "未知",
            };
            StatusText = $"{monLabel} 已启用：{fileName}（{typeText}，{GetScaleModeDisplayName(assignment.Value.Mode)}）";
            IsWallpaperActive = true;
        }
        else
        {
            StatusText = "就绪";
            IsWallpaperActive = false;
        }
    }

    private static string GetScaleModeDisplayName(ScaleMode mode) => mode switch
    {
        ScaleMode.Fill => "铺满",
        ScaleMode.Fit => "适应",
        ScaleMode.Stretch => "拉伸",
        ScaleMode.Center => "居中",
        _ => mode.ToString(),
    };

    private static WallpaperType DetectWallpaperType(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (SupportedVideoExtensions.Contains(extension))
            return WallpaperType.Video;
        if (SupportedHtmlExtensions.Contains(extension))
            return WallpaperType.Html;
        return WallpaperType.Image;
    }

    private void LoadPreview(string path, WallpaperType type)
    {
        try
        {
            if (type != WallpaperType.Image)
            {
                PreviewImage = null;
                return;
            }

            if (!File.Exists(path))
            {
                PreviewImage = null;
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 800;
            bitmap.EndInit();
            bitmap.Freeze();
            PreviewImage = bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load preview for {Path}", path);
            PreviewImage = null;
        }
    }
}
