// Edited on Aug 19, 2026 @ 11:15:30 -> Respect GameMaster TriviaSettings.DefaultQuestionSeconds over question JSON default
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        private TriviaGameEngine? _triviaEngine;
        private TriviaWebServer? _triviaWebServer;
        private DispatcherTimer? _triviaSettingsStatusTimer;
#if !MAUI
        private KSRotation.Windows.TriviaDisplayWindow? _triviaDisplayWindow;
        private TriviaDisplayViewModel? _triviaDisplayVm;
#endif

        [ObservableProperty]
        public partial bool IsTriviaDisplayOpen { get; set; }

        [ObservableProperty]
        public partial TriviaSettings TriviaSettings { get; set; } = new();

        [ObservableProperty]
        public partial string SelectedTriviaPack { get; set; } = "";

        partial void OnSelectedTriviaPackChanged(string value)
        {
#if !MAUI
            _triviaDisplayVm?.UpdateCategory(value);
#endif
        }

        [ObservableProperty]
        public partial string TriviaGameStateText { get; set; } = "Lobby";

        [ObservableProperty]
        public partial int TriviaRemainingSeconds { get; set; }

        [ObservableProperty]
        public partial int TriviaTotalSeconds { get; set; } = 15;

        [ObservableProperty]
        public partial bool IsTriviaGameRunning { get; set; }

        [ObservableProperty]
        public partial bool IsTriviaPaused { get; set; }

        [ObservableProperty]
        public partial string TriviaPauseReason { get; set; } = "";

        [ObservableProperty]
        public partial string TriviaPauseButtonText { get; set; } = "⏸ Pause";

        [ObservableProperty]
        public partial string TriviaActiveRoundTitle { get; set; } = "Round 1";

        [ObservableProperty]
        public partial string TriviaCurrentQuestionPrompt { get; set; } = "No question active. Load a pack and click 'Start Game'.";

        [ObservableProperty]
        public partial string TriviaOptionA { get; set; } = "";

        [ObservableProperty]
        public partial string TriviaOptionB { get; set; } = "";

        [ObservableProperty]
        public partial string TriviaOptionC { get; set; } = "";

        [ObservableProperty]
        public partial string TriviaOptionD { get; set; } = "";

        [ObservableProperty]
        public partial int TriviaCorrectAnswerIndex { get; set; } = -1;

        [ObservableProperty]
        public partial string TriviaServerStatusText { get; set; } = "Server Offline";

        [ObservableProperty]
        public partial string TriviaPatronUrl { get; set; } = "http://localhost:8085/trivia";

        // Separate Trivia Settings Properties (Venue and Host pulled directly from active app settings)
        public string TriviaVenueName => !string.IsNullOrWhiteSpace(VenueName) ? VenueName : "Main Venue";

        public string TriviaHostName => !string.IsNullOrWhiteSpace(DjName) ? DjName : "DJ / Host";

        [ObservableProperty]
        public partial int TriviaDefaultQuestionSeconds { get; set; } = 15;

        [ObservableProperty]
        public partial int TriviaWarningCountdownSeconds { get; set; } = 3;

        [ObservableProperty]
        public partial int TriviaAnswerEliminationIntervalSeconds { get; set; } = 5;

        [ObservableProperty]
        public partial int TriviaBasePoints { get; set; } = 1000;

        [ObservableProperty]
        public partial int TriviaWrongAnswerDeduction { get; set; } = 0;

        [ObservableProperty]
        public partial bool TriviaSpeedBonusEnabled { get; set; } = true;

        [ObservableProperty]
        public partial int TriviaMaxSpeedBonus { get; set; } = 500;

        [ObservableProperty]
        public partial bool TriviaStreakBonusEnabled { get; set; } = true;

        [ObservableProperty]
        public partial double TriviaStreakMultiplier { get; set; } = 0.1;

        [ObservableProperty]
        public partial bool TriviaAutoAdvance { get; set; } = true;

        [ObservableProperty]
        public partial bool TriviaAutoStartNextGame { get; set; } = true;

        [ObservableProperty]
        public partial int TriviaNextGameDelayMinutes { get; set; } = 3;

        [ObservableProperty]
        public partial int TriviaPreGameCountdownMinutes { get; set; } = 5;

        [ObservableProperty]
        public partial bool TriviaAutoStartAfterCountdown { get; set; } = true;

        [ObservableProperty]
        public partial int TriviaPort { get; set; } = 8085;

        [ObservableProperty]
        public partial string TriviaWifiSsid { get; set; } = "";

        [ObservableProperty]
        public partial string TriviaWifiPassword { get; set; } = "";

        [ObservableProperty]
        public partial string TriviaSettingsStatusMessage { get; set; } = "";

        [ObservableProperty]
        public partial bool IsTriviaSettingsStatusVisible { get; set; }

        public ObservableCollection<string> TriviaAvailablePacks { get; } = [];
        public ObservableCollection<TriviaPlayer> TriviaPlayers { get; } = [];

        public void InitializeTrivia()
        {
            try
            {
                TriviaSettings = TriviaStorageHelper.LoadSettings();
                LoadTriviaSettingsIntoProperties();

                _triviaEngine = new TriviaGameEngine(TriviaSettings);
                WireTriviaEngineEvents();
                LoadTriviaPacks();
                StartTriviaWebServer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing trivia: {ex.Message}");
            }
        }

        private void WireTriviaEngineEvents()
        {
            if (_triviaEngine == null) return;

            _triviaEngine.StateChanged += (s, state) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaGameStateText = state.ToString();
                    IsTriviaGameRunning = state != TriviaGameState.Lobby && state != TriviaGameState.GameComplete;
                });
            };

            _triviaEngine.TimerTick += (s, remaining) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaRemainingSeconds = remaining;
                });
            };

            _triviaEngine.QuestionStarted += (s, q) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaCurrentQuestionPrompt = q.Prompt;
                    TriviaOptionA = q.Options.Count > 0 ? q.Options[0] : "";
                    TriviaOptionB = q.Options.Count > 1 ? q.Options[1] : "";
                    TriviaOptionC = q.Options.Count > 2 ? q.Options[2] : "";
                    TriviaOptionD = q.Options.Count > 3 ? q.Options[3] : "";
                    TriviaCorrectAnswerIndex = -1;
                    TriviaTotalSeconds = TriviaSettings.DefaultQuestionSeconds > 0 ? TriviaSettings.DefaultQuestionSeconds : (q.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : 15);
                    TriviaRemainingSeconds = TriviaTotalSeconds;
                });
            };

            _triviaEngine.AnswerRevealed += (s, q) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaCorrectAnswerIndex = q.CorrectAnswerIndex;
                });
            };

            _triviaEngine.LeaderboardUpdated += (s, playersList) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaPlayers.Clear();
                    foreach (var p in playersList)
                    {
                        TriviaPlayers.Add(p);
                    }
                });
            };

            _triviaEngine.GamePaused += (s, reason) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    IsTriviaPaused = true;
                    TriviaPauseReason = reason ?? "Paused";
                    TriviaPauseButtonText = "▶ Resume";
                });
            };

            _triviaEngine.GameResumed += (s, e) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    IsTriviaPaused = false;
                    TriviaPauseReason = "";
                    TriviaPauseButtonText = "⏸ Pause";
                });
            };

            _triviaEngine.IntermissionCompleted += (s, e) =>
            {
                // The engine only reaches here when AutoStartNextGameEnabled was on, so it is safe
                // to unconditionally start the next round - without this, "Auto-Start Next Game"
                // silently does nothing and the show stops after one game.
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    AdvanceToNextTriviaPack();
                    StartTrivia();
                });
            };
        }

        private void AdvanceToNextTriviaPack()
        {
            if (TriviaAvailablePacks.Count > 1 && !string.IsNullOrWhiteSpace(SelectedTriviaPack))
            {
                int currentIndex = TriviaAvailablePacks.IndexOf(SelectedTriviaPack);
                int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % TriviaAvailablePacks.Count : 0;
                SelectedTriviaPack = TriviaAvailablePacks[nextIndex];
            }
        }

        private void LoadTriviaPacks()
        {
            TriviaAvailablePacks.Clear();
            var packs = TriviaPackManager.LoadAllPacks();
            foreach (var p in packs)
            {
                TriviaAvailablePacks.Add(p.Title);
            }

            if (TriviaAvailablePacks.Count > 0)
            {
                SelectedTriviaPack = TriviaAvailablePacks[0];
            }
            else
            {
                // On a machine where the resolved TriviaData directory doesn't hold the expected
                // packs (e.g. a fresh install away from the dev box), the host would otherwise
                // just see an empty category list with no clue why. Leave a breadcrumb in the log.
                Lyracist.Shared.Globals.LogError("KSRotation",
                    $"No trivia question packs found in '{TriviaPackManager.GetDefaultPacksDirectory()}'. Category list will be empty until a .json pack is placed there.",
                    "LoadTriviaPacks");
            }
        }

        private void StartTriviaWebServer()
        {
            if (_triviaEngine == null) return;
            try
            {
                string ip = LocalNetworkHelper.GetLocalIPv4Address()?.ToString() ?? "127.0.0.1";
                _triviaWebServer = new TriviaWebServer(_triviaEngine, TriviaSettings.Port);
                _triviaWebServer.Start();
                TriviaPatronUrl = $"http://{ip}:{TriviaSettings.Port}/trivia";
                TriviaServerStatusText = $"Online: {TriviaPatronUrl}";
            }
            catch (Exception ex)
            {
                TriviaServerStatusText = $"Server Error: {ex.Message}";
            }
        }

        public void CheckAndSyncTriviaPause()
        {
            if (_triviaEngine == null) return;

            bool isScreenOccupied = IsDisplayEnabled || IsDjBannerEnabled;
            if (isScreenOccupied)
            {
                string reason = IsDjBannerEnabled ? "DJ Banner Display" : "Rotation Screen Active";
                _triviaEngine.PauseGame(reason);
            }
            else
            {
                _triviaEngine.ResumeGame();
            }
        }

        private void LoadTriviaSettingsIntoProperties()
        {
            OnPropertyChanged(nameof(TriviaVenueName));
            OnPropertyChanged(nameof(TriviaHostName));
            TriviaDefaultQuestionSeconds = TriviaSettings.DefaultQuestionSeconds;
            TriviaWarningCountdownSeconds = TriviaSettings.WarningCountdownSeconds;
            TriviaAnswerEliminationIntervalSeconds = TriviaSettings.AnswerEliminationIntervalSeconds;
            TriviaBasePoints = TriviaSettings.BasePointsPerQuestion;
            TriviaWrongAnswerDeduction = TriviaSettings.WrongAnswerDeductionPoints;
            TriviaSpeedBonusEnabled = TriviaSettings.SpeedBonusEnabled;
            TriviaMaxSpeedBonus = TriviaSettings.MaxSpeedBonus;
            TriviaStreakBonusEnabled = TriviaSettings.StreakBonusMultiplier > 0;
            TriviaStreakMultiplier = TriviaSettings.StreakBonusMultiplier;
            TriviaAutoAdvance = TriviaSettings.AutoAdvanceQuestions;
            TriviaAutoStartNextGame = TriviaSettings.AutoStartNextGameEnabled;
            TriviaNextGameDelayMinutes = TriviaSettings.NextGameDelayMinutes;
            TriviaPreGameCountdownMinutes = TriviaSettings.PreGameCountdownMinutes;
            TriviaAutoStartAfterCountdown = TriviaSettings.AutoStartAfterCountdown;
            TriviaPort = TriviaSettings.Port;
            TriviaWifiSsid = TriviaSettings.WifiSsid;
            TriviaWifiPassword = TriviaSettings.WifiPassword;
        }

        private void PushTriviaPropertiesIntoSettings()
        {
            TriviaSettings.VenueName = TriviaVenueName;
            TriviaSettings.HostName = TriviaHostName;
            TriviaSettings.DefaultQuestionSeconds = TriviaDefaultQuestionSeconds;
            TriviaSettings.WarningCountdownSeconds = TriviaWarningCountdownSeconds;
            TriviaSettings.AnswerEliminationIntervalSeconds = TriviaAnswerEliminationIntervalSeconds;
            TriviaSettings.BasePointsPerQuestion = TriviaBasePoints;
            TriviaSettings.WrongAnswerDeductionPoints = TriviaWrongAnswerDeduction;
            TriviaSettings.SpeedBonusEnabled = TriviaSpeedBonusEnabled;
            TriviaSettings.MaxSpeedBonus = TriviaMaxSpeedBonus;
            TriviaSettings.StreakBonusMultiplier = TriviaStreakBonusEnabled ? TriviaStreakMultiplier : 0;
            TriviaSettings.AutoAdvanceQuestions = TriviaAutoAdvance;
            TriviaSettings.AutoStartNextGameEnabled = TriviaAutoStartNextGame;
            TriviaSettings.NextGameDelayMinutes = TriviaNextGameDelayMinutes;
            TriviaSettings.PreGameCountdownMinutes = TriviaPreGameCountdownMinutes;
            TriviaSettings.AutoStartAfterCountdown = TriviaAutoStartAfterCountdown;
            TriviaSettings.Port = TriviaPort;
            TriviaSettings.WifiSsid = TriviaWifiSsid;
            TriviaSettings.WifiPassword = TriviaWifiPassword;
        }

        [RelayCommand]
        public void StartTrivia()
        {
            if (_triviaEngine == null) return;
            try
            {
                List<TriviaRound> rounds = [];
                var allPacks = TriviaPackManager.LoadAllPacks();
                var matchedPack = allPacks.FirstOrDefault(p => p.Title.Equals(SelectedTriviaPack, StringComparison.OrdinalIgnoreCase));
                if (matchedPack != null && matchedPack.Questions.Count > 0)
                {
                    // Shuffle and cap to QuestionsPerGame so every session is a fresh randomized
                    // order of a bounded length, matching the standalone Lyracist.Trivia app -
                    // without this, every game replayed all ~150 questions in the same fixed order.
                    var shuffled = matchedPack.Questions.OrderBy(_ => Random.Shared.Next()).ToList();
                    int qCount = Math.Clamp(TriviaSettings.QuestionsPerGame > 0 ? TriviaSettings.QuestionsPerGame : 10, 1, shuffled.Count);
                    rounds = [new TriviaRound
                    {
                        RoundNumber = 1,
                        Title = matchedPack.Title,
                        Questions = shuffled.Take(qCount).ToList()
                    }];
                }

                if (rounds.Count == 0)
                {
                    rounds = [new TriviaRound
                    {
                        RoundNumber = 1,
                        Title = "General Knowledge",
                        Questions = [
                            new TriviaQuestion { Id = "Q1", Prompt = "What is the capital of France?", Options = ["London", "Paris", "Rome", "Berlin"], CorrectAnswerIndex = 1, TimeLimitSeconds = 15 },
                            new TriviaQuestion { Id = "Q2", Prompt = "Which planet is known as the Red Planet?", Options = ["Venus", "Mars", "Jupiter", "Saturn"], CorrectAnswerIndex = 1, TimeLimitSeconds = 15 }
                        ]
                    }];
                }

                _triviaEngine.StartGame(rounds, SelectedTriviaPack);
                TriviaActiveRoundTitle = rounds[0].Title;
                _triviaEngine.StartCurrentQuestion();

                // Automatically launch or focus the 16:9 big-screen display window on the selected monitor
                OpenTriviaDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error starting trivia game: {ex.Message}", "KSRotation Trivia", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

#if !MAUI
        [RelayCommand]
        public void ToggleTriviaDisplay()
        {
            if (_triviaDisplayWindow != null && _triviaDisplayWindow.IsLoaded)
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
            if (_triviaEngine == null) return;

            if (_triviaDisplayWindow == null || !_triviaDisplayWindow.IsLoaded)
            {
                _triviaDisplayVm = new TriviaDisplayViewModel(
                    _triviaEngine,
                    TriviaVenueName,
                    TriviaPatronUrl,
                    null,
                    TriviaWifiSsid,
                    TriviaWifiPassword,
                    TriviaPreGameCountdownMinutes * 60);

                _triviaDisplayVm.UpdateCategory(SelectedTriviaPack);

                _triviaDisplayWindow = new KSRotation.Windows.TriviaDisplayWindow
                {
                    DataContext = _triviaDisplayVm
                };

                _triviaDisplayWindow.Closed += (s, e) =>
                {
                    _triviaDisplayWindow = null;
                    _triviaDisplayVm = null;
                    IsTriviaDisplayOpen = false;
                };

                PositionTriviaDisplayWindow(SelectedMonitorDevice);
                _triviaDisplayWindow.Show();
                PositionTriviaDisplayWindow(SelectedMonitorDevice);
                IsTriviaDisplayOpen = true;
            }
            else
            {
                PositionTriviaDisplayWindow(SelectedMonitorDevice);
                _triviaDisplayWindow.Activate();
            }
        }

        [RelayCommand]
        public void CloseTriviaDisplay()
        {
            if (_triviaDisplayWindow != null && _triviaDisplayWindow.IsLoaded)
            {
                _triviaDisplayWindow.Close();
                _triviaDisplayWindow = null;
                _triviaDisplayVm = null;
                IsTriviaDisplayOpen = false;
            }
        }

        public void PositionTriviaDisplayWindow(string? deviceName)
        {
            if (_triviaDisplayWindow == null || !_triviaDisplayWindow.IsLoaded) return;

            var screens = System.Windows.Forms.Screen.AllScreens;
            var targetScreen = WindowPositioner.ResolveByDeviceName(screens, deviceName);
            if (targetScreen != null)
            {
                _triviaDisplayWindow.WindowState = System.Windows.WindowState.Normal;
                _triviaDisplayWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                WindowPositioner.FillArea(_triviaDisplayWindow, targetScreen.Bounds);
            }
        }
#else
        [RelayCommand]
        public void ToggleTriviaDisplay() { }

        [RelayCommand]
        public void OpenTriviaDisplay() { }

        [RelayCommand]
        public void CloseTriviaDisplay() { }

        public void PositionTriviaDisplayWindow(string? deviceName) { }
#endif

        [RelayCommand]
        public void ToggleTriviaPause()
        {
            _triviaEngine?.TogglePause("Host Manual Pause");
        }

        [RelayCommand]
        public void LockAndRevealTrivia()
        {
            _triviaEngine?.LockAndRevealAnswer();
        }

        [RelayCommand]
        public void NextTriviaQuestion()
        {
            _triviaEngine?.AdvanceToNextQuestion();
        }

        [RelayCommand]
        public void ResetTrivia()
        {
            if (_triviaEngine == null) return;
            // The web server holds a fixed reference to the engine passed at construction, so it
            // must be recreated too - otherwise phones keep talking to the disposed old engine
            // while the screen shows the new one, and the buzzers silently stop working.
            _triviaWebServer?.Dispose();
            _triviaEngine.Dispose();
            _triviaEngine = new TriviaGameEngine(TriviaSettings);
            WireTriviaEngineEvents();
            StartTriviaWebServer();
            TriviaGameStateText = "Lobby";
            IsTriviaGameRunning = false;
            TriviaCurrentQuestionPrompt = "Game reset. Click 'Start Game' to begin.";
            TriviaOptionA = TriviaOptionB = TriviaOptionC = TriviaOptionD = "";
            TriviaCorrectAnswerIndex = -1;
        }

        [RelayCommand]
        public void RefreshTriviaPacks()
        {
            LoadTriviaPacks();
        }

        [RelayCommand]
        public void AutoDetectTriviaWifi()
        {
            try
            {
                string? detectedSsid = WifiHelper.GetConnectedSsid();
                if (!string.IsNullOrWhiteSpace(detectedSsid))
                {
                    TriviaWifiSsid = detectedSsid;
                    string storedPassword = WifiPasswordStore.GetPasswordForSsid(detectedSsid);
                    if (!string.IsNullOrWhiteSpace(storedPassword))
                    {
                        TriviaWifiPassword = storedPassword;
                    }
                    ShowTriviaSettingsStatus($"Auto-detected Wi-Fi Network: \"{detectedSsid}\"");
                }
                else
                {
                    ShowTriviaSettingsStatus("No active Wi-Fi SSID detected.");
                }
            }
            catch (Exception ex)
            {
                ShowTriviaSettingsStatus($"Failed to detect Wi-Fi: {ex.Message}");
            }
        }

        [RelayCommand]
        public void SaveTriviaSettings()
        {
            PushTriviaPropertiesIntoSettings();
            TriviaStorageHelper.SaveSettings(TriviaSettings);

            if (!string.IsNullOrWhiteSpace(TriviaWifiSsid) && !string.IsNullOrWhiteSpace(TriviaWifiPassword))
            {
                WifiPasswordStore.SetPasswordForSsid(TriviaWifiSsid, TriviaWifiPassword);
            }

            if (_triviaEngine != null)
            {
                _triviaEngine.Settings = TriviaSettings;
            }

            ShowTriviaSettingsStatus("Trivia settings saved successfully.");
        }

        [RelayCommand]
        public void ResetTriviaSettings()
        {
            TriviaSettings = new TriviaSettings();
            LoadTriviaSettingsIntoProperties();
            TriviaStorageHelper.SaveSettings(TriviaSettings);

            if (_triviaEngine != null)
            {
                _triviaEngine.Settings = TriviaSettings;
            }

            ShowTriviaSettingsStatus("Trivia settings reset to defaults.");
        }

        private void ShowTriviaSettingsStatus(string message)
        {
            TriviaSettingsStatusMessage = message;
            IsTriviaSettingsStatusVisible = true;

            _triviaSettingsStatusTimer?.Stop();
            _triviaSettingsStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _triviaSettingsStatusTimer.Tick += (s, e) =>
            {
                IsTriviaSettingsStatusVisible = false;
                _triviaSettingsStatusTimer.Stop();
            };
            _triviaSettingsStatusTimer.Start();
        }
    }
}
