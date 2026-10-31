// Edited on Oct 31, 2026 @ 11:22:00 -> Call Shutdown on single-instance exit or startup failure and wire SoundService
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
    private SimulatorService? _simulatorService;
    private SoundService? _soundService;
    private MainViewModel? _mainVM;

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
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            // Initialize Services
            var triviaDataService = new TriviaDataService();
            var tokenService = new TokenService();
            var streakService = new StreakService(tokenService);
            var soundService = new SoundService();
            _soundService = soundService;
            var gameStateService = new GameStateService(triviaDataService, tokenService, streakService, soundService);
            _gameStateService = gameStateService;
            var wheelService = new WheelService();
            var bannerService = new BannerService();
            var simulatorService = new SimulatorService(gameStateService);
            _simulatorService = simulatorService;
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
            var wheelVM = new WheelViewModel(wheelService, gameStateService, tokenService, soundService);
            var bannerVM = new BannerViewModel(bannerService, gameStateService);
            var simulatorVM = new SimulatorViewModel(simulatorService, gameStateService);
            var settingsVM = new SettingsViewModel(configService, displayService, triviaDataService, gameStateService, webServer);
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
            _mainVM = mainVM;

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
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHelper.Cleanup();
        base.OnExit(e);
        _mainVM?.Dispose();
        _simulatorService?.Dispose();
        _webServer?.Dispose();
        _gameStateService?.Dispose();
        _soundService?.Dispose();
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
