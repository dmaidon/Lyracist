// Edited on Oct 6, 2026 @ 10:21:30 -> Enforce single-instance application execution using SingleInstanceHelper
using System;
using System.Threading.Tasks;
using System.Windows;
using KnockoutTrivia.Services;
using KnockoutTrivia.ViewModels;
using Lyracist.Shared;

namespace KnockoutTrivia;

public partial class App : Application
{
    private GameStateService? _gameStateService;
    private KnockoutWebServer? _webServer;

    public App()
    {
        TextBoxSelectionHelper.EnableGlobalSelectAllOnFocus();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Lyracist.Shared.Globals.LogAppStart("KnockoutTrivia");
        Lyracist.Shared.Globals.PurgeOldLogs(14);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (!SingleInstanceHelper.EnsureSingleInstance("KnockoutTrivia"))
        {
            return;
        }

        base.OnStartup(e);

        try
        {
            // Initialize Services
            var triviaDataService = new TriviaDataService();
            var tokenService = new TokenService();
            var streakService = new StreakService(tokenService);
            var gameStateService = new GameStateService(triviaDataService, tokenService, streakService);
            _gameStateService = gameStateService;
            var wheelService = new WheelService();
            var bannerService = new BannerService();
            var simulatorService = new SimulatorService(gameStateService);
            var displayService = new DisplayService();
            var configService = new ConfigService();

            // Load Game Settings
            var settings = configService.LoadSettings();
            gameStateService.InitializeGame(settings);

            // Initialize Web Server
            var webServer = new KnockoutWebServer(gameStateService, settings.WebServerPort);
            _webServer = webServer;

            // Preload questions from chosen databases/packs or default DB
            if (settings.SelectedSourcePaths != null && settings.SelectedSourcePaths.Count > 0)
            {
                await gameStateService.LoadQuestionsFromSourcesAsync(settings.SelectedSourcePaths);
            }
            else
            {
                await gameStateService.LoadGameQuestionsAsync();
            }

            // Initialize ViewModels
            var gameVM = new GameViewModel(gameStateService, tokenService, streakService);
            var connectVM = new ConnectViewModel(gameStateService, webServer);
            var scoreboardVM = new ScoreboardViewModel(gameStateService, tokenService);
            var questionVM = new QuestionViewModel(gameStateService);
            var wheelVM = new WheelViewModel(wheelService, gameStateService, tokenService);
            var bannerVM = new BannerViewModel(bannerService, gameStateService);
            var simulatorVM = new SimulatorViewModel(simulatorService, gameStateService);
            var settingsVM = new SettingsViewModel(configService, displayService, triviaDataService, gameStateService);
            var helpVM = new HelpViewModel();
            var aboutVM = new AboutViewModel();
            var audienceVM = new AudienceViewModel();

            var mainVM = new MainViewModel(
                gameStateService,
                displayService,
                webServer,
                gameVM,
                connectVM,
                scoreboardVM,
                questionVM,
                wheelVM,
                bannerVM,
                simulatorVM,
                settingsVM,
                helpVM,
                aboutVM,
                audienceVM);

            // Launch MainWindow
            var mainWindow = new MainWindow
            {
                DataContext = mainVM
            };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("KnockoutTrivia", "App.OnStartup", ex);
            System.Windows.MessageBox.Show($"Knockout Trivia encountered an error during startup:\n\n{ex.Message}", "Knockout Trivia", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHelper.Cleanup();
        base.OnExit(e);
        _webServer?.Dispose();
        _gameStateService?.Dispose();
        Environment.Exit(0);
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Lyracist.Shared.Globals.LogError("KnockoutTrivia", "DispatcherUnhandledException", e.Exception);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Lyracist.Shared.Globals.LogError("KnockoutTrivia", "AppDomainUnhandledException", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Lyracist.Shared.Globals.LogError("KnockoutTrivia", "UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}

