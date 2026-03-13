using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using AuroraDesk.Application.Services;
using AuroraDesk.Core.Enums;
using AuroraDesk.Core.Interfaces;
using AuroraDesk.Infrastructure.Config;
using AuroraDesk.Infrastructure.Library;
using AuroraDesk.Infrastructure.Logging;
using AuroraDesk.Renderers.Html;
using AuroraDesk.Renderers.Image;
using AuroraDesk.Renderers.Video;
using AuroraDesk.Runtime.Desktop;
using AuroraDesk.Shared.Helpers;
using AuroraDesk.App.ViewModels;

namespace AuroraDesk.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        PathHelper.EnsureDirectories();

        var serilogLogger = SerilogSetup.CreateLogger();
        Log.Logger = serilogLogger;

        var services = new ServiceCollection();
        ConfigureServices(services, serilogLogger);
        _serviceProvider = services.BuildServiceProvider();

        SetupGlobalExceptionHandling();

        var configService = _serviceProvider.GetRequiredService<IConfigService>();
        configService.Load();

        var wallpaperLibrary = _serviceProvider.GetRequiredService<IWallpaperLibrary>();
        wallpaperLibrary.Load();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _ = RestoreWallpaperAsync();
    }

    private static void ConfigureServices(IServiceCollection services, Serilog.ILogger serilogLogger)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(serilogLogger, dispose: true);
        });

        services.AddSingleton<IConfigService, JsonConfigService>();
        services.AddSingleton<IWallpaperLibrary, JsonWallpaperLibrary>();
        services.AddSingleton<IDesktopHost, DesktopHostManager>();

        services.AddSingleton<Func<WallpaperType, IWallpaperRenderer>>(_ => type => type switch
        {
            WallpaperType.Image => new ImageRenderer(),
            WallpaperType.Video => new VideoRenderer(),
            WallpaperType.Html => new HtmlRenderer(),
            _ => throw new NotSupportedException($"Wallpaper type '{type}' is not supported in this version.")
        });

        services.AddSingleton<WallpaperService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private async Task RestoreWallpaperAsync()
    {
        try
        {
            var wallpaperService = _serviceProvider!.GetRequiredService<WallpaperService>();
            await wallpaperService.RestoreAsync();

            var viewModel = _serviceProvider!.GetRequiredService<MainViewModel>();
            viewModel.RefreshState();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restore wallpaper on startup");
        }
    }

    private void SetupGlobalExceptionHandling()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled UI exception");
            System.Windows.MessageBox.Show(
                $"发生未处理异常：\n{e.Exception.Message}",
                "AuroraDesk 错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Fatal unhandled exception");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<WallpaperService>()?.Dispose();
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
