using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Wpf;
using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Core.Models;

namespace AuroraDesk.Renderers.Html;

public sealed class HtmlRenderer : IWallpaperRenderer
{
    private Window? _window;
    private WebView2? _webView;

    public WallpaperType SupportedType => WallpaperType.Html;

    public async Task InitializeAsync()
    {
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = false,
            Background = System.Windows.Media.Brushes.Black,
            Left = -32000,
            Top = -32000,
            Width = 1,
            Height = 1,
        };

        _webView = new WebView2();
        _window.Content = _webView;
        _window.Show();

        await _webView.EnsureCoreWebView2Async();
    }

    public Task LoadAsync(string sourcePath, PlaybackOptions options)
    {
        if (_webView?.CoreWebView2 is null) return Task.CompletedTask;

        if (Uri.TryCreate(sourcePath, UriKind.Absolute, out var uri))
        {
            _webView.CoreWebView2.Navigate(uri.ToString());
        }
        else
        {
            var fullPath = System.IO.Path.GetFullPath(sourcePath);
            _webView.CoreWebView2.Navigate(fullPath);
        }

        return Task.CompletedTask;
    }

    public void Play() { }
    public void Pause() { }
    public void Stop()
    {
        _webView?.CoreWebView2?.NavigateToString("<html><body style='background:black'></body></html>");
    }

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
        _webView?.Dispose();
        _webView = null;
        _window?.Close();
        _window = null;
    }
}
