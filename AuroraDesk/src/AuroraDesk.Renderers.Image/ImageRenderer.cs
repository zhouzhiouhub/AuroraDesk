using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;

namespace AuroraDesk.Renderers.Image;

public sealed class ImageRenderer : IWallpaperRenderer
{
    private Window? _window;
    private System.Windows.Controls.Image? _imageControl;

    public WallpaperType SupportedType => WallpaperType.Image;

    public Task InitializeAsync()
    {
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = false,
            Background = Brushes.Black,
            Left = -32000,
            Top = -32000,
            Width = 1,
            Height = 1,
        };

        _imageControl = new System.Windows.Controls.Image
        {
            Stretch = Stretch.UniformToFill,
        };

        _window.Content = _imageControl;
        _window.Show();

        return Task.CompletedTask;
    }

    public Task LoadAsync(string sourcePath, PlaybackOptions options)
    {
        if (_imageControl is null) return Task.CompletedTask;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        _imageControl.Source = bitmap;
        _imageControl.Stretch = options.FitMode switch
        {
            ScaleMode.Fill => Stretch.UniformToFill,
            ScaleMode.Fit => Stretch.Uniform,
            ScaleMode.Stretch => Stretch.Fill,
            ScaleMode.Center => Stretch.None,
            _ => Stretch.UniformToFill,
        };

        return Task.CompletedTask;
    }

    public void Play() { }
    public void Pause() { }
    public void Stop()
    {
        if (_imageControl is not null)
            _imageControl.Source = null;
    }

    public IntPtr GetWindowHandle()
    {
        if (_window is null) return IntPtr.Zero;
        var helper = new WindowInteropHelper(_window);
        return helper.Handle;
    }

    public void SetBounds(int x, int y, int width, int height)
    {
        if (_window is null) return;
        _window.Left = x;
        _window.Top = y;
        _window.Width = width;
        _window.Height = height;
    }

    public void Dispose()
    {
        _imageControl?.ClearValue(System.Windows.Controls.Image.SourceProperty);
        _imageControl = null;
        _window?.Close();
        _window = null;
    }
}
