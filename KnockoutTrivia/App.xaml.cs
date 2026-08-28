// Edited on Aug 28, 2026 @ 09:28:00 -> Added KnockoutWebServer and ConnectViewModel startup bootstrapping
using System.Windows;
using KnockoutTrivia.Services;
using KnockoutTrivia.ViewModels;

namespace KnockoutTrivia;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize Services
        var triviaDataService = new TriviaDataService();
        var tokenService = new TokenService();
        var streakService = new StreakService(tokenService);
        var gameStateService = new GameStateService(triviaDataService, tokenService, streakService);
        var wheelService = new WheelService();
        var bannerService = new BannerService();
        var displayService = new DisplayService();
        var configService = new ConfigService();

        // Load Game Settings
        var settings = configService.LoadSettings();
        gameStateService.InitializeGame(settings);

        // Initialize Web Server
        var webServer = new KnockoutWebServer(gameStateService, settings.WebServerPort);

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
}

