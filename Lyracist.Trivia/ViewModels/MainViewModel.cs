// Edited on Aug 22, 2026 @ 11:20:00 -> Added manual DJ/GameMaster flow commands, question navigation, timer bump/trim, and instant controls
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
using Lyracist.Trivia.Services;
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
    private string _instructionBannerText = TriviaSettings.DefaultInstructionBannerText;

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

    partial void OnCurrentQuestionNumberChanged(int value)
    {
        if (_engine?.CurrentSession?.CurrentRound != null)
        {
            int targetIdx = value - 1;
            if (targetIdx >= 0 && targetIdx < _engine.CurrentSession.CurrentRound.Questions.Count)
            {
                if (_engine.CurrentSession.CurrentQuestionIndex != targetIdx)
                {
                    _engine.GoToQuestion(targetIdx, startTimerImmediately: AutoAdvanceQuestions);
                    ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
                    IsTimerRunning = AutoAdvanceQuestions;
                }
            }
        }
    }

    [ObservableProperty]
    private int _totalQuestionsInRound = 5;

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

    /// 0 (or blank) means unlimited - auto-start just keeps looping forever like before. A
    /// positive value preloads that many games' worth of questions up front (see
    /// StartGameWithSelectedPack/_preloadedGameQuestionSets) and stops auto-restarting once
    /// TriviaGameEngine.HasReachedGamesCap trips.
    [ObservableProperty]
    private int _totalGamesToPlay = 0;

    private List<List<TriviaQuestion>>? _preloadedGameQuestionSets;
    private int _preloadedGameIndex;

    public string GameProgressText => TotalGamesToPlay > 0
        ? $"Game {Math.Min(_engine.GamesPlayedCount, TotalGamesToPlay)} of {TotalGamesToPlay}"
        : string.Empty;

    partial void OnTotalGamesToPlayChanged(int value)
    {
        // Invalidate any in-progress preload so the next Start Game click rebuilds against the
        // new count instead of continuing to hand out games sized/counted for the old value.
        _preloadedGameQuestionSets = null;
        _preloadedGameIndex = 0;
        Settings.TotalGamesToPlay = value;
        if (_engine != null) _engine.Settings.TotalGamesToPlay = value;
        OnPropertyChanged(nameof(GameProgressText));
        SaveSettings();
    }

    [ObservableProperty]
    private int _defaultQuestionSeconds = 15;

    [ObservableProperty]
    private int _answerEliminationIntervalSeconds = 5;

    [ObservableProperty]
    private int _postRevealDelaySeconds = 5;

    /// Live summary of the auto-run timing (shown under the "Auto-Run Game" toggle) so the
    /// header always reflects the game master's actual configured timings instead of a
    /// hardcoded "15s answer • 5s fade • 5s reveal" that goes stale the moment they're changed.
    public string AutoRunTimingSummary =>
        $"{DefaultQuestionSeconds}s answer • {AnswerEliminationIntervalSeconds}s fade • {PostRevealDelaySeconds}s reveal";

    partial void OnDefaultQuestionSecondsChanged(int value)
    {
        Settings.DefaultQuestionSeconds = value;
        if (_engine != null) _engine.Settings.DefaultQuestionSeconds = value;
        OnPropertyChanged(nameof(AutoRunTimingSummary));
        SaveSettings();
    }

    public int[] QuestionsPerGamePresets { get; } = [5, 10, 15, 20, 25, 50, 100];
    public ObservableCollection<MonitorInfo> AvailableMonitors { get; } = [];
    public ObservableCollection<TriviaQuestionPack> AvailablePacks { get; } = [];
    public ObservableCollection<SelectableTriviaPack> SelectablePacks { get; } = [];
    public ObservableCollection<TriviaPlayer> Players { get; } = [];
    public ObservableCollection<AnswerDistributionItem> AnswerDistribution { get; } = [];
    public ObservableCollection<TriviaHelpTopic> HelpTopics { get; } = [];
    public ObservableCollection<AnnouncementHelper.AnnouncementImage> AvailableAnnouncements { get; } = [];
    public ObservableCollection<int> AvailableQuestionNumbers { get; } = [];

    partial void OnAnswerEliminationIntervalSecondsChanged(int value)
    {
        Settings.AnswerEliminationIntervalSeconds = value;
        if (_engine != null) _engine.Settings.AnswerEliminationIntervalSeconds = value;
        OnPropertyChanged(nameof(AutoRunTimingSummary));
        SaveSettings();
    }

    partial void OnPostRevealDelaySecondsChanged(int value)
    {
        Settings.PostRevealDelaySeconds = value;
        if (_engine != null) _engine.Settings.PostRevealDelaySeconds = value;
        OnPropertyChanged(nameof(AutoRunTimingSummary));
        SaveSettings();
    }

    partial void OnBasePointsPerQuestionChanged(int value)
    {
        Settings.BasePointsPerQuestion = value;
        if (_engine != null) _engine.Settings.BasePointsPerQuestion = value;
        SaveSettings();
    }

    partial void OnWrongAnswerDeductionPointsChanged(int value)
    {
        Settings.WrongAnswerDeductionPoints = value;
        if (_engine != null) _engine.Settings.WrongAnswerDeductionPoints = value;
        SaveSettings();
    }

    partial void OnTieredScoringEnabledChanged(bool value)
    {
        Settings.TieredScoringEnabled = value;
        if (_engine != null) _engine.Settings.TieredScoringEnabled = value;
        SaveSettings();
    }

    partial void OnPoints4OptionsPercentChanged(int value)
    {
        Settings.Points4OptionsPercent = value;
        if (_engine != null) _engine.Settings.Points4OptionsPercent = value;
        SaveSettings();
    }

    partial void OnPoints3OptionsPercentChanged(int value)
    {
        Settings.Points3OptionsPercent = value;
        if (_engine != null) _engine.Settings.Points3OptionsPercent = value;
        SaveSettings();
    }

    partial void OnPoints2OptionsPercentChanged(int value)
    {
        Settings.Points2OptionsPercent = value;
        if (_engine != null) _engine.Settings.Points2OptionsPercent = value;
        SaveSettings();
    }

    partial void OnSpeedBonusEnabledChanged(bool value)
    {
        Settings.SpeedBonusEnabled = value;
        if (_engine != null) _engine.Settings.SpeedBonusEnabled = value;
        SaveSettings();
    }

    partial void OnMaxSpeedBonusChanged(int value)
    {
        Settings.MaxSpeedBonus = value;
        if (_engine != null) _engine.Settings.MaxSpeedBonus = value;
        SaveSettings();
    }

    partial void OnStreakBonusMultiplierChanged(double value)
    {
        Settings.StreakBonusMultiplier = value;
        if (_engine != null) _engine.Settings.StreakBonusMultiplier = value;
        SaveSettings();
    }

    partial void OnSoundEffectsEnabledChanged(bool value)
    {
        Settings.SoundEffectsEnabled = value;
        if (_engine != null) _engine.Settings.SoundEffectsEnabled = value;
        SaveSettings();
    }

    partial void OnPortChanged(int value)
    {
        Settings.Port = value;
        if (_engine != null) _engine.Settings.Port = value;
        SaveSettings();
    }

    partial void OnWifiSsidChanged(string value)
    {
        Settings.WifiSsid = value;
        if (_engine != null) _engine.Settings.WifiSsid = value;
        _activeDisplayVm?.UpdateWifiCredentials(WifiSsid, WifiPassword);
        SaveSettings();
    }

    partial void OnWifiPasswordChanged(string value)
    {
        Settings.WifiPassword = value;
        if (_engine != null) _engine.Settings.WifiPassword = value;
        _activeDisplayVm?.UpdateWifiCredentials(WifiSsid, WifiPassword);
        SaveSettings();
    }

    partial void OnPreGameCountdownMinutesChanged(int value)
    {
        Settings.PreGameCountdownMinutes = value;
        if (_engine != null) _engine.Settings.PreGameCountdownMinutes = value;
        SaveSettings();
    }

    partial void OnAutoStartAfterCountdownChanged(bool value)
    {
        Settings.AutoStartAfterCountdown = value;
        if (_engine != null) _engine.Settings.AutoStartAfterCountdown = value;
        SaveSettings();
    }

    partial void OnAutoStartNextGameEnabledChanged(bool value)
    {
        Settings.AutoStartNextGameEnabled = value;
        if (_engine != null) _engine.Settings.AutoStartNextGameEnabled = value;
        SaveSettings();
    }

    partial void OnNextGameDelayMinutesChanged(int value)
    {
        Settings.NextGameDelayMinutes = value;
        if (_engine != null) _engine.Settings.NextGameDelayMinutes = value;
        SaveSettings();
    }

    [ObservableProperty]
    private int _basePointsPerQuestion = 1000;

    [ObservableProperty]
    private int _wrongAnswerDeductionPoints = 0;

    [ObservableProperty]
    private bool _tieredScoringEnabled = true;

    [ObservableProperty]
    private int _points4OptionsPercent = 100;

    [ObservableProperty]
    private int _points3OptionsPercent = 70;

    [ObservableProperty]
    private int _points2OptionsPercent = 40;

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

    private void UpdateAvailableQuestionNumbers(int count)
    {
        AvailableQuestionNumbers.Clear();
        for (int i = 1; i <= count; i++)
        {
            AvailableQuestionNumbers.Add(i);
        }
    }

    [ObservableProperty]
    private AnnouncementHelper.AnnouncementImage? _selectedAnnouncement;

    partial void OnSelectedAnnouncementChanged(AnnouncementHelper.AnnouncementImage? value)
    {
        AnnouncementHelper.SaveSelectedFileName(value?.FileName);
    }

    [RelayCommand]
    private void ShowAnnouncement()
    {
        if (SelectedAnnouncement == null) return;

        if (_activeDisplayVm == null)
        {
            // Synchronously opens the projection window and registers _activeDisplayVm via
            // MainWindow's RequestOpenProjectionWindow handler before this call returns.
            RequestOpenProjectionWindow?.Invoke(this, EventArgs.Empty);
        }

        _activeDisplayVm?.ShowAnnouncement(SelectedAnnouncement.FullPath, SelectedAnnouncement.DisplayName);
    }

    [RelayCommand]
    private void HideAnnouncement()
    {
        _activeDisplayVm?.DismissAnnouncement();
    }

    [ObservableProperty]
    private TriviaHelpTopic? _selectedHelpTopic;

    public string AppName => "Lyracist Trivia Pro";
    public string AppVersion => "26.8.19.20";
    public string Company => "PAROLE Software";
    public string Author => "Dennis N. Maidon";
    public string Copyright => "Copyright © 2026 PAROLE Software. All rights reserved.";
    public string AppDescription => "Interactive live pub & bar trivia hosting engine with synchronized mobile player buzzers, dynamic custom database auto-discovery, 14 starter curated category databases (2,100 questions), dual-screen 70:30 pre-game lobby with 16:9 category announcement banners, multi-monitor projection, dynamic speed/streak scoring, and seamless karaoke integration.";

    public TriviaGameEngine Engine => _engine;

    public event EventHandler<string>? TargetMonitorChanged;
    public event EventHandler? RequestOpenProjectionWindow;

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
        _instructionBannerText = string.IsNullOrWhiteSpace(_settings.InstructionBannerText) ? TriviaSettings.DefaultInstructionBannerText : _settings.InstructionBannerText;
        _autoAdvanceQuestions = _settings.AutoAdvanceQuestions;
        _questionsPerGame = _settings.QuestionsPerGame > 0 ? _settings.QuestionsPerGame : 10;
        _autoStartNextGameEnabled = _settings.AutoStartNextGameEnabled;
        _nextGameDelayMinutes = _settings.NextGameDelayMinutes > 0 ? _settings.NextGameDelayMinutes : 3;
        _totalGamesToPlay = Math.Max(0, _settings.TotalGamesToPlay);
        _defaultQuestionSeconds = _settings.DefaultQuestionSeconds > 0 ? _settings.DefaultQuestionSeconds : 15;
        _answerEliminationIntervalSeconds = _settings.AnswerEliminationIntervalSeconds > 0 ? _settings.AnswerEliminationIntervalSeconds : 5;
        _postRevealDelaySeconds = _settings.PostRevealDelaySeconds > 0 ? _settings.PostRevealDelaySeconds : 5;
        _basePointsPerQuestion = _settings.BasePointsPerQuestion > 0 ? _settings.BasePointsPerQuestion : 1000;
        _wrongAnswerDeductionPoints = _settings.WrongAnswerDeductionPoints;
        _tieredScoringEnabled = _settings.TieredScoringEnabled;
        _points4OptionsPercent = _settings.Points4OptionsPercent;
        _points3OptionsPercent = _settings.Points3OptionsPercent;
        _points2OptionsPercent = _settings.Points2OptionsPercent;
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
        _engine.StateChanged += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleStateChanged(e));
        _engine.TimerTick += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleTimerTick(e));
        _engine.QuestionStarted += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleQuestionStarted(e));
        _engine.AnswerRevealed += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleAnswerRevealed(e));
        _engine.LeaderboardUpdated += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => RefreshPlayers(e));
        _engine.GameCompleted += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleGameCompleted(e));
        _engine.IntermissionTick += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleIntermissionTick(e));
        _engine.IntermissionCompleted += (s, e) => Application.Current?.Dispatcher.InvokeAsync(() => HandleIntermissionCompleted());

        // Setup Pre-Game ticker (started when screen is cast or manually started)
        _isPreGameCountdownRunning = false;
        _preGameTimer.Elapsed += OnPreGameTimerTick;

        // Start WebServer, populate monitors & load packs
        _webServer.Start();
        DetermineConnectUrl();
        RefreshMonitors();
        LoadQuestionPacks();
        LoadAnnouncements();
        InitializeHelpTopics();
    }

    public void LoadAnnouncements()
    {
        AvailableAnnouncements.Clear();
        foreach (var a in AnnouncementHelper.GetAvailableAnnouncements())
        {
            AvailableAnnouncements.Add(a);
        }

        string? lastSelected = AnnouncementHelper.LoadSelectedFileName();
        SelectedAnnouncement = lastSelected != null
            ? AvailableAnnouncements.FirstOrDefault(a => a.FileName == lastSelected)
            : null;
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

    partial void OnInstructionBannerTextChanged(string value)
    {
        Settings.InstructionBannerText = value;
        _activeDisplayVm?.UpdateInstructionBannerTemplate(value);
        SaveSettings();
    }

    partial void OnQuestionsPerGameChanged(int value)
    {
        Settings.QuestionsPerGame = value;
        _engine.Settings.QuestionsPerGame = value;
        SaveSettings();
        UpdateSelectedPacksPreview();
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
        SelectablePacks.Clear();
        var packs = TriviaPackManager.LoadAllPacks();
        bool isFirst = true;
        foreach (var p in packs)
        {
            AvailablePacks.Add(p);
            _dbService.SeedPackIntoDatabase(p);

            var selectable = new SelectableTriviaPack(p, isChecked: isFirst);
            selectable.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectableTriviaPack.IsChecked))
                {
                    UpdateSelectedPacksPreview();
                }
            };
            SelectablePacks.Add(selectable);
            isFirst = false;
        }

        if (AvailablePacks.Count == 0)
        {
            // On a machine where the resolved TriviaData directory doesn't hold the expected
            // packs (e.g. a fresh install away from the dev box), the host would otherwise just
            // see an empty category list with no clue why. Leave a breadcrumb in the log folder.
            Lyracist.Shared.Globals.LogError("Lyracist.Trivia",
                $"No trivia question packs found in '{TriviaPackManager.GetDefaultPacksDirectory()}'. Category list will be empty until a .json pack is placed there.",
                "LoadQuestionPacks");
        }

        UpdateSelectedPacksPreview();
    }

    /// <summary>
    /// Returns every checked pack, or the first available pack if none are checked (so "Start
    /// Game" always has something to play instead of silently doing nothing).
    /// </summary>
    private List<TriviaQuestionPack> GetCheckedPacks()
    {
        var checkedPacks = SelectablePacks.Where(sp => sp.IsChecked).Select(sp => sp.Pack).ToList();
        if (checkedPacks.Count > 0) return checkedPacks;
        return AvailablePacks.Count > 0 ? [AvailablePacks[0]] : [];
    }

    private void UpdateSelectedPacksPreview()
    {
        var checkedPacks = GetCheckedPacks();
        if (checkedPacks.Count == 0) return;

        CurrentQuestionNumber = 1;
        TotalQuestionsInRound = Math.Clamp(QuestionsPerGame, 1, checkedPacks.Sum(p => p.Questions.Count));
        UpdateAvailableQuestionNumbers(TotalQuestionsInRound);
        _activeDisplayVm?.UpdateFeaturedCategoryHeader(TotalQuestionsInRound, checkedPacks.Count);

        if (checkedPacks.Count == 1)
        {
            CurrentRoundTitle = checkedPacks[0].Title;
            ActiveQuestion = checkedPacks[0].Questions.FirstOrDefault();
            _activeDisplayVm?.UpdateCategory(checkedPacks[0].Category, checkedPacks[0].Description);
        }
        else
        {
            CurrentRoundTitle = string.Join(" + ", checkedPacks.Select(p => p.Title));
            ActiveQuestion = checkedPacks[0].Questions.FirstOrDefault();
            _activeDisplayVm?.UpdateMixedCategory(checkedPacks);
        }
    }

    [RelayCommand]
    private void LaunchPreGameLobby()
    {
        _activeDisplayVm?.DismissAnnouncement();
        IsShowingConnectScreen = true;
        if (!IsPreGameCountdownRunning)
        {
            if (PreGameSecondsRemaining <= 0)
            {
                PreGameSecondsRemaining = PreGameCountdownMinutes * 60;
                int mins = PreGameSecondsRemaining / 60;
                int secs = PreGameSecondsRemaining % 60;
                PreGameCountdownText = $"{mins:D2}:{secs:D2}";
            }
            IsPreGameCountdownRunning = true;
            _preGameTimer.Start();
        }
        _activeDisplayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
        UpdateSelectedPacksPreview();
        RequestOpenProjectionWindow?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void StartGameWithSelectedPack()
    {
        var checkedPacks = GetCheckedPacks();
        if (checkedPacks.Count == 0) return;

        // Cancel any running pre-game countdown and dismiss the announcement/lobby/connect screen
        _preGameTimer.Stop();
        IsPreGameCountdownRunning = false;
        IsShowingConnectScreen = false;
        if (_activeDisplayVm != null)
        {
            _activeDisplayVm.DismissAnnouncement();
            _activeDisplayVm.IsConnectInstructionsActive = false;
            _activeDisplayVm.IsPreGameCountdownRunning = false;
        }

        // Reset game completion state
        IsGameComplete = false;
        WinnerAnnouncement = string.Empty;
        WinningTeamRoster = string.Empty;

        List<TriviaQuestion> gameQuestions;
        if (TotalGamesToPlay > 1)
        {
            // Preload every game in this run up front so no question repeats across the whole
            // session (e.g. 3 games of 20), instead of drawing each game's set independently
            // right before it starts. Rebuilds whenever the cache is missing, stale (Total
            // Games changed), or exhausted - naturally covering both "starting a brand new
            // session" and "starting another one manually after the last capped run finished".
            if (_preloadedGameQuestionSets == null || _preloadedGameQuestionSets.Count != TotalGamesToPlay || _preloadedGameIndex >= _preloadedGameQuestionSets.Count)
            {
                _preloadedGameQuestionSets = TriviaPackManager.BuildMultiGameQuestionSets(checkedPacks, QuestionsPerGame, TotalGamesToPlay);
                _preloadedGameIndex = 0;
            }

            if (_preloadedGameQuestionSets.Count == 0) return;
            gameQuestions = _preloadedGameQuestionSets[_preloadedGameIndex];
            _preloadedGameIndex++;
        }
        else
        {
            _preloadedGameQuestionSets = null;
            _preloadedGameIndex = 0;

            // Pool every checked pack's questions together and draw this game's set fresh - see
            // TriviaPackManager.BuildMixedQuestionSet for the double-draw-then-rescramble
            // algorithm. Called again on every unattended auto-restart too, so the question set
            // is different every game even with the exact same packs checked.
            gameQuestions = TriviaPackManager.BuildMixedQuestionSet(checkedPacks, QuestionsPerGame);
        }
        if (gameQuestions.Count == 0) return;

        string title = checkedPacks.Count == 1 ? checkedPacks[0].Title : string.Join(" + ", checkedPacks.Select(p => p.Title));
        string category = checkedPacks.Count == 1 ? checkedPacks[0].Category : "Mixed Trivia";

        var round = new TriviaRound
        {
            RoundNumber = 1,
            Title = title,
            Category = category,
            Questions = gameQuestions
        };

        _engine.StartGame([round], title);
        OnPropertyChanged(nameof(GameProgressText));
        CurrentRoundTitle = round.Title;
        TotalQuestionsInRound = round.Questions.Count;
        UpdateAvailableQuestionNumbers(TotalQuestionsInRound);
        CurrentQuestionNumber = 1;
        ActiveQuestion = round.Questions.FirstOrDefault();

        // Start 1st question (immediate timer if AutoAdvance, or ready/reading state if manual)
        _engine.PrepareCurrentQuestion(startTimerImmediately: AutoAdvanceQuestions);
        IsTimerRunning = AutoAdvanceQuestions;

        // Ensure big screen opens / focuses directly to active gameplay
        RequestOpenProjectionWindow?.Invoke(this, EventArgs.Empty);
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

        // Whatever packs are checked stays checked between games - BuildMixedQuestionSet draws a
        // fresh random question set every time it's called, so this alone gives a different game
        // each time without needing to cycle to a different pack.
        StartGameWithSelectedPack();
    }

    public void OnProjectionOpened()
    {
        if (_engine.State == TriviaGameState.Lobby && IsShowingConnectScreen && !IsPreGameCountdownRunning)
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
        dvm.UpdateInstructionBannerTemplate(InstructionBannerText);
        dvm.UpdateWifiCredentials(WifiSsid, WifiPassword);
        dvm.UpdatePreGameCountdown(PreGameSecondsRemaining);
        dvm.IsConnectInstructionsActive = (_engine.State == TriviaGameState.Lobby) && IsShowingConnectScreen;
        UpdateSelectedPacksPreview();
        dvm.SyncWithEngine();
    }

    private void OnPreGameTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // This handler fires on the Timer's threadpool thread. Every property touched below is
        // bound to WPF UI (the game master panel and/or the projection window), so it must be
        // marshaled onto the UI thread like every other engine/timer callback in this class -
        // otherwise a cross-thread binding exception can crash the whole app during the
        // unattended pre-game wait, before anyone is at the keyboard to notice.
        Application.Current?.Dispatcher.Invoke(() =>
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
                        StartGameWithSelectedPack();
                    }
                }
            }
        });
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
        Settings.InstructionBannerText = InstructionBannerText;
        Settings.WifiSsid = WifiSsid;
        Settings.WifiPassword = WifiPassword;
        Settings.PreGameCountdownMinutes = PreGameCountdownMinutes;
        Settings.AutoStartAfterCountdown = AutoStartAfterCountdown;
        Settings.AutoAdvanceQuestions = AutoAdvanceQuestions;
        Settings.QuestionsPerGame = QuestionsPerGame;
        Settings.AutoStartNextGameEnabled = AutoStartNextGameEnabled;
        Settings.NextGameDelayMinutes = NextGameDelayMinutes;
        Settings.TotalGamesToPlay = TotalGamesToPlay;
        Settings.DefaultQuestionSeconds = DefaultQuestionSeconds;
        Settings.AnswerEliminationIntervalSeconds = AnswerEliminationIntervalSeconds;
        Settings.PostRevealDelaySeconds = PostRevealDelaySeconds;
        Settings.BasePointsPerQuestion = BasePointsPerQuestion;
        Settings.WrongAnswerDeductionPoints = WrongAnswerDeductionPoints;
        Settings.TieredScoringEnabled = TieredScoringEnabled;
        Settings.Points4OptionsPercent = Points4OptionsPercent;
        Settings.Points3OptionsPercent = Points3OptionsPercent;
        Settings.Points2OptionsPercent = Points2OptionsPercent;
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
            _engine.RemovePlayer(player.Name);
            Players.Remove(player);
        }
    }

    [RelayCommand]
    private void StartQuestion()
    {
        _engine.StartCurrentQuestion();
        IsTimerRunning = true;
    }

    [RelayCommand]
    private void PreviousQuestion()
    {
        bool hasPrev = _engine.PreviousQuestion(startTimerImmediately: AutoAdvanceQuestions);
        if (hasPrev && _engine.CurrentSession.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
            ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
            IsTimerRunning = AutoAdvanceQuestions;
        }
    }

    [RelayCommand]
    private void NextQuestion()
    {
        bool hasNext = _engine.AdvanceToNextQuestion(startTimerImmediately: AutoAdvanceQuestions);
        if (hasNext && _engine.CurrentSession.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
            ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
            IsTimerRunning = AutoAdvanceQuestions;
        }
    }

    [RelayCommand]
    private void GoToQuestion(object? parameter)
    {
        int qNum = 1;
        if (parameter is int i) qNum = i;
        else if (parameter is string s && int.TryParse(s, out int parsed)) qNum = parsed;

        if (qNum >= 1)
        {
            bool ok = _engine.GoToQuestion(qNum - 1, startTimerImmediately: AutoAdvanceQuestions);
            if (ok && _engine.CurrentSession.CurrentQuestion != null)
            {
                CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
                ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
                IsTimerRunning = AutoAdvanceQuestions;
            }
        }
    }

    [RelayCommand]
    private void AddTimerSeconds(object? parameter)
    {
        int seconds = 5;
        if (parameter is int i) seconds = i;
        else if (parameter is string s && int.TryParse(s, out int parsed)) seconds = parsed;

        _engine.AdjustRemainingSeconds(seconds);
    }

    [RelayCommand]
    private void ResetTimer()
    {
        _engine.ResetQuestionTimer();
        IsTimerRunning = true;
    }

    [RelayCommand]
    private void EliminateNextWrong()
    {
        _engine.EliminateNextWrongAnswer();
    }

    [RelayCommand]
    private void InstantReveal()
    {
        _engine.InstantRevealAnswer();
    }

    [RelayCommand]
    private void VoidCurrentQuestion()
    {
        _engine.VoidCurrentQuestion();
        if (_engine.CurrentSession.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
            ActiveQuestion = _engine.CurrentSession.CurrentQuestion;
            IsTimerRunning = AutoAdvanceQuestions;
        }
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

    private void InitializeHelpTopics()
    {
        HelpTopics.Clear();
        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "🎯 1. Game Master Command Deck",
            Icon = "🎯",
            AccentColor = "#38BDF8",
            DescriptionHeader = "Live Trivia Host Controls, Manual Flow & Keyboard Shortcuts",
            DescriptionContent = "• Category Pack Selection: Choose from any of the 14 built-in categories or custom JSON packs in TriviaData/packs/. The pack's questions, rules, and banner load immediately.\n\n• 🎯 1-Click Launch Pre-Game Lobby: Automatically opens and focuses the TV projection window on the configured monitor, syncs the 16:9 category banner, starts the pre-game countdown clock, and activates the lobby marquee.\n\n• ▶ Start Game Now: Launches Question #1 immediately.\n\n• ⚡ Auto-Run vs 🎮 Manual DJ Mode: When Auto-Run is disabled, the game master controls the exact pacing: questions open in reading/standby mode, allowing the host to read the question over the microphone before starting the timer (Spacebar).\n\n• ⏱️ Live Timer Bump & Pacing: Instantly add or subtract countdown time ([-5s], [+5s], [+10s], [🔄 Reset]) on the fly to accommodate crowd discussion or Wi-Fi delays.\n\n• ⏭ Question Navigation & Direct Jump: Step backward (⏮ Prev) or forward (⏭ Next), or use the question number dropdown to jump directly to any question in the round.\n\n• ✂ Staged Fade, ⚡ Instant Reveal & ❌ Voiding: Manually eliminate wrong options one by one, immediately reveal correct answers, or nullify flawed questions without penalizing player scores.\n\n• ⌨️ DJ Keyboard Shortcuts: Hands-on-keyboard shortcuts for live hosting: Spacebar (Pause/Resume Timer), Right Arrow / PageDown (Next Question), Left Arrow / PageUp (Previous Question)."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "📺 2. 70:30 Pre-Game Lobby & 16:9 Banners",
            Icon = "📺",
            AccentColor = "#A78BFA",
            DescriptionHeader = "High-Impact Projection Layout with Dual QR Codes",
            DescriptionContent = "• 70:30 Proportional Screen Layout: Specifically engineered for 16:9 and ultrawide projector screens:\n  - Left Hero Column (70%): Displays high-resolution 1920x1080 category announcement banners (TriviaData/Banners/*.png) at maximum size with glow borders and ambient theme lighting.\n  - Right Onboarding Stack (30%): Features a high-contrast digital countdown clock, Wi-Fi Scan-to-Connect QR card, and Mobile Buzzer Join QR card stacked vertically for effortless phone scanning.\n\n• Bottom Connection & Copyright Bar: Shows the direct mobile URL (http://<LAN-IP>:8085/trivia) and company copyright banner across the bottom.\n\n• Full-Width Scrolling Ticker: Continuously scrolls custom venue announcements, host branding, rules, and buzzer tips along the top/bottom."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "⚙️ 3. Settings & Timer Override Rules",
            Icon = "⚙️",
            AccentColor = "#F59E0B",
            DescriptionHeader = "Authoritative Timer Control, Tiered Option Value Scoring & Game Customization",
            DescriptionContent = "• ⚡ Tiered Option Value Scoring (100% / 70% / 40%):\n  - 4 Options Visible (0 Eliminated): 100% Base Points (1,000 pts default). Awards early, confident buzz-ins before options fade.\n  - 3 Options Visible (1 Eliminated): 70% Base Points (700 pts default). Fades 1st wrong option at 2/3 countdown.\n  - 2 Options Visible (50/50): 40% Base Points (400 pts default). Fades 2nd wrong option at 1/3 countdown.\n  - Early knowledge enjoys a decisive 2.5× advantage while casual patrons can still score on 50/50 guesses.\n\n• ⏱️ Question Answer Window (Game Master Override):\n  - The 'Question Answer Window' setting in the Settings tab (default: 15s) is authoritative for the live game.\n  - If the Game Master changes this setting to 10s, 20s, 25s, 30s, etc., all questions will count down using the Game Master's configured duration, overriding the 15s factory default in the question JSON files.\n\n• ❌ Wrong Answer Elimination: Automatically eliminates incorrect answer choices at configured intervals during the countdown, narrowing choices for remaining players.\n\n• ⏳ 5-Second Reveal Buffer: Displays the correct answer, points breakdown, and explanation for 5 seconds before advancing.\n\n• 🎯 Scoring & Speed Bonuses: Base score of 1,000 pts per correct answer, with up to +500 speed bonus for fast buzzers, and a +10% streak multiplier for consecutive correct answers.\n\n• ⚠️ Wrong Answer Deductions: Optional point deduction (0 to -500 pts) for incorrect submissions to discourage blind guessing.\n\n• 🔄 Auto-Start Next Game: Configurable intermission timer (e.g. 3 minutes) that automatically launches the next game when the current session concludes."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "📱 4. Mobile Buzzer Web App",
            Icon = "📱",
            AccentColor = "#10B981",
            DescriptionHeader = "Zero-Install Web App for Phones & Tablets (http://<IP>:8085/trivia)",
            DescriptionContent = "• Zero App Store Downloads: Players simply connect to the venue Wi-Fi and scan the Join QR code or open http://<IP>:8085/trivia in Safari, Chrome, Edge, or Firefox.\n\n• One-Click Registration: Players enter their name and optional Team Name to join the live session instantly.\n\n• 4-Button Color Buzzer: Responsive touch buttons (Red A, Blue B, Green C, Yellow D) with instant haptic vibration and locked-in confirmation.\n\n• Real-Time Answer Elimination: Eliminated incorrect options visually fade and disable on players' screens in real-time.\n\n• Personal Leaderboard & Ranking: After each question and at game completion, players see their current placement, streak bonus, total points, and team standings."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "👥 5. Players & Teams Management",
            Icon = "👥",
            AccentColor = "#EC4899",
            DescriptionHeader = "Live Roster, Team Aggregation & Player Moderation",
            DescriptionContent = "• Live Player Roster: Displays all connected players with real-time total scores, active answer streaks, correct count, and total answers attempted.\n\n• Team Scoring: Players can group into teams under a shared team name. Team scores aggregate automatically on the leaderboard.\n\n• Kick Player Control: Game Masters can click 'Kick' next to any player row to instantly disconnect and remove disruptive or test entries."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "🗄️ 6. Category Databases & Custom Packs",
            Icon = "🗄️",
            AccentColor = "#6366F1",
            DescriptionHeader = "Starter Categories, Auto-Discovery & Adding Custom Databases",
            DescriptionContent = "• Dynamic Auto-Discovery: The game engine dynamically scans the TriviaData/packs/ folder on startup and loads all available JSON question packs into the category selector.\n\n• Adding Custom Databases:\n  - Users can add their own custom category databases at any time. Simply place a new JSON file (e.g. disney_trivia.json) into the TriviaData/packs/ directory.\n  - Each question requires a Prompt, 4 Options, CorrectAnswerIndex (0-3), and an optional Explanation.\n  - To display a custom 16:9 pre-game banner, place a matching PNG or JPG image (e.g. disney_trivia.png) in TriviaData/Banners/. If no image is provided, the lobby automatically generates a sleek category card.\n\n• Pre-Loaded Starter Library:\n  - Includes 15 starter categories with 150 questions each (2,250 total curated questions) spanning Bikers, Rock & Roll, Country, Pop Culture, Movies, TV, Geography, Capitals, History, Sports, Slogans, and Pub Trivia.\n\n• Automatic SQLite Database Seeding: Discovered questions are synchronized into TriviaData/trivia.db with full offline capability."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "🖥️ 7. Multi-Monitor & Shortcut Keys",
            Icon = "🖥️",
            AccentColor = "#14B8A6",
            DescriptionHeader = "Projection Controls, Display Management & Deconfliction",
            DescriptionContent = "• Target Monitor Selection: Select the projection screen from the monitor dropdown (supports primary, secondary, and projector screens with per-monitor DPI scaling).\n\n• Quick Window & Exit Controls:\n  - ✕ Exit Button: 1-click exit button in the Game Master header bar cleanly terminates all web servers, timers, and background tasks.\n  - Esc Key: Closes the TV projection window immediately.\n  - Floating ✕ Button: Discreet close button in the upper-right corner of the projection screen.\n  - F11 Key: Toggles borderless fullscreen mode.\n\n• Karaoke Integration: If run alongside Lyracist or KsRotation, trivia games automatically pause with a 'DJ Banner Active' notice whenever full-screen DJ banners are projected."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "📝 8. JSON Database Format & Schema",
            Icon = "📝",
            AccentColor = "#10B981",
            DescriptionHeader = "Complete JSON Schema Reference for Custom Question Packs",
            DescriptionContent = "• Custom Question Pack JSON File Structure:\nSave as UTF-8 `.json` file inside `TriviaData/packs/` (e.g. `disney_trivia.json`):\n\n{\n  \"PackId\": \"disney-trivia\",\n  \"Title\": \"Disney Animation Classics\",\n  \"Category\": \"Disney Movies\",\n  \"Description\": \"Family-friendly questions spanning animated classics and Pixar films.\",\n  \"Questions\": [\n    {\n      \"Id\": \"DIS-001\",\n      \"Category\": \"Disney Movies\",\n      \"Difficulty\": \"Easy\",\n      \"QuestionType\": \"MultipleChoice\",\n      \"Prompt\": \"What is the name of Simba's father in The Lion King?\",\n      \"Options\": [\n        \"Scar\",\n        \"Mufasa\",\n        \"Rafiki\",\n        \"Pumbaa\"\n      ],\n      \"CorrectAnswerIndex\": 1,\n      \"Explanation\": \"Mufasa was voiced by James Earl Jones in the 1994 classic.\",\n      \"TimeLimitSeconds\": 15\n    }\n  ]\n}\n\n• Key Field Definitions:\n  - PackId: Unique identifier slug (e.g. 'rock-and-roll', 'movie-soundtracks').\n  - Title: Display name shown in Game Master category dropdowns.\n  - Difficulty: 'Easy', 'Medium', or 'Hard'.\n  - QuestionType: 'MultipleChoice' (standard 4-choice buzzer).\n  - Prompt: The question text displayed on TVs and mobile phones.\n  - Options: Array of exactly 4 strings for choices A, B, C, D.\n  - CorrectAnswerIndex: Zero-based integer (0=A, 1=B, 2=C, 3=D).\n  - Explanation: Brief educational snippet shown during answer reveal.\n  - TimeLimitSeconds: Factory fallback timer (15s); Game Master Settings take precedence during live games."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "⚡ 9. Textbox Auto-Selection & Fast Input",
            Icon = "⚡",
            AccentColor = "#38BDF8",
            DescriptionHeader = "Instant Highlighting on Focus Across All Application Inputs",
            DescriptionContent = "• Global Select-All on Focus: Clicking or tabbing into any text input box, Wi-Fi password field, or numeric setting automatically selects and highlights all text.\n\n• Instant Overwrite Typing: Game Masters can immediately enter new venue names, countdown durations, or point penalties without manually deleting or double-clicking first.\n\n• Precision Editing: Subsequent clicks within an already focused field preserve standard cursor placement for pinpoint editing."
        });

        SelectedHelpTopic = HelpTopics.FirstOrDefault();
    }

    public void Dispose()
    {
        try { _preGameTimer.Stop(); } catch { }
        try { _preGameTimer.Dispose(); } catch { }
        _webServer.Dispose();
        _engine.Dispose();
        _dbService.Dispose();
        GC.SuppressFinalize(this);
    }
}
