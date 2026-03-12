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

    public string MonitorInfo
    {
        get
        {
            try
            {
                var (w, h) = _wallpaperService.GetPrimaryScreenResolution();
                return $"Primary Monitor  {w} × {h}";
            }
            catch
            {
                return "Primary Monitor";
            }
        }
    }

    public MainViewModel(WallpaperService wallpaperService, ILogger<MainViewModel> logger)
    {
        _wallpaperService = wallpaperService;
        _logger = logger;

        if (_wallpaperService.IsActive && _wallpaperService.CurrentWallpaperPath is not null)
        {
            SelectedImagePath = _wallpaperService.CurrentWallpaperPath;
            SelectedScaleMode = _wallpaperService.CurrentScaleMode;
            LoadPreview(_wallpaperService.CurrentWallpaperPath);
            UpdateStatus();
        }
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
        if (string.IsNullOrEmpty(SelectedImagePath)) return;

        IsApplying = true;
        StatusText = "Applying wallpaper...";

        try
        {
            await _wallpaperService.ApplyImageAsync(SelectedImagePath, SelectedScaleMode);
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
        _wallpaperService.ClearWallpaper();
        IsWallpaperActive = false;
        StatusText = "Wallpaper cleared";
    }

    public void RefreshState()
    {
        if (_wallpaperService.IsActive && _wallpaperService.CurrentWallpaperPath is not null)
        {
            SelectedImagePath = _wallpaperService.CurrentWallpaperPath;
            SelectedScaleMode = _wallpaperService.CurrentScaleMode;
            LoadPreview(_wallpaperService.CurrentWallpaperPath);
        }
        UpdateStatus();
        OnPropertyChanged(nameof(MonitorInfo));
    }

    private void UpdateStatus()
    {
        if (_wallpaperService.IsActive && _wallpaperService.CurrentWallpaperPath is not null)
        {
            var fileName = Path.GetFileName(_wallpaperService.CurrentWallpaperPath);
            StatusText = $"Active — {fileName}  ({SelectedScaleMode})";
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
