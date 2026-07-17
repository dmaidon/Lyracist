// Edited on Jul 17, 2026 @ 09:00:00 -> SQLite keep-alive connection
using System;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Media.Audio;
using Lyracist.Media.Video;
using Lyracist.Services.Media;
using Lyracist.Services.Media.Cdg;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Lyracist.ViewModels;
using Lyracist.Views.Pages;
using Lyracist.Windows;
using Lyracist.Services.Integration;

namespace Lyracist;

public partial class App : System.Windows.Application
{
    public static readonly Microsoft.IO.RecyclableMemoryStreamManager MemoryStreamManager = new();

    public static IHost? Host { get; private set; }
    public static IHost AppHost => Host!;

    private static Microsoft.Data.Sqlite.SqliteConnection? _keepAliveConnection;

    private static void KeepDatabaseAlive()
    {
        try
        {
            _keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection(Lyracist.Data.LyracistDbContext.GetConnectionString());
            _keepAliveConnection.Open();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Failed to open SQLite keep-alive connection");
        }
    }

    public App()
    {
        // Initialise logger (creates Logs dir, purges files older than 30 days)
        _ = typeof(AppLogger);
        AppLogger.LogAppStart();

        // Centralized LibVLC initialization
        AppLogger.InitializeLibVlc();

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
                services.AddSingleton<IPlaylistService, PlaylistService>();
                services.AddSingleton<IRequestService, Services.Requests.RequestService>();
                services.AddSingleton<IOccasionService, Services.Occasions.OccasionService>();
                services.AddSingleton<IPartyTymeService, PartyTymeService>();
                services.AddKeyedSingleton<BackgroundMusicPlayer>("Opening");
                services.AddKeyedSingleton<BackgroundMusicPlayer>("FillIn");
                services.AddKeyedSingleton<BackgroundMusicPlayer>("EndRotation");
                services.AddKeyedSingleton("Occasion", (_, _) => new BackgroundMusicPlayer { Loop = false });
                services.AddSingleton<IShowFlowService, ShowFlowService>();
                services.AddSingleton<IKSRotationSyncService, KSRotationSyncService>();

                // ViewModels
                services.AddSingleton<SplashViewModel>();
                services.AddSingleton<KaraokeViewModel>();
                services.AddSingleton<RotationViewModel>();
                services.AddSingleton<RotationWindowViewModel>();
                services.AddSingleton<LyricsViewModel>();
                services.AddSingleton<LyricsWindowViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<PlaylistsViewModel>();
                services.AddSingleton<RequestsViewModel>();
                services.AddSingleton<ScaryokeViewModel>();
                services.AddSingleton<HelpViewModel>();
                services.AddSingleton<AboutViewModel>();
                services.AddTransient<SongSettingsViewModel>();
                services.AddTransient<SingerSettingsViewModel>();

                // Windows
                services.AddSingleton<SplashWindow>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<RotationWindow>();
                services.AddSingleton<LyricsWindow>();
                services.AddSingleton<ScaryokeWindow>();
                services.AddTransient<SongSettingsWindow>();
                services.AddTransient<SingerSettingsWindow>();
                services.AddTransient<AboutWindow>();

                // Pages
                services.AddSingleton<KaraokePage>();
                services.AddSingleton<RotationPage>();
                services.AddSingleton<LyricsPage>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<PlaylistsPage>();
                services.AddSingleton<RequestsPage>();
                services.AddSingleton<HelpPage>();
                services.AddSingleton<AboutPage>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Subscribe to theme changes to apply Lyracist custom brushes
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += (theme, accent) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                if (dispatcher.CheckAccess())
                {
                    bool isDark = theme == Wpf.Ui.Appearance.ApplicationTheme.Dark;
                    Lyracist.Themes.LyracistThemeManager.Apply(isDark);
                }
                else
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        bool isDark = theme == Wpf.Ui.Appearance.ApplicationTheme.Dark;
                        Lyracist.Themes.LyracistThemeManager.Apply(isDark);
                    }));
                }
            }
        };

        await Host!.StartAsync();

        // Apply any pending EF Core migrations so a fresh install gets a
        // fully-formed schema instead of an empty 0-byte SQLite file.
        try
        {
            using var db = new Lyracist.Data.LyracistDbContext();
            db.Database.Migrate();
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

            // Deduplicate Songs on FilePath before making index unique
            db.Database.ExecuteSqlRaw("DELETE FROM Songs WHERE SongId NOT IN (SELECT MIN(SongId) FROM Songs GROUP BY FilePath);");
            db.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Songs_FilePath;");
            db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Songs_FilePath ON Songs (FilePath);");

            Lyracist.Services.Database.SingerHistoryService.EnsureTableCreated();
            
            // Keep database connection alive to optimize SQLite caching and concurrency
            KeepDatabaseAlive();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database migration");
        }

        // Resolve and show the SplashWindow if the setting is enabled
        var splash = Host.Services.GetRequiredService<SplashWindow>();
        if (AppSettings.ShowSplashOnStartup)
        {
            splash.Show();

            // Simulate loading updates
            splash.UpdateStatus("Initializing services...");
            await Task.Delay(50);

            splash.UpdateStatus("Loading UI components...");
            await Task.Delay(50);

            splash.UpdateStatus("Starting Lyracist...");
            await Task.Delay(50);
        }

        // Start the tablet lyrics server in the background
        var server = Host.Services.GetRequiredService<ITabletLyricsServer>();
        await server.StartAsync();

        // Start the KSRotation sync service in the background
        var syncService = Host.Services.GetRequiredService<IKSRotationSyncService>();
        syncService.Start();

        // Resolve and show the MainWindow via dependency injection
        var mainWindow = Host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Apply theme settings
        ApplyAppTheme(mainWindow, AppSettings.ThemeMode);

        // Listen for future setting changes
        AppSettings.ThemeModeChanged += theme =>
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyAppTheme(mainWindow, theme);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => ApplyAppTheme(mainWindow, theme)));
            }
        };

        mainWindow.Show();

        // Close splash window now that MainWindow is ready
        if (AppSettings.ShowSplashOnStartup)
        {
            splash.Close();
        }

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

    private static void ApplyAppTheme(Window window, string themeMode)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;

        if (string.Equals(themeMode, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            if (handle != System.IntPtr.Zero)
            {
                Wpf.Ui.Appearance.SystemThemeWatcher.UnWatch(window);
            }
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
        }
        else if (string.Equals(themeMode, "Light", StringComparison.OrdinalIgnoreCase))
        {
            if (handle != System.IntPtr.Zero)
            {
                Wpf.Ui.Appearance.SystemThemeWatcher.UnWatch(window);
            }
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
        }
        else // System
        {
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(window);
            Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
        }
    }
}
