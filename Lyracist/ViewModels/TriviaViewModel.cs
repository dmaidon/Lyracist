// Edited on Aug 19, 2026 @ 11:15:30 -> Respect GameMaster DefaultQuestionSeconds over question JSON default
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Services.Display;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;

namespace Lyracist.ViewModels;

public partial class TriviaViewModel : BaseViewModel, IDisposable
{
    private readonly IDisplayService _displayService;
    private readonly TriviaDatabaseService _dbService;
    private TriviaGameEngine _engine;
    private TriviaWebServer? _webServer;

    [ObservableProperty]
    private TriviaSettings _settings;

    [ObservableProperty]
    private string _gameTitle = "Trivia Night Pro";

    [ObservableProperty]
    private string _activeRoundTitle = "Round 1";

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
    private int _remainingSeconds;

    [ObservableProperty]
    private int _totalSeconds = 15;

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
    private int _selectedScreenIndex = 0;

    [ObservableProperty]
    private string _serverStatusText = "Server Offline";

    [ObservableProperty]
    private string _patronUrl = "http://localhost:8085/trivia";

    public ObservableCollection<SelectableTriviaPack> SelectablePacks { get; } = [];
    public ObservableCollection<TriviaPlayer> Players { get; } = [];
    public ObservableCollection<string> ScreenOptions { get; } = [];

    public TriviaGameEngine Engine => _engine;

    public TriviaViewModel(IDisplayService displayService)
    {
        _displayService = displayService;
        _settings = TriviaStorageHelper.LoadSettings();
        _engine = new TriviaGameEngine(_settings);
        _dbService = new TriviaDatabaseService();

        _displayService.SetTriviaGameEngine(_engine);

        WireEngineEvents();
        LoadPacks();
        LoadScreens();
        StartWebServer();
    }

    private void WireEngineEvents()
    {
        _engine.StateChanged += (s, state) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                GameStateText = state.ToString();
                IsGameRunning = state != TriviaGameState.Lobby && state != TriviaGameState.GameComplete;
            });
        };

        _engine.TimerTick += (s, remaining) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RemainingSeconds = remaining;
            });
        };

        _engine.QuestionStarted += (s, q) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CurrentQuestionPrompt = q.Prompt;
                OptionA = q.Options.Count > 0 ? q.Options[0] : "";
                OptionB = q.Options.Count > 1 ? q.Options[1] : "";
                OptionC = q.Options.Count > 2 ? q.Options[2] : "";
                OptionD = q.Options.Count > 3 ? q.Options[3] : "";
                CorrectAnswerIndex = -1; // Hidden until reveal
                TotalSeconds = Settings.DefaultQuestionSeconds > 0 ? Settings.DefaultQuestionSeconds : (q.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : 15);
                RemainingSeconds = TotalSeconds;
            });
        };

        _engine.AnswerRevealed += (s, q) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CorrectAnswerIndex = q.CorrectAnswerIndex;
            });
        };

        _engine.LeaderboardUpdated += (s, playersList) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Players.Clear();
                foreach (var p in playersList)
                {
                    Players.Add(p);
                }
            });
        };

        _engine.GamePaused += (s, reason) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsPaused = true;
                PauseReason = reason ?? "Paused";
                PauseButtonText = "▶ Resume";
            });
        };

        _engine.GameResumed += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsPaused = false;
                PauseReason = "";
                PauseButtonText = "⏸ Pause";
            });
        };

        _engine.IntermissionCompleted += (s, e) =>
        {
            // The engine only reaches here when AutoStartNextGameEnabled was on, so it is safe to
            // unconditionally start the next round - without this, "Auto-Start Next Game" silently
            // does nothing and the show stops after one game. Whatever packs are checked stays
            // checked between games - StartGame draws a fresh random question set every time it
            // runs, so this alone gives a different game each time.
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                StartGame();
            });
        };
    }

    /// <summary>
    /// Returns every checked pack, or the first available pack if none are checked (so "Start
    /// Game" always has something to play instead of silently doing nothing).
    /// </summary>
    private List<TriviaQuestionPack> GetCheckedPacks()
    {
        var checkedPacks = SelectablePacks.Where(sp => sp.IsChecked).Select(sp => sp.Pack).ToList();
        if (checkedPacks.Count > 0) return checkedPacks;
        return SelectablePacks.Count > 0 ? [SelectablePacks[0].Pack] : [];
    }

    public void ApplySettings(TriviaSettings newSettings)
    {
        Settings = newSettings;
        _engine.Settings = newSettings;
    }

    private void LoadPacks()
    {
        SelectablePacks.Clear();
        var packs = TriviaPackManager.LoadAllPacks();
        bool isFirst = true;
        foreach (var p in packs)
        {
            SelectablePacks.Add(new SelectableTriviaPack(p, isChecked: isFirst));
            isFirst = false;
        }

        if (SelectablePacks.Count == 0)
        {
            // On a machine where the resolved TriviaData directory doesn't hold the expected
            // packs (e.g. a fresh install away from the dev box), the host would otherwise just
            // see an empty category list with no clue why. Leave a breadcrumb in the log folder.
            Lyracist.Shared.Globals.LogError("Lyracist",
                $"No trivia question packs found in '{TriviaPackManager.GetDefaultPacksDirectory()}'. Category list will be empty until a .json pack is placed there.",
                "LoadPacks");
        }
    }

    private void LoadScreens()
    {
        ScreenOptions.Clear();
        var screens = _displayService.GetScreens();
        for (int i = 0; i < screens.Count; i++)
        {
            ScreenOptions.Add($"Monitor {screens[i].Index}: {screens[i].DeviceName} ({(screens[i].IsPrimary ? "Primary" : "Secondary")})");
        }
        SelectedScreenIndex = screens.Count > 1 ? 1 : 0;
    }

    private void StartWebServer()
    {
        try
        {
            string ip = LocalNetworkHelper.GetLocalIPv4Address()?.ToString() ?? "127.0.0.1";
            _webServer = new TriviaWebServer(_engine, Settings.Port);
            _webServer.Start();
            PatronUrl = $"http://{ip}:{Settings.Port}/trivia";
            ServerStatusText = $"Online: {PatronUrl}";
        }
        catch (Exception ex)
        {
            ServerStatusText = $"Server Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StartGame()
    {
        try
        {
            List<TriviaRound> rounds = [];
            var checkedPacks = GetCheckedPacks();
            if (checkedPacks.Count > 0)
            {
                // Pool every checked pack's questions together and draw this game's set fresh -
                // see TriviaPackManager.BuildMixedQuestionSet for the double-draw-then-rescramble
                // algorithm. Called again on every unattended auto-restart too, so the question
                // set is different every game even with the exact same packs checked.
                var gameQuestions = TriviaPackManager.BuildMixedQuestionSet(checkedPacks, Settings.QuestionsPerGame);
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
                // Fallback default round
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

            _engine.StartGame(rounds, rounds[0].Title);
            ActiveRoundTitle = rounds[0].Title;
            _engine.StartCurrentQuestion();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error starting game: {ex.Message}", "Lyracist Trivia", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
    private void NextQuestion()
    {
        _engine.AdvanceToNextQuestion();
    }

    [RelayCommand]
    private void ResetGame()
    {
        // The web server holds a fixed reference to the engine passed at construction, so it must
        // be recreated too - otherwise phones keep talking to the disposed old engine while the
        // screen shows the new one, and the buzzers silently stop working.
        _webServer?.Dispose();
        _engine.Dispose();
        _engine = new TriviaGameEngine(Settings);
        _displayService.SetTriviaGameEngine(_engine);
        WireEngineEvents();
        StartWebServer();
        GameStateText = "Lobby";
        IsGameRunning = false;
        CurrentQuestionPrompt = "Game reset. Click 'Start Game' to begin.";
        OptionA = OptionB = OptionC = OptionD = "";
        CorrectAnswerIndex = -1;
    }

    [RelayCommand]
    private void RefreshPacks()
    {
        LoadPacks();
    }

    public void Dispose()
    {
        _webServer?.Dispose();
        _engine.Dispose();
        GC.SuppressFinalize(this);
    }
}
