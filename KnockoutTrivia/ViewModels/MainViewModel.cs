// Edited on Aug 29, 2026 @ 10:46:00 -> Added SimulatorViewModel navigation for in-app DJ bot simulation and testing
using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using KnockoutTrivia.Views;

namespace KnockoutTrivia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;
    private readonly IDisplayService _displayService;
    private readonly IKnockoutWebServer _webServer;
    private AudienceWindow? _audienceWindow;

    [ObservableProperty]
    private object _currentView;

    [ObservableProperty]
    private string _activeTabTitle = "Game Host";

    [ObservableProperty]
    private string _statusNotification = "Knockout Trivia Ready";

    [ObservableProperty]
    private bool _isAudienceDisplayOpen;

    public GameViewModel GameVM { get; }
    public ConnectViewModel ConnectVM { get; }
    public ScoreboardViewModel ScoreboardVM { get; }
    public QuestionViewModel QuestionVM { get; }
    public WheelViewModel WheelVM { get; }
    public BannerViewModel BannerVM { get; }
    public SimulatorViewModel SimulatorVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public HelpViewModel HelpVM { get; }
    public AboutViewModel AboutVM { get; }
    public AudienceViewModel AudienceVM { get; }

    public MainViewModel(
        IGameStateService gameStateService,
        IDisplayService displayService,
        IKnockoutWebServer webServer,
        GameViewModel gameVM,
        ConnectViewModel connectVM,
        ScoreboardViewModel scoreboardVM,
        QuestionViewModel questionVM,
        WheelViewModel wheelVM,
        BannerViewModel bannerVM,
        SimulatorViewModel simulatorVM,
        SettingsViewModel settingsVM,
        HelpViewModel helpVM,
        AboutViewModel aboutVM,
        AudienceViewModel audienceVM)
    {
        _gameStateService = gameStateService;
        _displayService = displayService;
        _webServer = webServer;
        GameVM = gameVM;
        ConnectVM = connectVM;
        ScoreboardVM = scoreboardVM;
        QuestionVM = questionVM;
        WheelVM = wheelVM;
        BannerVM = bannerVM;
        SimulatorVM = simulatorVM;
        SettingsVM = settingsVM;
        HelpVM = helpVM;
        AboutVM = aboutVM;
        AudienceVM = audienceVM;

        _currentView = ConnectVM;
        ActiveTabTitle = "Player Connect";
        AudienceVM.UpdateView(ConnectVM);

        // Start Web Server
        if (!_webServer.Start(_gameStateService.Settings.WebServerPort))
        {
            StatusNotification = $"⚠ Could not start the player server on port {_gameStateService.Settings.WebServerPort} - it may already be in use. Change the port in Settings and try again.";
        }

        // Connect Screen action links
        ConnectVM.PushToAudienceRequested += (s, e) =>
        {
            AudienceVM.UpdateView(ConnectVM);
            StatusNotification = "Connect Screen sent to Audience Big Screen";
        };

        ConnectVM.GameStartRequested += (s, e) =>
        {
            NavigateToGame();
            StatusNotification = "Game started with connected players!";
        };

        // Super Streak Wheel trigger
        GameVM.SuperStreakRequested += (s, player) =>
        {
            WheelVM.SetSuperStreakPlayer(player);
            NavigateToWheel();
            StatusNotification = $"⚡ Super Streak triggered for {player.Name}!";
        };

// Edited on Aug 28, 2026 @ 11:15:00 -> Added DeviceName routing and DPI-aware monitor positioning
        // Wire settings display events
        SettingsVM.OpenAudienceDisplayRequested += (s, args) => OpenAudienceDisplay(args.Index, args.Device);
        SettingsVM.CloseAudienceDisplayRequested += (s, e) => CloseAudienceDisplay();
        SettingsVM.MoveHostWindowRequested += (s, args) => MoveHostWindow(args.Index, args.Device);
    }

    public void OpenAudienceDisplay(int monitorIndex, string? deviceName = null)
    {
        if (_audienceWindow == null)
        {
            _audienceWindow = new AudienceWindow
            {
                DataContext = AudienceVM
            };
            _audienceWindow.Closed += (s, e) =>
            {
                _audienceWindow = null;
                AudienceVM.IsWindowOpen = false;
                SettingsVM.IsAudienceDisplayActive = false;
                IsAudienceDisplayOpen = false;
            };
        }

        _displayService.PositionWindow(_audienceWindow, monitorIndex, deviceName, fillArea: true);
        _audienceWindow.Show();
        AudienceVM.IsWindowOpen = true;
        SettingsVM.IsAudienceDisplayActive = true;
        IsAudienceDisplayOpen = true;
        StatusNotification = $"Audience Big Screen active on Display {monitorIndex + 1}";
    }

    public void CloseAudienceDisplay()
    {
        if (_audienceWindow != null)
        {
            _audienceWindow.Hide();
            AudienceVM.IsWindowOpen = false;
            SettingsVM.IsAudienceDisplayActive = false;
            IsAudienceDisplayOpen = false;
            StatusNotification = "Audience Big Screen closed";
        }
    }

    [RelayCommand]
    public void ToggleAudienceDisplay()
    {
        if (IsAudienceDisplayOpen)
        {
            CloseAudienceDisplay();
        }
        else
        {
            int monitorIndex = SettingsVM.SelectedAudienceMonitor?.Index ?? 1;
            string? targetDevice = SettingsVM.SelectedAudienceMonitor?.DeviceName;
            OpenAudienceDisplay(monitorIndex, targetDevice);
        }
    }

    public void MoveHostWindow(int monitorIndex, string? deviceName = null)
    {
        if (Application.Current.MainWindow != null)
        {
            _displayService.PositionWindow(Application.Current.MainWindow, monitorIndex, deviceName, fillArea: false);
        }
    }

    private void SyncAudienceView(object newView)
    {
        // Don't show DJ internal tabs (Simulator, Settings, Help, About) to the audience on the big screen
        if (newView == SimulatorVM || newView == SettingsVM || newView == HelpVM || newView == AboutVM)
        {
            return;
        }

        if (newView == GameVM)
        {
            AudienceVM.UpdateView(QuestionVM);
        }
        else
        {
            AudienceVM.UpdateView(newView);
        }
    }

    [RelayCommand]
    public void NavigateToConnect()
    {
        CurrentView = ConnectVM;
        ActiveTabTitle = "Player Connect";
        SyncAudienceView(ConnectVM);
    }

    [RelayCommand]
    public void NavigateToGame()
    {
        CurrentView = GameVM;
        ActiveTabTitle = "Game Host";
        SyncAudienceView(GameVM);
    }

    [RelayCommand]
    public void NavigateToScoreboard()
    {
        CurrentView = ScoreboardVM;
        ActiveTabTitle = "Scoreboard";
        SyncAudienceView(ScoreboardVM);
    }

    [RelayCommand]
    public void NavigateToQuestion()
    {
        CurrentView = QuestionVM;
        ActiveTabTitle = "Question Board";
        SyncAudienceView(QuestionVM);
    }

    [RelayCommand]
    public void NavigateToWheel()
    {
        CurrentView = WheelVM;
        ActiveTabTitle = "Super Streak Wheel";
        SyncAudienceView(WheelVM);
    }

    [RelayCommand]
    public void NavigateToBanners()
    {
        CurrentView = BannerVM;
        ActiveTabTitle = "Banners & Screens";
        SyncAudienceView(BannerVM);
    }

    [RelayCommand]
    public void NavigateToSimulator()
    {
        CurrentView = SimulatorVM;
        ActiveTabTitle = "Bot Simulator & Tests";
    }

    [RelayCommand]
    public void NavigateToSettings()
    {
        CurrentView = SettingsVM;
        ActiveTabTitle = "DJ Settings";
    }

    [RelayCommand]
    public void NavigateToHelp()
    {
        CurrentView = HelpVM;
        ActiveTabTitle = "DJ & Host Guide";
    }

    [RelayCommand]
    public void NavigateToAbout()
    {
        CurrentView = AboutVM;
        ActiveTabTitle = "About Knockout";
    }
}

