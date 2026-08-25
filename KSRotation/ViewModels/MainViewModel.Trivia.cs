// Edited on Aug 25, 2026 @ 06:37:00 -> Fix RCS1139 summary tags, RCS1163 unused parameters, RCS1021, and RCS1146
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
        private readonly DispatcherTimer _triviaPreGameTimer = new();
#if !MAUI
        private KSRotation.Windows.TriviaDisplayWindow? _triviaDisplayWindow;
        private TriviaDisplayViewModel? _triviaDisplayVm;
#endif

        [ObservableProperty]
        public partial bool IsTriviaDisplayOpen { get; set; }

        [ObservableProperty]
        public partial TriviaSettings TriviaSettings { get; set; } = new();

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
        public partial int TriviaCurrentQuestionNumber { get; set; } = 1;

        partial void OnTriviaCurrentQuestionNumberChanged(int value)
        {
            // Guard against reacting to programmatic resets (e.g. pack-selection preview) - only
            // a live/in-progress session should have question-jump navigation applied to it.
            if (_triviaEngine?.CurrentSession?.CurrentRound != null &&
                _triviaEngine.State != TriviaGameState.Lobby &&
                _triviaEngine.State != TriviaGameState.GameComplete)
            {
                int targetIdx = value - 1;
                if (targetIdx >= 0 && targetIdx < _triviaEngine.CurrentSession.CurrentRound.Questions.Count)
                {
                    if (_triviaEngine.CurrentSession.CurrentQuestionIndex != targetIdx)
                    {
                        _triviaEngine.GoToQuestion(targetIdx, startTimerImmediately: TriviaAutoAdvance);
                    }
                }
            }
        }

        [ObservableProperty]
        public partial int TriviaTotalQuestionsInRound { get; set; } = 1;

        public ObservableCollection<int> TriviaAvailableQuestionNumbers { get; } = [];

        private void UpdateTriviaAvailableQuestionNumbers(int count)
        {
            TriviaAvailableQuestionNumbers.Clear();
            for (int i = 1; i <= count; i++)
            {
                TriviaAvailableQuestionNumbers.Add(i);
            }
        }

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

        [ObservableProperty]
        public partial int TriviaConnectedPlayerCount { get; set; }

        public int[] TriviaQuestionsPerGamePresets { get; } = [5, 10, 15, 20, 25, 50, 100];

        // Separate Trivia Settings Properties (Venue and Host pulled directly from active app settings)
        public string TriviaVenueName => !string.IsNullOrWhiteSpace(VenueName) ? VenueName : "Main Venue";

        public string TriviaHostName => !string.IsNullOrWhiteSpace(DjName) ? DjName : "DJ / Host";

        [ObservableProperty]
        public partial string TriviaInstructionBannerText { get; set; } = TriviaSettings.DefaultInstructionBannerText;

        partial void OnTriviaInstructionBannerTextChanged(string value)
        {
            TriviaSettings.InstructionBannerText = value;
            if (_triviaEngine != null) _triviaEngine.Settings.InstructionBannerText = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
#if !MAUI
            _triviaDisplayVm?.UpdateInstructionBannerTemplate(value);
#endif
        }

        [ObservableProperty]
        public partial int TriviaQuestionsPerGame { get; set; } = 10;

        partial void OnTriviaQuestionsPerGameChanged(int value)
        {
            TriviaSettings.QuestionsPerGame = value;
            if (_triviaEngine != null) _triviaEngine.Settings.QuestionsPerGame = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
#if !MAUI
            UpdateTriviaCategoryPreview();
#endif
        }

        [ObservableProperty]
        public partial int TriviaDefaultQuestionSeconds { get; set; } = 15;

        partial void OnTriviaDefaultQuestionSecondsChanged(int value)
        {
            TriviaSettings.DefaultQuestionSeconds = value;
            if (_triviaEngine != null) _triviaEngine.Settings.DefaultQuestionSeconds = value;
            OnPropertyChanged(nameof(TriviaAutoRunTimingSummary));
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaWarningCountdownSeconds { get; set; } = 3;

        partial void OnTriviaWarningCountdownSecondsChanged(int value)
        {
            TriviaSettings.WarningCountdownSeconds = value;
            if (_triviaEngine != null) _triviaEngine.Settings.WarningCountdownSeconds = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaAnswerEliminationIntervalSeconds { get; set; } = 5;

        partial void OnTriviaAnswerEliminationIntervalSecondsChanged(int value)
        {
            TriviaSettings.AnswerEliminationIntervalSeconds = value;
            if (_triviaEngine != null) _triviaEngine.Settings.AnswerEliminationIntervalSeconds = value;
            OnPropertyChanged(nameof(TriviaAutoRunTimingSummary));
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPostRevealDelaySeconds { get; set; } = 5;

        partial void OnTriviaPostRevealDelaySecondsChanged(int value)
        {
            TriviaSettings.PostRevealDelaySeconds = value;
            if (_triviaEngine != null) _triviaEngine.Settings.PostRevealDelaySeconds = value;
            OnPropertyChanged(nameof(TriviaAutoRunTimingSummary));
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        /// <summary>
        /// Live summary of the auto-run timing (shown under the "Auto-Run Game" toggle) so the
        /// header always reflects the game master's actual configured timings instead of a
        /// hardcoded "15s answer • 5s fade • 5s reveal" that goes stale the moment they're changed.
        /// </summary>
        public string TriviaAutoRunTimingSummary =>
            $"{TriviaDefaultQuestionSeconds}s answer • {TriviaAnswerEliminationIntervalSeconds}s fade • {TriviaPostRevealDelaySeconds}s reveal";

        [ObservableProperty]
        public partial int TriviaBasePoints { get; set; } = 1000;

        partial void OnTriviaBasePointsChanged(int value)
        {
            TriviaSettings.BasePointsPerQuestion = value;
            if (_triviaEngine != null) _triviaEngine.Settings.BasePointsPerQuestion = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaWrongAnswerDeduction { get; set; } = 0;

        partial void OnTriviaWrongAnswerDeductionChanged(int value)
        {
            TriviaSettings.WrongAnswerDeductionPoints = value;
            if (_triviaEngine != null) _triviaEngine.Settings.WrongAnswerDeductionPoints = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial bool TriviaTieredScoringEnabled { get; set; } = true;

        partial void OnTriviaTieredScoringEnabledChanged(bool value)
        {
            TriviaSettings.TieredScoringEnabled = value;
            if (_triviaEngine != null) _triviaEngine.Settings.TieredScoringEnabled = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPoints4OptionsPercent { get; set; } = 100;

        partial void OnTriviaPoints4OptionsPercentChanged(int value)
        {
            TriviaSettings.Points4OptionsPercent = value;
            if (_triviaEngine != null) _triviaEngine.Settings.Points4OptionsPercent = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPoints3OptionsPercent { get; set; } = 70;

        partial void OnTriviaPoints3OptionsPercentChanged(int value)
        {
            TriviaSettings.Points3OptionsPercent = value;
            if (_triviaEngine != null) _triviaEngine.Settings.Points3OptionsPercent = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPoints2OptionsPercent { get; set; } = 40;

        partial void OnTriviaPoints2OptionsPercentChanged(int value)
        {
            TriviaSettings.Points2OptionsPercent = value;
            if (_triviaEngine != null) _triviaEngine.Settings.Points2OptionsPercent = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial bool TriviaSpeedBonusEnabled { get; set; } = true;

        partial void OnTriviaSpeedBonusEnabledChanged(bool value)
        {
            TriviaSettings.SpeedBonusEnabled = value;
            if (_triviaEngine != null) _triviaEngine.Settings.SpeedBonusEnabled = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaMaxSpeedBonus { get; set; } = 500;

        partial void OnTriviaMaxSpeedBonusChanged(int value)
        {
            TriviaSettings.MaxSpeedBonus = value;
            if (_triviaEngine != null) _triviaEngine.Settings.MaxSpeedBonus = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial bool TriviaStreakBonusEnabled { get; set; } = true;

        partial void OnTriviaStreakBonusEnabledChanged(bool value)
        {
            TriviaSettings.StreakBonusMultiplier = value ? TriviaStreakMultiplier : 0;
            if (_triviaEngine != null) _triviaEngine.Settings.StreakBonusMultiplier = TriviaSettings.StreakBonusMultiplier;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial double TriviaStreakMultiplier { get; set; } = 0.1;

        partial void OnTriviaStreakMultiplierChanged(double value)
        {
            if (TriviaStreakBonusEnabled)
            {
                TriviaSettings.StreakBonusMultiplier = value;
                if (_triviaEngine != null) _triviaEngine.Settings.StreakBonusMultiplier = value;
                TriviaStorageHelper.SaveSettings(TriviaSettings);
            }
        }

        [ObservableProperty]
        public partial bool TriviaAutoAdvance { get; set; } = true;

        partial void OnTriviaAutoAdvanceChanged(bool value)
        {
            TriviaSettings.AutoAdvanceQuestions = value;
            if (_triviaEngine != null) _triviaEngine.Settings.AutoAdvanceQuestions = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial bool TriviaAutoStartNextGame { get; set; } = true;

        partial void OnTriviaAutoStartNextGameChanged(bool value)
        {
            TriviaSettings.AutoStartNextGameEnabled = value;
            if (_triviaEngine != null) _triviaEngine.Settings.AutoStartNextGameEnabled = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaNextGameDelayMinutes { get; set; } = 3;

        partial void OnTriviaNextGameDelayMinutesChanged(int value)
        {
            TriviaSettings.NextGameDelayMinutes = value;
            if (_triviaEngine != null) _triviaEngine.Settings.NextGameDelayMinutes = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPreGameCountdownMinutes { get; set; } = 5;

        partial void OnTriviaPreGameCountdownMinutesChanged(int value)
        {
            TriviaSettings.PreGameCountdownMinutes = value;
            if (_triviaEngine != null) _triviaEngine.Settings.PreGameCountdownMinutes = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPreGameSecondsRemaining { get; set; } = 300;

        [ObservableProperty]
        public partial bool TriviaIsPreGameCountdownRunning { get; set; }

        [ObservableProperty]
        public partial string TriviaPreGameCountdownText { get; set; } = "05:00";

        [ObservableProperty]
        public partial bool TriviaIsShowingConnectScreen { get; set; } = true;

        [ObservableProperty]
        public partial bool TriviaAutoStartAfterCountdown { get; set; } = true;

        partial void OnTriviaAutoStartAfterCountdownChanged(bool value)
        {
            TriviaSettings.AutoStartAfterCountdown = value;
            if (_triviaEngine != null) _triviaEngine.Settings.AutoStartAfterCountdown = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial int TriviaPort { get; set; } = 8085;

        partial void OnTriviaPortChanged(int value)
        {
            TriviaSettings.Port = value;
            if (_triviaEngine != null) _triviaEngine.Settings.Port = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial string TriviaWifiSsid { get; set; } = "";

        partial void OnTriviaWifiSsidChanged(string value)
        {
            TriviaSettings.WifiSsid = value;
            if (_triviaEngine != null) _triviaEngine.Settings.WifiSsid = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
        }

        [ObservableProperty]
        public partial string TriviaWifiPassword { get; set; } = "";

        partial void OnTriviaWifiPasswordChanged(string value)
        {
            TriviaSettings.WifiPassword = value;
            if (_triviaEngine != null) _triviaEngine.Settings.WifiPassword = value;
            TriviaStorageHelper.SaveSettings(TriviaSettings);
            if (!string.IsNullOrWhiteSpace(TriviaWifiSsid) && !string.IsNullOrWhiteSpace(value))
            {
                WifiPasswordStore.SetPasswordForSsid(TriviaWifiSsid, value);
            }
        }

        [ObservableProperty]
        public partial string TriviaSettingsStatusMessage { get; set; } = "";

        [ObservableProperty]
        public partial bool IsTriviaSettingsStatusVisible { get; set; }

        public ObservableCollection<SelectableTriviaPack> TriviaSelectablePacks { get; } = [];
        public ObservableCollection<TriviaPlayer> TriviaPlayers { get; } = [];
        public ObservableCollection<AnswerDistributionItem> TriviaAnswerDistribution { get; } = [];

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

                _triviaPreGameTimer.Interval = TimeSpan.FromSeconds(1);
                _triviaPreGameTimer.Tick += OnTriviaPreGameTimerTick;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing trivia: {ex.Message}");
            }
        }

        private void WireTriviaEngineEvents()
        {
            if (_triviaEngine == null) return;

            _triviaEngine.StateChanged += (_, state) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaGameStateText = state.ToString();
                    IsTriviaGameRunning = state != TriviaGameState.Lobby && state != TriviaGameState.GameComplete;
                });
            };

            _triviaEngine.TimerTick += (_, remaining) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaRemainingSeconds = remaining;
                    TriviaTotalSeconds = _triviaEngine.TotalCountdownSeconds;
                });
            };

            _triviaEngine.QuestionStarted += (_, q) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaCurrentQuestionPrompt = q.Prompt;
                    TriviaOptionA = q.Options.Count > 0 ? q.Options[0] : "";
                    TriviaOptionB = q.Options.Count > 1 ? q.Options[1] : "";
                    TriviaOptionC = q.Options.Count > 2 ? q.Options[2] : "";
                    TriviaOptionD = q.Options.Count > 3 ? q.Options[3] : "";
                    TriviaCorrectAnswerIndex = -1;
                    TriviaTotalSeconds = _triviaEngine.TotalCountdownSeconds;
                    TriviaRemainingSeconds = TriviaTotalSeconds;
                    TriviaCurrentQuestionNumber = _triviaEngine.CurrentSession.CurrentQuestionIndex + 1;
                    TriviaAnswerDistribution.Clear();
                });
            };

            _triviaEngine.AnswerRevealed += (_, q) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaCorrectAnswerIndex = q.CorrectAnswerIndex;

                    TriviaAnswerDistribution.Clear();
                    var dist = _triviaEngine.GetAnswerDistribution();
                    string[] labels = ["A", "B", "C", "D"];
                    for (int i = 0; i < 4; i++)
                    {
                        int count = dist.GetValueOrDefault(i, 0);
                        string text = (q.Options.Count > i) ? q.Options[i] : $"Option {labels[i]}";
                        bool isCorrect = (i == q.CorrectAnswerIndex);
                        TriviaAnswerDistribution.Add(new AnswerDistributionItem(labels[i], text, count, isCorrect));
                    }
                });
            };

            _triviaEngine.LeaderboardUpdated += (_, playersList) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    TriviaPlayers.Clear();
                    foreach (var p in playersList)
                    {
                        TriviaPlayers.Add(p);
                    }
                    TriviaConnectedPlayerCount = TriviaPlayers.Count(p => p.IsConnected);
                });
            };

            _triviaEngine.GamePaused += (_, reason) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    IsTriviaPaused = true;
                    TriviaPauseReason = reason ?? "Paused";
                    TriviaPauseButtonText = "▶ Resume";
                });
            };

            _triviaEngine.GameResumed += (_, _) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    IsTriviaPaused = false;
                    TriviaPauseReason = "";
                    TriviaPauseButtonText = "⏸ Pause";
                });
            };

            _triviaEngine.IntermissionCompleted += (_, _) =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => StartTrivia());
            };
        }

        private void OnTriviaPreGameTimerTick(object? sender, EventArgs e)
        {
            if (TriviaIsPreGameCountdownRunning && TriviaPreGameSecondsRemaining > 0)
            {
                TriviaPreGameSecondsRemaining--;
                int mins = TriviaPreGameSecondsRemaining / 60;
                int secs = TriviaPreGameSecondsRemaining % 60;
                TriviaPreGameCountdownText = $"{mins:D2}:{secs:D2}";
#if !MAUI
                _triviaDisplayVm?.UpdatePreGameCountdown(TriviaPreGameSecondsRemaining);
#endif
                if (TriviaPreGameSecondsRemaining == 0)
                {
                    _triviaPreGameTimer.Stop();
                    TriviaIsPreGameCountdownRunning = false;
                    if (TriviaAutoStartAfterCountdown)
                    {
                        StartTrivia();
                    }
                }
            }
        }

        private List<TriviaQuestionPack> GetCheckedTriviaPacks()
        {
            var checkedPacks = TriviaSelectablePacks.Where(sp => sp.IsChecked).Select(sp => sp.Pack).ToList();
            if (checkedPacks.Count > 0) return checkedPacks;
            return TriviaSelectablePacks.Count > 0 ? [TriviaSelectablePacks[0].Pack] : [];
        }

        private void LoadTriviaPacks()
        {
            TriviaSelectablePacks.Clear();
            var packs = TriviaPackManager.LoadAllPacks();
            bool isFirst = true;
            foreach (var p in packs)
            {
                var selectable = new SelectableTriviaPack(p, isChecked: isFirst);
                selectable.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableTriviaPack.IsChecked))
                    {
#if !MAUI
                        UpdateTriviaCategoryPreview();
#endif
                    }
                };
                TriviaSelectablePacks.Add(selectable);
                isFirst = false;
            }

            if (TriviaSelectablePacks.Count == 0)
            {
                Lyracist.Shared.Globals.LogError("KSRotation",
                    $"No trivia question packs found in '{TriviaPackManager.GetDefaultPacksDirectory()}'. Category list will be empty until a .json pack is placed there.",
                    "LoadTriviaPacks");
            }
        }

#if !MAUI
        private void UpdateTriviaCategoryPreview()
        {
            var checkedPacks = GetCheckedTriviaPacks();
            if (checkedPacks.Count == 0) return;

            int questionCount = Math.Clamp(TriviaQuestionsPerGame > 0 ? TriviaQuestionsPerGame : 10, 1, checkedPacks.Sum(p => p.Questions.Count));
            _triviaDisplayVm?.UpdateFeaturedCategoryHeader(questionCount, checkedPacks.Count);

            if (checkedPacks.Count == 1)
            {
                _triviaDisplayVm?.UpdateCategory(checkedPacks[0].Category, checkedPacks[0].Description);
            }
            else
            {
                _triviaDisplayVm?.UpdateMixedCategory(checkedPacks);
            }
        }
#endif

        private void StartTriviaWebServer()
        {
            if (_triviaEngine == null) return;
            try
            {
                string ip = LocalNetworkHelper.GetLocalIPv4Address()?.ToString() ?? "127.0.0.1";
                _triviaWebServer = new TriviaWebServer(_triviaEngine, TriviaSettings.Port);
                _triviaWebServer.Start();
                if (!_triviaWebServer.IsRunning)
                {
                    TriviaServerStatusText = $"Server Error: could not bind port {TriviaSettings.Port} (already in use?)";
                    return;
                }
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

            bool isSpecialEventActive = !string.IsNullOrEmpty(ActiveSpecialEvent) &&
                !ActiveSpecialEvent.Equals("None", StringComparison.OrdinalIgnoreCase);
            bool isScreenOccupied = IsDisplayEnabled || IsDjBannerEnabled || isSpecialEventActive;
            if (isScreenOccupied)
            {
                string reason = isSpecialEventActive ? "Special Event" : (IsDjBannerEnabled ? "DJ Banner Display" : "Rotation Screen Active");
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
            TriviaInstructionBannerText = string.IsNullOrWhiteSpace(TriviaSettings.InstructionBannerText) ? TriviaSettings.DefaultInstructionBannerText : TriviaSettings.InstructionBannerText;
            TriviaQuestionsPerGame = TriviaSettings.QuestionsPerGame > 0 ? TriviaSettings.QuestionsPerGame : 10;
            TriviaDefaultQuestionSeconds = TriviaSettings.DefaultQuestionSeconds;
            TriviaWarningCountdownSeconds = TriviaSettings.WarningCountdownSeconds;
            TriviaAnswerEliminationIntervalSeconds = TriviaSettings.AnswerEliminationIntervalSeconds;
            TriviaPostRevealDelaySeconds = TriviaSettings.PostRevealDelaySeconds > 0 ? TriviaSettings.PostRevealDelaySeconds : 5;
            TriviaBasePoints = TriviaSettings.BasePointsPerQuestion;
            TriviaWrongAnswerDeduction = TriviaSettings.WrongAnswerDeductionPoints;
            TriviaTieredScoringEnabled = TriviaSettings.TieredScoringEnabled;
            TriviaPoints4OptionsPercent = TriviaSettings.Points4OptionsPercent;
            TriviaPoints3OptionsPercent = TriviaSettings.Points3OptionsPercent;
            TriviaPoints2OptionsPercent = TriviaSettings.Points2OptionsPercent;
            TriviaSpeedBonusEnabled = TriviaSettings.SpeedBonusEnabled;
            TriviaMaxSpeedBonus = TriviaSettings.MaxSpeedBonus;
            TriviaStreakBonusEnabled = TriviaSettings.StreakBonusMultiplier > 0;
            TriviaStreakMultiplier = TriviaSettings.StreakBonusMultiplier;
            TriviaAutoAdvance = TriviaSettings.AutoAdvanceQuestions;
            TriviaAutoStartNextGame = TriviaSettings.AutoStartNextGameEnabled;
            TriviaNextGameDelayMinutes = TriviaSettings.NextGameDelayMinutes;
            TriviaPreGameCountdownMinutes = TriviaSettings.PreGameCountdownMinutes > 0 ? TriviaSettings.PreGameCountdownMinutes : 5;
            TriviaPreGameSecondsRemaining = TriviaPreGameCountdownMinutes * 60;
            TriviaPreGameCountdownText = $"{TriviaPreGameCountdownMinutes:D2}:00";
            TriviaAutoStartAfterCountdown = TriviaSettings.AutoStartAfterCountdown;
            TriviaPort = TriviaSettings.Port;
            TriviaWifiSsid = TriviaSettings.WifiSsid;
            TriviaWifiPassword = TriviaSettings.WifiPassword;
        }

        private void PushTriviaPropertiesIntoSettings()
        {
            TriviaSettings.VenueName = TriviaVenueName;
            TriviaSettings.HostName = TriviaHostName;
            TriviaSettings.InstructionBannerText = TriviaInstructionBannerText;
            TriviaSettings.QuestionsPerGame = TriviaQuestionsPerGame;
            TriviaSettings.DefaultQuestionSeconds = TriviaDefaultQuestionSeconds;
            TriviaSettings.WarningCountdownSeconds = TriviaWarningCountdownSeconds;
            TriviaSettings.AnswerEliminationIntervalSeconds = TriviaAnswerEliminationIntervalSeconds;
            TriviaSettings.PostRevealDelaySeconds = TriviaPostRevealDelaySeconds;
            TriviaSettings.BasePointsPerQuestion = TriviaBasePoints;
            TriviaSettings.WrongAnswerDeductionPoints = TriviaWrongAnswerDeduction;
            TriviaSettings.TieredScoringEnabled = TriviaTieredScoringEnabled;
            TriviaSettings.Points4OptionsPercent = TriviaPoints4OptionsPercent;
            TriviaSettings.Points3OptionsPercent = TriviaPoints3OptionsPercent;
            TriviaSettings.Points2OptionsPercent = TriviaPoints2OptionsPercent;
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
        public void LaunchTriviaPreGameLobby()
        {
            TriviaIsShowingConnectScreen = true;
            if (!TriviaIsPreGameCountdownRunning)
            {
                if (TriviaPreGameSecondsRemaining <= 0)
                {
                    TriviaPreGameSecondsRemaining = TriviaPreGameCountdownMinutes * 60;
                    int mins = TriviaPreGameSecondsRemaining / 60;
                    int secs = TriviaPreGameSecondsRemaining % 60;
                    TriviaPreGameCountdownText = $"{mins:D2}:{secs:D2}";
                }
                TriviaIsPreGameCountdownRunning = true;
                _triviaPreGameTimer.Start();
            }

            OpenTriviaDisplay();
#if !MAUI
            _triviaDisplayVm?.UpdatePreGameCountdown(TriviaPreGameSecondsRemaining);
            UpdateTriviaCategoryPreview();
#endif
        }

        [RelayCommand]
        public void StartTrivia()
        {
            if (_triviaEngine == null) return;
            try
            {
                // Cancel pre-game countdown and dismiss lobby screen
                _triviaPreGameTimer.Stop();
                TriviaIsPreGameCountdownRunning = false;
                TriviaIsShowingConnectScreen = false;
#if !MAUI
                if (_triviaDisplayVm != null)
                {
                    _triviaDisplayVm.IsConnectInstructionsActive = false;
                    _triviaDisplayVm.IsPreGameCountdownRunning = false;
                }
#endif

                List<TriviaRound> rounds = [];
                var checkedPacks = GetCheckedTriviaPacks();
                if (checkedPacks.Count > 0)
                {
                    var gameQuestions = TriviaPackManager.BuildMixedQuestionSet(checkedPacks, TriviaQuestionsPerGame);
                    if (gameQuestions.Count > 0)
                    {
                        string title = checkedPacks.Count == 1 ? checkedPacks[0].Title : string.Join(" + ", checkedPacks.Select(p => p.Title));
                        rounds = [new TriviaRound
                        {
                            RoundNumber = 1,
                            Title = title,
                            Category = checkedPacks.Count == 1 ? checkedPacks[0].Category : "Mixed Trivia",
                            Questions = gameQuestions
                        }];
                    }
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

                PushTriviaPropertiesIntoSettings();
                TriviaStorageHelper.SaveSettings(TriviaSettings);
                _triviaEngine.Settings = TriviaSettings;

                _triviaEngine.StartGame(rounds, rounds[0].Title);
                TriviaActiveRoundTitle = rounds[0].Title;
                TriviaTotalQuestionsInRound = rounds[0].Questions.Count;
                UpdateTriviaAvailableQuestionNumbers(TriviaTotalQuestionsInRound);
                TriviaCurrentQuestionNumber = 1;

                // Start 1st question (immediate timer if Auto-Run, or reading/standby state if manual)
                _triviaEngine.PrepareCurrentQuestion(startTimerImmediately: TriviaAutoAdvance);

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
            if (_triviaDisplayWindow?.IsLoaded == true)
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

            if (_triviaDisplayWindow?.IsLoaded != true)
            {
                _triviaDisplayVm = new TriviaDisplayViewModel(
                    _triviaEngine,
                    TriviaVenueName,
                    TriviaPatronUrl,
                    null,
                    TriviaWifiSsid,
                    TriviaWifiPassword,
                    TriviaPreGameSecondsRemaining,
                    brandName: "KSRotation");

                _triviaDisplayVm.HostName = TriviaHostName;
                _triviaDisplayVm.UpdateInstructionBannerTemplate(TriviaInstructionBannerText);
                _triviaDisplayVm.IsConnectInstructionsActive = (_triviaEngine.State == TriviaGameState.Lobby) && TriviaIsShowingConnectScreen;

                var checkedPacksForDisplay = GetCheckedTriviaPacks();
                if (checkedPacksForDisplay.Count > 0)
                {
                    int questionCountForDisplay = Math.Clamp(TriviaQuestionsPerGame > 0 ? TriviaQuestionsPerGame : 10, 1, checkedPacksForDisplay.Sum(p => p.Questions.Count));
                    _triviaDisplayVm.UpdateFeaturedCategoryHeader(questionCountForDisplay, checkedPacksForDisplay.Count);
                }

                if (checkedPacksForDisplay.Count == 1)
                {
                    _triviaDisplayVm.UpdateCategory(checkedPacksForDisplay[0].Category, checkedPacksForDisplay[0].Description);
                }
                else if (checkedPacksForDisplay.Count > 1)
                {
                    _triviaDisplayVm.UpdateMixedCategory(checkedPacksForDisplay);
                }

                _triviaDisplayVm.SyncWithEngine();

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
                _triviaDisplayVm?.SyncWithEngine();
                PositionTriviaDisplayWindow(SelectedMonitorDevice);
                _triviaDisplayWindow.Activate();
            }
        }

        [RelayCommand]
        public void CloseTriviaDisplay()
        {
            if (_triviaDisplayWindow?.IsLoaded == true)
            {
                _triviaDisplayWindow.Close();
                _triviaDisplayWindow = null;
                _triviaDisplayVm = null;
                IsTriviaDisplayOpen = false;
            }
        }

        public void PositionTriviaDisplayWindow(string? deviceName)
        {
            if (_triviaDisplayWindow?.IsLoaded != true) return;

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

        public void PositionTriviaDisplayWindow(string? deviceName) { _ = deviceName; }
#endif

        [RelayCommand]
        public void StartTriviaPreGameCountdown(object? parameter)
        {
            int minutes = 5;
            if (parameter is int i) minutes = i;
            else if (parameter is string s && int.TryParse(s, out int parsed)) minutes = parsed;

            TriviaPreGameCountdownMinutes = minutes > 0 ? minutes : 5;
            TriviaPreGameSecondsRemaining = TriviaPreGameCountdownMinutes * 60;
            int mins = TriviaPreGameSecondsRemaining / 60;
            int secs = TriviaPreGameSecondsRemaining % 60;
            TriviaPreGameCountdownText = $"{mins:D2}:{secs:D2}";
            TriviaIsPreGameCountdownRunning = true;
            _triviaPreGameTimer.Stop();
            _triviaPreGameTimer.Start();
#if !MAUI
            _triviaDisplayVm?.UpdatePreGameCountdown(TriviaPreGameSecondsRemaining);
#endif
        }

        [RelayCommand]
        public void AddTriviaPreGameMinutes(object? parameter)
        {
            int deltaMinutes = 1;
            if (parameter is int i) deltaMinutes = i;
            else if (parameter is string s && int.TryParse(s, out int parsed)) deltaMinutes = parsed;

            TriviaPreGameSecondsRemaining = Math.Max(0, TriviaPreGameSecondsRemaining + (deltaMinutes * 60));
            int mins = TriviaPreGameSecondsRemaining / 60;
            int secs = TriviaPreGameSecondsRemaining % 60;
            TriviaPreGameCountdownText = $"{mins:D2}:{secs:D2}";
#if !MAUI
            _triviaDisplayVm?.UpdatePreGameCountdown(TriviaPreGameSecondsRemaining);
#endif
            if (!TriviaIsPreGameCountdownRunning && TriviaPreGameSecondsRemaining > 0)
            {
                TriviaIsPreGameCountdownRunning = true;
                _triviaPreGameTimer.Start();
            }
        }

        [RelayCommand]
        public void Add1TriviaPreGameMinute() => AddTriviaPreGameMinutes(1);

        [RelayCommand]
        public void Add5TriviaPreGameMinutes() => AddTriviaPreGameMinutes(5);

        [RelayCommand]
        public void Reset5TriviaPreGameMinutes() => StartTriviaPreGameCountdown(5);

        [RelayCommand]
        public void ToggleTriviaPreGameTimer()
        {
            TriviaIsPreGameCountdownRunning = !TriviaIsPreGameCountdownRunning;
            if (TriviaIsPreGameCountdownRunning)
            {
                _triviaPreGameTimer.Start();
            }
            else
            {
                _triviaPreGameTimer.Stop();
            }
        }

        [RelayCommand]
        public void ToggleTriviaConnectInstructions()
        {
            TriviaIsShowingConnectScreen = !TriviaIsShowingConnectScreen;
#if !MAUI
            if (_triviaDisplayVm != null)
            {
                _triviaDisplayVm.IsConnectInstructionsActive = TriviaIsShowingConnectScreen;
            }
#endif
        }

        [RelayCommand]
        public void RemoveTriviaPlayer(TriviaPlayer? player)
        {
            if (player != null)
            {
                _triviaEngine?.RemovePlayer(player.Name);
                TriviaPlayers.Remove(player);
                TriviaConnectedPlayerCount = TriviaPlayers.Count(p => p.IsConnected);
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
        public void StartTriviaQuestion()
        {
            _triviaEngine?.StartCurrentQuestion();
        }

        [RelayCommand]
        public void PreviousTriviaQuestion()
        {
            if (_triviaEngine == null) return;
            bool hasPrev = _triviaEngine.PreviousQuestion(startTimerImmediately: TriviaAutoAdvance);
            if (hasPrev && _triviaEngine.CurrentSession.CurrentQuestion != null)
            {
                TriviaCurrentQuestionNumber = _triviaEngine.CurrentSession.CurrentQuestionIndex + 1;
            }
        }

        [RelayCommand]
        public void NextTriviaQuestion()
        {
            if (_triviaEngine == null) return;
            bool hasNext = _triviaEngine.AdvanceToNextQuestion(startTimerImmediately: TriviaAutoAdvance);
            if (hasNext && _triviaEngine.CurrentSession.CurrentQuestion != null)
            {
                TriviaCurrentQuestionNumber = _triviaEngine.CurrentSession.CurrentQuestionIndex + 1;
            }
        }

        [RelayCommand]
        public void AddTriviaTimerSeconds(object? parameter)
        {
            int seconds = 5;
            if (parameter is int i) seconds = i;
            else if (parameter is string s && int.TryParse(s, out int parsed)) seconds = parsed;

            _triviaEngine?.AdjustRemainingSeconds(seconds);
        }

        [RelayCommand]
        public void ResetTriviaTimer()
        {
            _triviaEngine?.ResetQuestionTimer();
        }

        [RelayCommand]
        public void EliminateNextTriviaWrong()
        {
            _triviaEngine?.EliminateNextWrongAnswer();
        }

        [RelayCommand]
        public void InstantRevealTrivia()
        {
            _triviaEngine?.InstantRevealAnswer();
        }

        [RelayCommand]
        public void VoidCurrentTriviaQuestion()
        {
            if (_triviaEngine == null) return;
            _triviaEngine.VoidCurrentQuestion();
            if (_triviaEngine.CurrentSession.CurrentQuestion != null)
            {
                TriviaCurrentQuestionNumber = _triviaEngine.CurrentSession.CurrentQuestionIndex + 1;
            }
        }

        [RelayCommand]
        public void ResetTrivia()
        {
            if (_triviaEngine == null) return;
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
            TriviaCurrentQuestionNumber = 1;
            TriviaAvailableQuestionNumbers.Clear();
            TriviaAnswerDistribution.Clear();
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
