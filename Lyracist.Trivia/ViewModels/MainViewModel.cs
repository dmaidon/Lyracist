// Edited on Aug 17, 2026 @ 14:53:30 -> Added Wi-Fi credentials, Pre-Game countdown controls, and Connect Instructions screen synchronization
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;
using QRCoder;
using Application = System.Windows.Application;
using Screen = System.Windows.Forms.Screen;

namespace Lyracist.Trivia.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly TriviaGameEngine _engine;
    private readonly TriviaWebServer _webServer;
    private readonly TriviaDatabaseService _dbService;

    [ObservableProperty]
    private TriviaSettings _settings;

    [ObservableProperty]
    private string _venueName = "The Main Stage Lounge";

    [ObservableProperty]
    private string _hostName = "Trivia Master";

    [ObservableProperty]
    private string _connectUrl = "http://localhost:8085";

    [ObservableProperty]
    private BitmapSource? _qrCodeImage;

    [ObservableProperty]
    private TriviaGameState _gameState = TriviaGameState.Lobby;

    [ObservableProperty]
    private int _remainingSeconds = 15;

    [ObservableProperty]
    private int _totalCountdownSeconds = 15;

    [ObservableProperty]
    private bool _isTimerRunning;

    [ObservableProperty]
    private bool _isWarningActive;

    [ObservableProperty]
    private TriviaQuestion? _activeQuestion;

    [ObservableProperty]
    private string _currentRoundTitle = "Round 1";

    [ObservableProperty]
    private int _currentQuestionNumber = 1;

    [ObservableProperty]
    private int _totalQuestionsInRound = 5;

    [ObservableProperty]
    private TriviaQuestionPack? _selectedPack;

    [ObservableProperty]
    private int _connectedPlayerCount;

    [ObservableProperty]
    private bool _autoAdvanceQuestions = true;

    [ObservableProperty]
    private int _questionsPerGame = 10;

    [ObservableProperty]
    private bool _isGameComplete;

    [ObservableProperty]
    private string _winnerAnnouncement = string.Empty;

    [ObservableProperty]
    private string _winningTeamRoster = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private bool _autoStartNextGameEnabled = true;

    [ObservableProperty]
    private int _nextGameDelayMinutes = 3;

    [ObservableProperty]
    private int _defaultQuestionSeconds = 15;

    [ObservableProperty]
    private int _answerEliminationIntervalSeconds = 5;

    [ObservableProperty]
    private int _postRevealDelaySeconds = 5;

    [ObservableProperty]
    private int _basePointsPerQuestion = 1000;

    [ObservableProperty]
    private bool _speedBonusEnabled = true;

    [ObservableProperty]
    private int _maxSpeedBonus = 500;

    [ObservableProperty]
    private double _streakBonusMultiplier = 0.1;

    [ObservableProperty]
    private bool _soundEffectsEnabled = true;

    [ObservableProperty]
    private int _port = 8085;

    [ObservableProperty]
    private string _wifiSsid = string.Empty;

    [ObservableProperty]
    private string _wifiPassword = string.Empty;

    [ObservableProperty]
    private int _preGameCountdownMinutes = 5;

    [ObservableProperty]
    private int _preGameSecondsRemaining = 300;

    [ObservableProperty]
    private bool _isPreGameCountdownRunning = true;

    [ObservableProperty]
    private string _preGameCountdownText = "05:00";

    [ObservableProperty]
    private bool _isShowingConnectScreen = true;

    [ObservableProperty]
    private bool _autoStartAfterCountdown = true;

    [ObservableProperty]
    private bool _isIntermissionActive;

    [ObservableProperty]
    private int _intermissionSecondsRemaining;

    [ObservableProperty]
    private string _intermissionCountdownText = "03:00";

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    public int[] QuestionsPerGamePresets { get; } = [5, 10, 15, 20, 25, 50, 100];
    public ObservableCollection<MonitorInfo> AvailableMonitors { get; } = [];
    public ObservableCollection<TriviaQuestionPack> AvailablePacks { get; } = [];
    public ObservableCollection<TriviaPlayer> Players { get; } = [];
    public ObservableCollection<AnswerDistributionItem> AnswerDistribution { get; } = [];

    public TriviaGameEngine Engine => _engine;

    public event EventHandler<string>? TargetMonitorChanged;

    private readonly System.Timers.Timer _preGameTimer = new(1000);
    private DisplayViewModel? _activeDisplayVm;

    public MainViewModel()
    {
        string settingsPath = TriviaStorageHelper.GetSettingsPath();

        TriviaSettings? loadedSettings = null;
        if (File.Exists(settingsPath))
        {
            try
            {
                string json = File.ReadAllText(settingsPath);
                loadedSettings = JsonSerializer.Deserialize<TriviaSettings>(json);
            }
            catch { }
        }

        _settings = loadedSettings ?? new TriviaSettings();
        _venueName = _settings.VenueName;
        _hostName = _settings.HostName;
        _autoAdvanceQuestions = _settings.AutoAdvanceQuestions;
        _questionsPerGame = _settings.QuestionsPerGame > 0 ? _settings.QuestionsPerGame : 10;
        _autoStartNextGameEnabled = _settings.AutoStartNextGameEnabled;
        _nextGameDelayMinutes = _settings.NextGameDelayMinutes > 0 ? _settings.NextGameDelayMinutes : 3;
        _defaultQuestionSeconds = _settings.DefaultQuestionSeconds > 0 ? _settings.DefaultQuestionSeconds : 15;
        _answerEliminationIntervalSeconds = _settings.AnswerEliminationIntervalSeconds > 0 ? _settings.AnswerEliminationIntervalSeconds : 5;
        _postRevealDelaySeconds = _settings.PostRevealDelaySeconds > 0 ? _settings.PostRevealDelaySeconds : 5;
        _basePointsPerQuestion = _settings.BasePointsPerQuestion > 0 ? _settings.BasePointsPerQuestion : 1000;
        _speedBonusEnabled = _settings.SpeedBonusEnabled;
        _maxSpeedBonus = _settings.MaxSpeedBonus;
        _streakBonusMultiplier = _settings.StreakBonusMultiplier;
        _soundEffectsEnabled = _settings.SoundEffectsEnabled;
        _port = _settings.Port > 0 ? _settings.Port : 8085;
        _wifiSsid = string.IsNullOrWhiteSpace(_settings.WifiSsid) ? (Lyracist.Shared.WifiHelper.GetConnectedSsid() ?? "Venue Wi-Fi") : _settings.WifiSsid;
        _wifiPassword = _settings.WifiPassword;
        _preGameCountdownMinutes = _settings.PreGameCountdownMinutes > 0 ? _settings.PreGameCountdownMinutes : 5;
        _autoStartAfterCountdown = _settings.AutoStartAfterCountdown;
        _preGameSecondsRemaining = _preGameCountdownMinutes * 60;
        _preGameCountdownText = $"{_preGameCountdownMinutes:D2}:00";

        _dbService = new TriviaDatabaseService();
        _engine = new TriviaGameEngine(_settings);
        _webServer = new TriviaWebServer(_engine, _settings.Port);

        // Wire engine events
        _engine.StateChanged += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleStateChanged(e));
        _engine.TimerTick += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleTimerTick(e));
        _engine.QuestionStarted += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleQuestionStarted(e));
        _engine.AnswerRevealed += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleAnswerRevealed(e));
        _engine.LeaderboardUpdated += (s, e) => Application.Current?.Dispatcher.Invoke(() => RefreshPlayers(e));
        _engine.GameCompleted += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleGameCompleted(e));
        _engine.IntermissionTick += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleIntermissionTick(e));
        _engine.IntermissionCompleted += (s, e) => Application.Current?.Dispatcher.Invoke(() => HandleIntermissionCompleted());

        // Setup Pre-Game ticker (started when screen is cast or manually started)
        _isPreGameCountdownRunning = false;
        _preGameTimer.Elapsed += OnPreGameTimerTick;

        // Start WebServer, populate monitors & load packs
        _webServer.Start();
        DetermineConnectUrl();
        RefreshMonitors();
        LoadQuestionPacks();
    }

    public void RefreshMonitors()
    {
        AvailableMonitors.Clear();
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var bounds = s.Bounds;
            var info = new MonitorInfo(i, s.DeviceName, s.Primary, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            AvailableMonitors.Add(info);
        }

        if (AvailableMonitors.Count > 0)
        {
            var match = AvailableMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, Settings.SelectedMonitorDevice, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedMonitor = match;
            }
            else
            {
                // Default to secondary monitor if present, else primary
                SelectedMonitor = AvailableMonitors.Count > 1 ? AvailableMonitors[1] : AvailableMonitors[0];
            }
        }
    }

    partial void OnSelectedMonitorChanged(MonitorInfo? value)
    {
        if (value != null)
        {
            Settings.SelectedMonitorDevice = value.DeviceName;
            SaveSettings();
            TargetMonitorChanged?.Invoke(this, value.DeviceName);
        }
    }

    partial void OnAutoAdvanceQuestionsChanged(bool value)
    {
        Settings.AutoAdvanceQuestions = value;
        _engine.Settings.AutoAdvanceQuestions = value;
        SaveSettings();
    }

    partial void OnVenueNameChanged(string value)
    {
        Settings.VenueName = value;
        if (_engine != null && _engine.CurrentSession != null)
        {
            _engine.CurrentSession.VenueName = value;
        }
        if (_activeDisplayVm != null)
        {
            _activeDisplayVm.VenueName = value;
        }
        SaveSettings();
    }

    partial void OnHostNameChanged(string value)
    {
        Settings.HostName = value;
        if (_activeDisplayVm != null)
        {
            _activeDisplayVm.HostName = value;
        }
        SaveSettings();
    }

    partial void OnQuestionsPerGameChanged(int value)
    {
        Settings.QuestionsPerGame = value;
        _engine.Settings.QuestionsPerGame = value;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            string path = TriviaStorageHelper.GetSettingsPath();
            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private void DetermineConnectUrl()
    {
        string localIp = LocalNetworkHelper.GetLocalIPv4();
        ConnectUrl = $"http://{localIp}:{Settings.Port}";
        GenerateQrCode(ConnectUrl);
    }

    private void GenerateQrCode(string url)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(data);
            byte[] bytes = qrCode.GetGraphic(20, [0, 0, 0, 255], [255, 255, 255, 255]);

            var image = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            QrCodeImage = image;
        }
        catch { }
    }

    public void LoadQuestionPacks()
    {
        AvailablePacks.Clear();
        var packs = TriviaPackManager.LoadAllPacks();
        foreach (var p in packs)
        {
            AvailablePacks.Add(p);
            _dbService.SeedPackIntoDatabase(p);
        }

        if (AvailablePacks.Count > 0)
        {
            SelectedPack = AvailablePacks[0];
        }
    }

    partial void OnSelectedPackChanged(TriviaQuestionPack? value)
    {
        if (value != null && value.Questions.Count > 0)
        {
            // Shuffle questions when category is selected so questions are in randomized order
            var shuffled = value.Questions.OrderBy(_ => Random.Shared.Next()).ToList();
            ActiveQuestion = shuffled.FirstOrDefault();
            CurrentQuestionNumber = 1;
            int qCount = Math.Clamp(QuestionsPerGame, 1, shuffled.Count);
            TotalQuestionsInRound = qCount;
            CurrentRoundTitle = value.Title;
        }
    }

    [RelayCommand]
    private void StartGameWithSelectedPack()
    {
        if (SelectedPack == null || SelectedPack.Questions.Count == 0) return;

        // Reset game completion state
        IsGameComplete = false;
        WinnerAnnouncement = string.Empty;
        WinningTeamRoster = string.Empty;

        // Shuffle questions so each time a category is played, a fresh randomized order is used
        var shuffledQuestions = SelectedPack.Questions.OrderBy(_ => Random.Shared.Next()).ToList();
        int qCount = Math.Clamp(QuestionsPerGame, 1, shuffledQuestions.Count);
        var gameQuestions = shuffledQuestions.Take(qCount).ToList();

        var round = new TriviaRound
        {
            RoundNumber = 1,
            Title = SelectedPack.Title,
            Category = SelectedPack.Category,
            Questions = gameQuestions
        };

        _engine.StartGame([round], SelectedPack.Title);
        CurrentRoundTitle = round.Title;
        TotalQuestionsInRound = round.Questions.Count;
        CurrentQuestionNumber = 1;
        ActiveQuestion = round.Questions.FirstOrDefault();

        // Start 1st question automatically
        _engine.StartCurrentQuestion();
    }

    private void HandleGameCompleted(TriviaGameResult result)
    {
        IsGameComplete = true;
        WinnerAnnouncement = result.WinnerTitle;
        WinningTeamRoster = result.HasTeamWinner && result.WinningTeamMembers.Count > 0
            ? $"Team Members: {result.WinningTeamMembersRoster}"
            : "";
    }

    private void HandleIntermissionTick(int secondsRemaining)
    {
        IsIntermissionActive = secondsRemaining > 0;
        IntermissionSecondsRemaining = secondsRemaining;
        int mins = secondsRemaining / 60;
        int secs = secondsRemaining % 60;
        IntermissionCountdownText = $"{mins:D2}:{secs:D2}";
    }

    private void HandleIntermissionCompleted()
    {
        IsIntermissionActive = false;
        IntermissionSecondsRemaining = 0;

        // Auto-advance to next pack or reshuffle current
        if (AvailablePacks.Count > 1 && SelectedPack != null)
        {
            int currentIndex = AvailablePacks.IndexOf(SelectedPack);
            int nextIndex = (currentIndex + 1) % AvailablePacks.Count;
            SelectedPack = AvailablePacks[nextIndex];
        }
        StartGameWithSelectedPack();
    }

    public void OnProjectionOpened()
    {
        if (_engine.State == TriviaGameState.Lobby && !IsPreGameCountdownRunning)
        {
            IsPreGameCountdownRunning = true;
            _preGameTimer.Start();
            _activeDisplayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
        }
    }

    public void RegisterDisplayViewModel(DisplayViewModel dvm)
    {
        _activeDisplayVm = dvm;
        dvm.HostName = HostName;
        dvm.VenueName = VenueName;
        dvm.UpdateWifiCredentials(WifiSsid, WifiPassword);
        dvm.UpdatePreGameCountdown(PreGameSecondsRemaining);
        dvm.IsConnectInstructionsActive = IsShowingConnectScreen;
    }

    private void OnPreGameTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (IsPreGameCountdownRunning && PreGameSecondsRemaining > 0)
        {
            PreGameSecondsRemaining--;
            int mins = PreGameSecondsRemaining / 60;
            int secs = PreGameSecondsRemaining % 60;
            PreGameCountdownText = $"{mins:D2}:{secs:D2}";
            _activeDisplayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);

            if (PreGameSecondsRemaining == 0)
            {
                _preGameTimer.Stop();
                IsPreGameCountdownRunning = false;
                if (AutoStartAfterCountdown)
                {
                    Application.Current?.Dispatcher.Invoke(() => StartGameWithSelectedPack());
                }
            }
        }
    }

    [RelayCommand]
    private void StartPreGameCountdown(object? parameter)
    {
        int minutes = 5;
        if (parameter is int i) minutes = i;
        else if (parameter is string s && int.TryParse(s, out int parsed)) minutes = parsed;

        PreGameCountdownMinutes = minutes > 0 ? minutes : 5;
        PreGameSecondsRemaining = PreGameCountdownMinutes * 60;
        int mins = PreGameSecondsRemaining / 60;
        int secs = PreGameSecondsRemaining % 60;
        PreGameCountdownText = $"{mins:D2}:{secs:D2}";
        IsPreGameCountdownRunning = true;
        _preGameTimer.Stop();
        _preGameTimer.Start();
        _activeDisplayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
    }

    [RelayCommand]
    private void AddPreGameMinutes(object? parameter)
    {
        int deltaMinutes = 1;
        if (parameter is int i) deltaMinutes = i;
        else if (parameter is string s && int.TryParse(s, out int parsed)) deltaMinutes = parsed;

        PreGameSecondsRemaining = Math.Max(0, PreGameSecondsRemaining + (deltaMinutes * 60));
        int mins = PreGameSecondsRemaining / 60;
        int secs = PreGameSecondsRemaining % 60;
        PreGameCountdownText = $"{mins:D2}:{secs:D2}";
        _activeDisplayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
        if (!IsPreGameCountdownRunning && PreGameSecondsRemaining > 0)
        {
            IsPreGameCountdownRunning = true;
            _preGameTimer.Start();
        }
    }

    [RelayCommand]
    private void Add1Minute() => AddPreGameMinutes(1);

    [RelayCommand]
    private void Add5Minutes() => AddPreGameMinutes(5);

    [RelayCommand]
    private void Reset5Minutes() => StartPreGameCountdown(5);

    [RelayCommand]
    private void TogglePreGameTimer()
    {
        IsPreGameCountdownRunning = !IsPreGameCountdownRunning;
        if (IsPreGameCountdownRunning)
        {
            _preGameTimer.Start();
        }
        else
        {
            _preGameTimer.Stop();
        }
    }

    [RelayCommand]
    private void ToggleConnectInstructionsScreen()
    {
        IsShowingConnectScreen = !IsShowingConnectScreen;
        if (_activeDisplayVm != null)
        {
            _activeDisplayVm.IsConnectInstructionsActive = IsShowingConnectScreen;
        }
    }

    [RelayCommand]
    private void AutoDetectWifiSsid()
    {
        string? ssid = Lyracist.Shared.WifiHelper.GetConnectedSsid();
        if (!string.IsNullOrWhiteSpace(ssid))
        {
            WifiSsid = ssid;
            _activeDisplayVm?.UpdateWifiCredentials(WifiSsid, WifiPassword);
        }
    }

    [RelayCommand]
    private void SkipIntermissionAndStartNow()
    {
        _engine.SkipIntermission();
    }

    [RelayCommand]
    private void SaveAllSettings()
    {
        Settings.VenueName = VenueName;
        Settings.HostName = HostName;
        Settings.WifiSsid = WifiSsid;
        Settings.WifiPassword = WifiPassword;
        Settings.PreGameCountdownMinutes = PreGameCountdownMinutes;
        Settings.AutoStartAfterCountdown = AutoStartAfterCountdown;
        Settings.AutoAdvanceQuestions = AutoAdvanceQuestions;
        Settings.QuestionsPerGame = QuestionsPerGame;
        Settings.AutoStartNextGameEnabled = AutoStartNextGameEnabled;
        Settings.NextGameDelayMinutes = NextGameDelayMinutes;
        Settings.DefaultQuestionSeconds = DefaultQuestionSeconds;
        Settings.AnswerEliminationIntervalSeconds = AnswerEliminationIntervalSeconds;
        Settings.PostRevealDelaySeconds = PostRevealDelaySeconds;
        Settings.BasePointsPerQuestion = BasePointsPerQuestion;
        Settings.SpeedBonusEnabled = SpeedBonusEnabled;
        Settings.MaxSpeedBonus = MaxSpeedBonus;
        Settings.StreakBonusMultiplier = StreakBonusMultiplier;
        Settings.SoundEffectsEnabled = SoundEffectsEnabled;
        Settings.Port = Port;

        _engine.Settings = Settings;
        _activeDisplayVm?.UpdateWifiCredentials(WifiSsid, WifiPassword);
        SaveSettings();
    }

    [RelayCommand]
    private void RemovePlayer(TriviaPlayer? player)
    {
        if (player != null)
        {
            Players.Remove(player);
        }
    }

    [RelayCommand]
    private void StartQuestion()
    {
        _engine.StartCurrentQuestion();
    }

    [RelayCommand]
    private void LockAndReveal()
    {
        _engine.LockAndRevealAnswer();
    }

    [RelayCommand]
    private void ShowLeaderboard()
    {
        _engine.ShowLeaderboard();
    }

    [RelayCommand]
    private void NextQuestion()
    {
        bool hasNext = _engine.AdvanceToNextQuestion();
        if (hasNext && _engine.CurrentSession.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
            ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
        }
    }

    [RelayCommand]
    private void ToggleTimer()
    {
        if (IsTimerRunning)
        {
            _engine.PauseTimer();
            IsTimerRunning = false;
        }
        else
        {
            _engine.ResumeTimer();
            IsTimerRunning = true;
        }
    }

    private void HandleStateChanged(TriviaGameState newState)
    {
        GameState = newState;
        IsTimerRunning = (newState == TriviaGameState.QuestionActive || newState == TriviaGameState.EliminatingAnswers);
        if (_engine.CurrentSession.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
            ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
        }
    }

    private void HandleTimerTick(int secondsRemaining)
    {
        RemainingSeconds = secondsRemaining;
        TotalCountdownSeconds = _engine.TotalCountdownSeconds;
        IsWarningActive = _engine.IsInWarningCountdown;
    }

    private void HandleQuestionStarted(TriviaQuestion q)
    {
        ActiveQuestion = q;
        CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
        RemainingSeconds = _engine.RemainingSeconds;
        TotalCountdownSeconds = _engine.TotalCountdownSeconds;
        IsWarningActive = false;
        IsTimerRunning = true;
        AnswerDistribution.Clear();
    }

    private void HandleAnswerRevealed(TriviaQuestion q)
    {
        IsTimerRunning = false;
        IsWarningActive = false;

        // Build answer distribution chart
        AnswerDistribution.Clear();
        var dist = _engine.GetAnswerDistribution();
        string[] labels = ["A", "B", "C", "D"];
        for (int i = 0; i < 4; i++)
        {
            int count = dist.GetValueOrDefault(i, 0);
            string text = (q.Options.Count > i) ? q.Options[i] : $"Option {labels[i]}";
            bool isCorrect = (i == q.CorrectAnswerIndex);
            AnswerDistribution.Add(new AnswerDistributionItem(labels[i], text, count, isCorrect));
        }
    }

    private void RefreshPlayers(List<TriviaPlayer> playerList)
    {
        Players.Clear();
        foreach (var p in playerList)
        {
            Players.Add(p);
        }
        ConnectedPlayerCount = Players.Count(p => p.IsConnected);
    }

    public void Dispose()
    {
        _webServer.Dispose();
        _engine.Dispose();
        _dbService.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record AnswerDistributionItem(string Label, string Text, int Count, bool IsCorrect);
