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

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };

    public ObservableCollection<MonitorDisplayItem> Monitors { get; } = new();
    public ObservableCollection<WallpaperThumbnailItem> LibraryItems { get; } = new();

    [ObservableProperty]
    private MonitorDisplayItem? selectedMonitor;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyWallpaperCommand))]
    private string? selectedImagePath;

    [ObservableProperty]
    private ScaleMode selectedScaleMode = ScaleMode.Fill;

    [ObservableProperty]
    private string statusText = "Ready";

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

    public bool IsBuiltInLibrarySelected => SelectedLibraryScope == LibraryScope.BuiltIn;
    public bool IsUserLibrarySelected => SelectedLibraryScope == LibraryScope.User;

    public string LibraryDescription => IsBuiltInLibrarySelected
        ? "Built-in wallpapers shipped with AuroraDesk"
        : "Import and manage your custom wallpapers";

    public string EmptyLibraryTitle => IsBuiltInLibrarySelected
        ? "Built-in library is empty"
        : "Custom library is empty";

    public string EmptyLibraryHint => IsBuiltInLibrarySelected
        ? "Put images into Library/wallpapers to use built-in items"
        : "Import images or folders to get started";

    public string ScaleModeDescription => SelectedScaleMode switch
    {
        ScaleMode.Fill => "Fill the screen and crop edges to remove letterboxing",
        ScaleMode.Fit => "Show the full image, letterbox if aspect ratio differs",
        ScaleMode.Stretch => "Stretch to fill exactly, may distort the image",
        ScaleMode.Center => "Display at original size, centered on screen",
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
            if (SelectedMonitor is null) return "No monitor selected";
            var p = SelectedMonitor.Profile;
            var label = p.IsPrimary ? "Primary" : $"Monitor {SelectedMonitor.Index}";
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
            IsWallpaperActive = true;
            LoadPreview(assignment.Value.Path);
            UpdateStatus();
        }
        else
        {
            SelectedImagePath = null;
            SelectedScaleMode = ScaleMode.Fill;
            IsWallpaperActive = false;
            PreviewImage = null;
            StatusText = "Ready";
        }
    }

    [RelayCommand]
    private void SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Wallpaper Image",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedImagePath = dialog.FileName;
            LoadPreview(dialog.FileName);
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
            Title = "Import Wallpapers to Library",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0) return;

        IsImporting = true;
        StatusText = "Importing wallpapers...";

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
                ? $"Imported {newItems.Count} wallpaper(s)"
                : "No new wallpapers to import (duplicates skipped)";
        }
        catch (Exception ex)
        {
            StatusText = $"Import failed: {ex.Message}";
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
            Description = "Select a folder to scan for wallpapers",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        IsImporting = true;
        StatusText = "Scanning folder...";

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
                ? $"Imported {newItems.Count} wallpaper(s) from folder"
                : "No new wallpapers found in folder";
        }
        catch (Exception ex)
        {
            StatusText = $"Folder import failed: {ex.Message}";
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
            StatusText = "Built-in wallpapers cannot be removed";
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

        StatusText = $"Removed \"{item.Title}\" from library";
    }

    public void SelectLibraryItem(WallpaperThumbnailItem item)
    {
        if (SelectedLibraryItem is not null)
            SelectedLibraryItem.IsSelected = false;

        SelectedLibraryItem = item;
        item.IsSelected = true;
        SelectedImagePath = item.SourcePath;
        LoadPreview(item.SourcePath);
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
            var id = Guid.NewGuid().ToString("N")[..12];
            var title = Path.GetFileNameWithoutExtension(filePath);

            int width = 0, height = 0;
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

            var thumbnailPath = generateThumbnail ? GenerateThumbnail(filePath, id) : null;

            return new WallpaperItem(
                id, WallpaperType.Image, filePath, title,
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
        StatusText = "Applying wallpaper...";

        try
        {
            await _wallpaperService.ApplyImageAsync(
                SelectedMonitor.Profile.MonitorId,
                SelectedImagePath,
                SelectedScaleMode);

            SelectedMonitor.HasWallpaper = true;
            IsWallpaperActive = true;
            UpdateStatus();
        }
        catch (FileNotFoundException)
        {
            StatusText = "File not found — the image may have been moved or deleted";
            IsWallpaperActive = false;
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to apply: {ex.Message}";
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
        StatusText = "Wallpaper cleared";
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
            var monLabel = SelectedMonitor.IsPrimary ? "Primary" : $"Monitor {SelectedMonitor.Index}";
            StatusText = $"Active on {monLabel} — {fileName} ({assignment.Value.Mode})";
            IsWallpaperActive = true;
        }
        else
        {
            StatusText = "Ready";
            IsWallpaperActive = false;
        }
    }

    private void LoadPreview(string path)
    {
        try
        {
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
