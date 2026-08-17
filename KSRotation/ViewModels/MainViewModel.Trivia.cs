// Edited on Aug 17, 2026 @ 16:09:00 -> Pulled Trivia Venue and Host directly from existing MainViewModel properties
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

        [ObservableProperty]
        public partial TriviaSettings TriviaSettings { get; set; } = new();

        [ObservableProperty]
        public partial string SelectedTriviaPack { get; set; } = "";

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
                    TriviaTotalSeconds = q.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : TriviaSettings.DefaultQuestionSeconds;
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
                    rounds = [new TriviaRound
                    {
                        RoundNumber = 1,
                        Title = matchedPack.Title,
                        Questions = matchedPack.Questions
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
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error starting trivia game: {ex.Message}", "KSRotation Trivia", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

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
            _triviaEngine.Dispose();
            _triviaEngine = new TriviaGameEngine(TriviaSettings);
            WireTriviaEngineEvents();
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
