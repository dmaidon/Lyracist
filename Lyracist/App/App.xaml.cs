using System;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Media.Video;
using Lyracist.Services.Media;
using Lyracist.Services.Media.Cdg;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Lyracist.ViewModels;
using Lyracist.Views.Pages;
using Lyracist.Windows;

namespace Lyracist;

public partial class App : System.Windows.Application
{
    public static IHost? Host { get; private set; }
    public static IHost AppHost => Host!;

    public App()
    {
        // Initialise logger (creates Logs dir, purges files older than 30 days)
        _ = typeof(AppLogger);
        AppLogger.LogAppStart();

        DispatcherUnhandledException += (_, e) =>
        {
            AppLogger.LogError(e.Exception, "DispatcherUnhandledException");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLogger.LogError(ex, "UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.LogError(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // WPF UI Navigation Services
                services.AddSingleton<Wpf.Ui.INavigationService, Wpf.Ui.NavigationService>();
                services.AddSingleton<Wpf.Ui.Abstractions.INavigationViewPageProvider, PageService>();

                // Services
                services.AddSingleton<ICDGDecoder, CdgDecoder>();
                services.AddSingleton<ICdgFrameScheduler, CdgFrameScheduler>();
                services.AddSingleton<IVideoBackend, LibVlcVideoBackend>();
                services.AddSingleton<IMediaEngine, MediaEngine>();
                services.AddSingleton<IDisplayService, DisplayService>();
                services.AddSingleton<ILibraryService, LibraryService>();
                services.AddSingleton<ITabletLyricsServer, TabletLyricsServer>();
                services.AddSingleton<ILyricsController, LyricsController>();

                // ViewModels
                services.AddSingleton<SplashViewModel>();
                services.AddSingleton<KaraokeViewModel>();
                services.AddSingleton<RotationViewModel>();
                services.AddSingleton<RotationWindowViewModel>();
                services.AddSingleton<LyricsViewModel>();
                services.AddSingleton<LyricsWindowViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<HelpViewModel>();
                services.AddSingleton<AboutViewModel>();

                // Windows
                services.AddSingleton<SplashWindow>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<RotationWindow>();
                services.AddSingleton<LyricsWindow>();

                // Pages
                services.AddSingleton<KaraokePage>();
                services.AddSingleton<RotationPage>();
                services.AddSingleton<LyricsPage>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<HelpPage>();
                services.AddSingleton<AboutPage>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await Host!.StartAsync();

        // Resolve and show the SplashWindow immediately
        var splash = Host.Services.GetRequiredService<SplashWindow>();
        splash.Show();

        // Simulate loading updates
        splash.UpdateStatus("Initializing services...");
        await Task.Delay(600);

        splash.UpdateStatus("Loading UI components...");
        await Task.Delay(600);

        splash.UpdateStatus("Starting Lyracist...");
        await Task.Delay(600);

        // Start the tablet lyrics server in the background
        var server = Host.Services.GetRequiredService<ITabletLyricsServer>();
        await server.StartAsync();

        // Resolve and show the MainWindow via dependency injection
        var mainWindow = Host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();

        // Close splash window now that MainWindow is ready
        splash.Close();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (Host != null)
        {
            var server = Host.Services.GetService<ITabletLyricsServer>();
            if (server != null)
            {
                await server.StopAsync();
            }

            await Host.StopAsync();
            Host.Dispose();
        }

        base.OnExit(e);
    }
}
