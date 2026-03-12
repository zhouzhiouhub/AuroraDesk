using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AuroraDesk.Application.Services;
using AuroraDesk.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AuroraDesk.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WallpaperService _wallpaperService;
    private readonly ILogger<MainViewModel> _logger;

    public ObservableCollection<MonitorDisplayItem> Monitors { get; } = new();

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

    public ScaleMode[] AvailableScaleModes { get; } =
        [ScaleMode.Fill, ScaleMode.Fit, ScaleMode.Stretch, ScaleMode.Center];

    public string? SelectedFileName => string.IsNullOrEmpty(SelectedImagePath)
        ? null
        : Path.GetFileName(SelectedImagePath);

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

    public MainViewModel(WallpaperService wallpaperService, ILogger<MainViewModel> logger)
    {
        _wallpaperService = wallpaperService;
        _logger = logger;
        LoadMonitors();
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
