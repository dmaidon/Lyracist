// Created on Aug 17, 2026 @ 15:48:30 -> Game Master ViewModel for Lyracist interactive Trivia host console
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
    private string _selectedPack = "";

    [ObservableProperty]
    private int _selectedScreenIndex = 0;

    [ObservableProperty]
    private string _serverStatusText = "Server Offline";

    [ObservableProperty]
    private string _patronUrl = "http://localhost:8085/trivia";

    public ObservableCollection<string> AvailablePacks { get; } = [];
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
                TotalSeconds = q.TimeLimitSeconds > 0 ? q.TimeLimitSeconds : Settings.DefaultQuestionSeconds;
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
    }

    public void ApplySettings(TriviaSettings newSettings)
    {
        Settings = newSettings;
        _engine.Settings = newSettings;
    }

    private void LoadPacks()
    {
        AvailablePacks.Clear();
        var packs = TriviaPackManager.LoadAllPacks();
        foreach (var p in packs)
        {
            AvailablePacks.Add(p.Title);
        }

        if (AvailablePacks.Count > 0)
        {
            SelectedPack = AvailablePacks[0];
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
            var allPacks = TriviaPackManager.LoadAllPacks();
            var matchedPack = allPacks.FirstOrDefault(p => p.Title.Equals(SelectedPack, StringComparison.OrdinalIgnoreCase));
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

            _engine.StartGame(rounds, SelectedPack);
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
        _engine.Dispose();
        _engine = new TriviaGameEngine(Settings);
        _displayService.SetTriviaGameEngine(_engine);
        WireEngineEvents();
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
