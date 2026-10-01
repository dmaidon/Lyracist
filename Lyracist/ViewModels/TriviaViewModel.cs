// Edited on Oct 1, 2026 @ 08:50:00 -> Fix TV display VM leak, rebind engine on ResetGame, debounce settings, and update roster in-place
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Services.Display;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;
using Lyracist.Windows;
using QRCoder;
using Application = System.Windows.Application;

namespace Lyracist.ViewModels;

public partial class TriviaViewModel : BaseViewModel, IDisposable
{
    private readonly IDisplayService _displayService;
    private readonly TriviaDatabaseService _dbService;
    private TriviaGameEngine _engine;
    private TriviaWebServer? _webServer;
    private TriviaDisplayWindow? _displayWindow;
    private TriviaDisplayViewModel? _displayVm;
    private readonly DispatcherTimer _preGameTimer = new();
    private readonly System.Timers.Timer _saveSettingsDebounceTimer = new(500) { AutoReset = false };
    private bool _isIntermissionAutoRestart;

    [ObservableProperty]
    private TriviaSettings _settings;

    /// Live summary of the auto-run timing (shown under the "Auto-Run Game" toggle) so the
    /// header always reflects the game master's actual configured timings instead of a
    /// hardcoded "15s answer • 5s fade • 5s reveal" that goes stale the moment they're changed
    /// (e.g. from the Lyracist.Trivia app, since all three apps share the same settings file).
    public string AutoRunTimingSummary =>
        $"{Settings.DefaultQuestionSeconds}s answer • {Settings.AnswerEliminationIntervalSeconds}s fade • {Settings.PostRevealDelaySeconds}s reveal";

    partial void OnSettingsChanged(TriviaSettings value) => OnPropertyChanged(nameof(AutoRunTimingSummary));

    [ObservableProperty]
    private string _venueName = "Main Venue";

    [ObservableProperty]
    private string _hostName = "Trivia Master";

    [ObservableProperty]
    private string _instructionBannerText = TriviaSettings.DefaultInstructionBannerText;

    [ObservableProperty]
    private string _gameTitle = "Trivia Night Pro";

    [ObservableProperty]
    private string _activeRoundTitle = "Round 1";

    [ObservableProperty]
    private int _currentQuestionNumber = 1;

    partial void OnCurrentQuestionNumberChanged(int value)
    {
        if (_engine?.CurrentSession?.CurrentRound != null &&
            _engine.State != TriviaGameState.Lobby &&
            _engine.State != TriviaGameState.GameComplete)
        {
            int targetIdx = value - 1;
            if (targetIdx >= 0 && targetIdx < _engine.CurrentSession.CurrentRound.Questions.Count)
            {
                if (_engine.CurrentSession.CurrentQuestionIndex != targetIdx)
                {
                    _engine.GoToQuestion(targetIdx, startTimerImmediately: AutoAdvanceQuestions);
                }
            }
        }
    }

    [ObservableProperty]
    private int _totalQuestionsInRound = 1;

    public ObservableCollection<int> AvailableQuestionNumbers { get; } = [];

    private void UpdateAvailableQuestionNumbers(int count)
    {
        AvailableQuestionNumbers.Clear();
        for (int i = 1; i <= count; i++)
        {
            AvailableQuestionNumbers.Add(i);
        }
    }

    [ObservableProperty]
    private string _currentQuestionPrompt = "No question active. Load a pack and click 'Start Game'.";

    [ObservableProperty]
    private string _optionA = "";

    [ObservableProperty]
    private string _optionB = "";

    [ObservableProperty]
    private string _optionC = "";

    [ObservableProperty]
    private string _optionD = "";

    [ObservableProperty]
    private int _correctAnswerIndex = -1;

    [ObservableProperty]
    private int _remainingSeconds = 15;

    [ObservableProperty]
    private int _totalCountdownSeconds = 15;

    [ObservableProperty]
    private string _gameStateText = "Lobby";

    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _pauseReason = "";

    [ObservableProperty]
    private string _pauseButtonText = "⏸ Pause";

    [ObservableProperty]
    private bool _autoAdvanceQuestions = true;

    [ObservableProperty]
    private int _questionsPerGame = 10;

    [ObservableProperty]
    private int _connectedPlayerCount;

    [ObservableProperty]
    private string _serverStatusText = "Server Offline";

    [ObservableProperty]
    private string _patronUrl = "http://localhost:8085/trivia";

    [ObservableProperty]
    private BitmapSource? _qrCodeImage;

    [ObservableProperty]
    private int _preGameCountdownMinutes = 5;

    [ObservableProperty]
    private int _preGameSecondsRemaining = 300;

    [ObservableProperty]
    private bool _isPreGameCountdownRunning;

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

    [ObservableProperty]
    private bool _isDisplayOpen;

    public int[] QuestionsPerGamePresets { get; } = [5, 10, 15, 20, 25, 50, 100];
    public ObservableCollection<MonitorInfo> AvailableMonitors { get; } = [];
    public ObservableCollection<SelectableTriviaPack> SelectablePacks { get; } = [];
    public ObservableCollection<TriviaPlayer> Players { get; } = [];
    public ObservableCollection<AnswerDistributionItem> AnswerDistribution { get; } = [];

    public TriviaGameEngine Engine => _engine;

    public TriviaViewModel(IDisplayService displayService)
    {
        _displayService = displayService;
        _settings = TriviaStorageHelper.LoadSettings();
        _engine = new TriviaGameEngine(_settings);
        _dbService = new TriviaDatabaseService();

        _venueName = !string.IsNullOrWhiteSpace(AppSettings.SelectedVenue) ? AppSettings.SelectedVenue : _settings.VenueName;
        _hostName = !string.IsNullOrWhiteSpace(AppSettings.DjName) ? AppSettings.DjName : _settings.HostName;
        _instructionBannerText = string.IsNullOrWhiteSpace(_settings.InstructionBannerText) ? TriviaSettings.DefaultInstructionBannerText : _settings.InstructionBannerText;
        _autoAdvanceQuestions = _settings.AutoAdvanceQuestions;
        _questionsPerGame = _settings.QuestionsPerGame > 0 ? _settings.QuestionsPerGame : 10;
        _preGameCountdownMinutes = _settings.PreGameCountdownMinutes > 0 ? _settings.PreGameCountdownMinutes : 5;
        _autoStartAfterCountdown = _settings.AutoStartAfterCountdown;
        _preGameSecondsRemaining = _preGameCountdownMinutes * 60;
        _preGameCountdownText = $"{_preGameCountdownMinutes:D2}:00";

        _displayService.SetTriviaGameEngine(_engine);

        _preGameTimer.Interval = TimeSpan.FromSeconds(1);
        _preGameTimer.Tick += OnPreGameTimerTick;

        _saveSettingsDebounceTimer.Elapsed += (_, _) => TriviaStorageHelper.SaveSettings(Settings);

        WireEngineEvents();
        LoadPacks();
        RefreshMonitors();
        StartWebServer();
    }

    private void DebounceSaveSettings()
    {
        _saveSettingsDebounceTimer.Stop();
        _saveSettingsDebounceTimer.Start();
    }

    public void RefreshMonitors()
    {
        AvailableMonitors.Clear();
        var monitors = MonitorEnumerator.GetMonitors();
        foreach (var m in monitors)
        {
            AvailableMonitors.Add(m);
        }

        if (AvailableMonitors.Count > 0)
        {
            var match = AvailableMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, Settings.SelectedMonitorDevice, StringComparison.OrdinalIgnoreCase));
            SelectedMonitor = match ?? (AvailableMonitors.Count > 1 ? AvailableMonitors[1] : AvailableMonitors[0]);
        }
    }

    partial void OnSelectedMonitorChanged(MonitorInfo? value)
    {
        if (value != null)
        {
            Settings.SelectedMonitorDevice = value.DeviceName;
            DebounceSaveSettings();
            PositionDisplayWindow(value.DeviceName);
        }
    }

    partial void OnAutoAdvanceQuestionsChanged(bool value)
    {
        Settings.AutoAdvanceQuestions = value;
        _engine.Settings.AutoAdvanceQuestions = value;
        DebounceSaveSettings();
    }

    partial void OnQuestionsPerGameChanged(int value)
    {
        Settings.QuestionsPerGame = value;
        _engine.Settings.QuestionsPerGame = value;
        DebounceSaveSettings();
        UpdateSelectedPacksPreview();
    }

    private void WireEngineEvents()
    {
        _engine.StateChanged += (s, state) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                GameStateText = state.ToString();
                IsGameRunning = state != TriviaGameState.Lobby && state != TriviaGameState.GameComplete;
            });
        };

        _engine.TimerTick += (s, remaining) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RemainingSeconds = remaining;
                TotalCountdownSeconds = _engine.TotalCountdownSeconds;
            });
        };

        _engine.QuestionStarted += (s, q) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentQuestionPrompt = q.Prompt;
                OptionA = q.Options.Count > 0 ? q.Options[0] : "";
                OptionB = q.Options.Count > 1 ? q.Options[1] : "";
                OptionC = q.Options.Count > 2 ? q.Options[2] : "";
                OptionD = q.Options.Count > 3 ? q.Options[3] : "";
                CorrectAnswerIndex = -1;
                TotalCountdownSeconds = _engine.TotalCountdownSeconds;
                RemainingSeconds = TotalCountdownSeconds;
                AnswerDistribution.Clear();
                if (_engine.CurrentSession?.CurrentRound != null)
                {
                    CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
                }
            });
        };

        _engine.AnswerRevealed += (s, q) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CorrectAnswerIndex = q.CorrectAnswerIndex;

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
            });
        };

        _engine.LeaderboardUpdated += (s, playersList) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() => RefreshPlayers(playersList));
        };

        _engine.GamePaused += (s, reason) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsPaused = true;
                PauseReason = reason ?? "Paused";
                PauseButtonText = "▶ Resume";
            });
        };

        _engine.GameResumed += (s, e) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsPaused = false;
                PauseReason = "";
                PauseButtonText = "⏸ Pause";
            });
        };

        _engine.IntermissionTick += (s, remaining) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsIntermissionActive = remaining > 0;
                IntermissionSecondsRemaining = remaining;
                int mins = remaining / 60;
                int secs = remaining % 60;
                IntermissionCountdownText = $"{mins:D2}:{secs:D2}";
            });
        };

        _engine.IntermissionCompleted += (s, e) =>
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsIntermissionActive = false;
                IntermissionSecondsRemaining = 0;
                _isIntermissionAutoRestart = true;
                try
                {
                    StartGame();
                }
                finally
                {
                    _isIntermissionAutoRestart = false;
                }
            });
        };
    }

    public List<TriviaQuestionPack> GetCheckedPacks()
    {
        var checkedPacks = SelectablePacks.Where(sp => sp.IsChecked).Select(sp => sp.Pack).ToList();
        if (checkedPacks.Count > 0) return checkedPacks;
        return SelectablePacks.Count > 0 ? [SelectablePacks[0].Pack] : [];
    }

    private void UpdateSelectedPacksPreview()
    {
        var checkedPacks = GetCheckedPacks();
        if (checkedPacks.Count == 0) return;

        int questionCount = Math.Clamp(QuestionsPerGame > 0 ? QuestionsPerGame : 10, 1, checkedPacks.Sum(p => p.Questions.Count));
        _displayVm?.UpdateFeaturedCategoryHeader(questionCount, checkedPacks.Count);

        if (checkedPacks.Count == 1)
        {
            ActiveRoundTitle = checkedPacks[0].Title;
            _displayVm?.UpdateCategory(checkedPacks[0].Category, checkedPacks[0].Description);
        }
        else
        {
            ActiveRoundTitle = string.Join(" + ", checkedPacks.Select(p => p.Title));
            _displayVm?.UpdateMixedCategory(checkedPacks);
        }
    }

    public void ApplySettings(TriviaSettings newSettings)
    {
        Settings = newSettings;
        _engine.Settings = newSettings;
        AutoAdvanceQuestions = newSettings.AutoAdvanceQuestions;
        QuestionsPerGame = newSettings.QuestionsPerGame > 0 ? newSettings.QuestionsPerGame : 10;
        PreGameCountdownMinutes = newSettings.PreGameCountdownMinutes > 0 ? newSettings.PreGameCountdownMinutes : 5;
        AutoStartAfterCountdown = newSettings.AutoStartAfterCountdown;

        if (_displayVm != null)
        {
            _displayVm.UpdateInstructionBannerTemplate(newSettings.InstructionBannerText);
            _displayVm.UpdateWifiCredentials(newSettings.WifiSsid, newSettings.WifiPassword);
        }
    }

    private void LoadPacks()
    {
        SelectablePacks.Clear();
        var packs = TriviaPackManager.LoadAllPacks();
        bool isFirst = true;
        foreach (var p in packs)
        {
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

        // Asynchronously seed SQLite database in background so UI thread startup remains instantaneous
        Task.Run(() =>
        {
            try
            {
                foreach (var p in packs)
                {
                    _dbService.SeedPackIntoDatabase(p);
                }
            }
            catch (Exception ex)
            {
                Globals.LogError("Trivia", "SeedPackIntoDatabase", ex);
            }
        });

        if (SelectablePacks.Count == 0)
        {
            Globals.LogError("Lyracist",
                $"No trivia question packs found in '{TriviaPackManager.GetDefaultPacksDirectory()}'.",
                "LoadPacks");
        }

        UpdateSelectedPacksPreview();
    }

    private void StartWebServer()
    {
        try
        {
            string ip = LocalNetworkHelper.GetLocalIPv4Address()?.ToString() ?? "127.0.0.1";
            _webServer = new TriviaWebServer(_engine, Settings.Port);
            _webServer.Start();
            if (!_webServer.IsRunning)
            {
                ServerStatusText = $"Server Error: could not bind port {Settings.Port} (already in use?)";
                return;
            }
            PatronUrl = $"http://{ip}:{Settings.Port}/trivia";
            ServerStatusText = $"Online: {PatronUrl}";
            GenerateQrCode(PatronUrl);
        }
        catch (Exception ex)
        {
            ServerStatusText = $"Server Error: {ex.Message}";
        }
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

    private void OnPreGameTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (IsPreGameCountdownRunning && PreGameSecondsRemaining > 0)
            {
                PreGameSecondsRemaining--;
                int mins = PreGameSecondsRemaining / 60;
                int secs = PreGameSecondsRemaining % 60;
                PreGameCountdownText = $"{mins:D2}:{secs:D2}";
                _displayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);

                if (PreGameSecondsRemaining == 0)
                {
                    _preGameTimer.Stop();
                    IsPreGameCountdownRunning = false;
                    if (AutoStartAfterCountdown)
                    {
                        StartGame();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "TriviaViewModel.OnPreGameTimerTick", ex);
        }
    }

    [RelayCommand]
    public void LaunchPreGameLobby()
    {
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

        OpenTriviaDisplay();
        _displayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
        UpdateSelectedPacksPreview();
    }

    [RelayCommand]
    public void StartGame()
    {
        try
        {
            var checkedPacks = GetCheckedPacks();
            if (checkedPacks.Count == 0) return;

            // Cancel pre-game countdown and dismiss lobby screen
            _preGameTimer.Stop();
            IsPreGameCountdownRunning = false;
            IsShowingConnectScreen = false;
            if (_displayVm != null)
            {
                _displayVm.IsConnectInstructionsActive = false;
                _displayVm.IsPreGameCountdownRunning = false;
            }

            var gameQuestions = TriviaPackManager.BuildMixedQuestionSet(checkedPacks, QuestionsPerGame);
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

            _engine.StartGame([round], title, isAutoRestart: _isIntermissionAutoRestart);
            ActiveRoundTitle = round.Title;
            TotalQuestionsInRound = gameQuestions.Count;
            UpdateAvailableQuestionNumbers(TotalQuestionsInRound);
            CurrentQuestionNumber = 1;

            // Start 1st question (immediate timer if Auto-Run, or reading/standby state if manual)
            _engine.PrepareCurrentQuestion(startTimerImmediately: AutoAdvanceQuestions);

            OpenTriviaDisplay();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error starting game: {ex.Message}", "Lyracist Trivia", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ToggleTriviaDisplay()
    {
        if (_displayWindow != null && _displayWindow.IsLoaded)
        {
            CloseTriviaDisplay();
        }
        else
        {
            OpenTriviaDisplay();
        }
    }

    [RelayCommand]
    public void OpenTriviaDisplay()
    {
        if (_displayWindow == null || !_displayWindow.IsLoaded)
        {
            _displayVm = new TriviaDisplayViewModel(
                _engine,
                VenueName,
                PatronUrl,
                QrCodeImage,
                Settings.WifiSsid,
                Settings.WifiPassword,
                PreGameSecondsRemaining);

            _displayVm.HostName = HostName;
            _displayVm.UpdateInstructionBannerTemplate(InstructionBannerText);
            _displayVm.IsConnectInstructionsActive = (_engine.State == TriviaGameState.Lobby) && IsShowingConnectScreen;

            UpdateSelectedPacksPreview();
            _displayVm.SyncWithEngine();

            _displayWindow = new TriviaDisplayWindow
            {
                DataContext = _displayVm
            };

            _displayWindow.Closed += (s, e) =>
            {
                _displayVm?.Dispose();
                _displayWindow = null;
                _displayVm = null;
                IsDisplayOpen = false;
            };

            PositionDisplayWindow(SelectedMonitor?.DeviceName);
            _displayWindow.Show();
            PositionDisplayWindow(SelectedMonitor?.DeviceName);
            IsDisplayOpen = true;
        }
        else
        {
            _displayVm?.SyncWithEngine();
            PositionDisplayWindow(SelectedMonitor?.DeviceName);
            _displayWindow.Activate();
        }
    }

    [RelayCommand]
    public void CloseTriviaDisplay()
    {
        if (_displayWindow != null && _displayWindow.IsLoaded)
        {
            _displayWindow.Close();
            _displayWindow = null;
            _displayVm = null;
            IsDisplayOpen = false;
        }
    }

    public void PositionDisplayWindow(string? deviceName)
    {
        if (_displayWindow == null || !_displayWindow.IsLoaded) return;

        var screens = System.Windows.Forms.Screen.AllScreens;
        var targetScreen = WindowPositioner.ResolveByDeviceName(screens, deviceName);
        if (targetScreen != null)
        {
            _displayWindow.WindowState = WindowState.Normal;
            _displayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            WindowPositioner.FillArea(_displayWindow, targetScreen.Bounds);
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
        _displayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
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
        _displayVm?.UpdatePreGameCountdown(PreGameSecondsRemaining);
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
    private void ToggleConnectInstructions()
    {
        IsShowingConnectScreen = !IsShowingConnectScreen;
        if (_displayVm != null)
        {
            _displayVm.IsConnectInstructionsActive = IsShowingConnectScreen;
        }
    }

    [RelayCommand]
    private void RemovePlayer(TriviaPlayer? player)
    {
        if (player != null)
        {
            _engine.KickPlayer(player.Name); // kick, so the phone can't re-register
            Players.Remove(player);
        }
    }

    [RelayCommand]
    private void StartQuestion()
    {
        _engine.StartCurrentQuestion();
    }

    [RelayCommand]
    private void PreviousQuestion()
    {
        bool hasPrev = _engine.PreviousQuestion(startTimerImmediately: AutoAdvanceQuestions);
        if (hasPrev && _engine.CurrentSession?.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
        }
    }

    [RelayCommand]
    private void NextQuestion()
    {
        bool hasNext = _engine.AdvanceToNextQuestion(startTimerImmediately: AutoAdvanceQuestions);
        if (hasNext && _engine.CurrentSession?.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
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
    private void ResetQuestionTimer()
    {
        _engine.ResetQuestionTimer();
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
        if (_engine.CurrentSession?.CurrentQuestion != null)
        {
            CurrentQuestionNumber = _engine.CurrentSession.CurrentQuestionIndex + 1;
        }
    }

    [RelayCommand]
    private void TogglePause()
    {
        _engine.TogglePause("Host Manual Pause");
    }

    [RelayCommand]
    private void LockAndReveal()
    {
        _engine.LockAndRevealAnswer();
    }

    [RelayCommand]
    private void ResetGame()
    {
        _webServer?.Dispose();
        _engine.Dispose();
        _engine = new TriviaGameEngine(Settings);
        _displayService.SetTriviaGameEngine(_engine);
        WireEngineEvents();
        _displayVm?.RebindEngine(_engine);
        StartWebServer();
        Players.Clear();
        GameStateText = "Lobby";
        IsGameRunning = false;
        CurrentQuestionPrompt = "Game reset. Click 'Start Game' to begin.";
        OptionA = OptionB = OptionC = OptionD = "";
        CorrectAnswerIndex = -1;
        CurrentQuestionNumber = 1;
        TotalQuestionsInRound = 1;
        AvailableQuestionNumbers.Clear();
        AnswerDistribution.Clear();
    }

    private void RefreshPlayers(List<TriviaPlayer> playerList)
    {
        var targetIds = new HashSet<string>(playerList.Select(p => p.PlayerId));

        for (int i = Players.Count - 1; i >= 0; i--)
        {
            if (!targetIds.Contains(Players[i].PlayerId))
            {
                Players.RemoveAt(i);
            }
        }

        for (int i = 0; i < playerList.Count; i++)
        {
            var p = playerList[i];
            int currentIdx = Players.IndexOf(p);
            if (currentIdx < 0)
            {
                Players.Insert(i, p);
            }
            else if (currentIdx != i)
            {
                Players.Move(currentIdx, i);
            }
        }
        ConnectedPlayerCount = Players.Count(p => p.IsConnected);
    }

    [RelayCommand]
    private void RefreshPacks()
    {
        LoadPacks();
    }

    public void Dispose()
    {
        _preGameTimer.Stop();
        // Flush a settings change still waiting out its debounce so closing right after an edit keeps it.
        bool savePending = _saveSettingsDebounceTimer.Enabled;
        _saveSettingsDebounceTimer.Dispose();
        if (savePending) TriviaStorageHelper.SaveSettings(Settings);
        CloseTriviaDisplay();
        _displayVm?.Dispose();
        _webServer?.Dispose();
        _engine.Dispose();
        _dbService.Dispose();
        GC.SuppressFinalize(this);
    }
}
