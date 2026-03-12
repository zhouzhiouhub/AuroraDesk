using System.Windows;
using System.Windows.Interop;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;

namespace AuroraDesk.Renderers.Video;

public sealed class VideoRenderer : IWallpaperRenderer
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Window? _window;
    private VideoView? _videoView;

    public WallpaperType SupportedType => WallpaperType.Video;

    public Task InitializeAsync()
    {
        LibVLCSharp.Shared.Core.Initialize();

        _libVlc = new LibVLC("--no-audio", "--no-osd");
        _mediaPlayer = new MediaPlayer(_libVlc);

        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            AllowsTransparency = false,
            Background = System.Windows.Media.Brushes.Black,
        };

        _videoView = new VideoView();
        _window.Content = _videoView;
        _window.Show();

        _videoView.MediaPlayer = _mediaPlayer;

        return Task.CompletedTask;
    }

    public Task LoadAsync(string sourcePath, PlaybackOptions options)
    {
        if (_libVlc is null || _mediaPlayer is null) return Task.CompletedTask;

        var media = new Media(_libVlc, new Uri(sourcePath));
        if (options.Loop)
            media.AddOption("input-repeat=65535");

        _mediaPlayer.Volume = options.Mute ? 0 : options.Volume;
        _mediaPlayer.Play(media);

        return Task.CompletedTask;
    }

    public void Play() => _mediaPlayer?.Play();
    public void Pause() => _mediaPlayer?.Pause();
    public void Stop() => _mediaPlayer?.Stop();

    public IntPtr GetWindowHandle()
    {
        if (_window is null) return IntPtr.Zero;
        return new WindowInteropHelper(_window).Handle;
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
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _videoView = null;
        _libVlc?.Dispose();
        _libVlc = null;
        _window?.Close();
        _window = null;
    }
}
