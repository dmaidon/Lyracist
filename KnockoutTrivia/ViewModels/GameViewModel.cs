// Edited on Oct 31, 2026 @ 10:42:00 -> Add CurrentOptionItems with A/B/C/D badges and answer distribution, and implement IDisposable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class GameViewModel : ViewModelBase, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private readonly ITokenService _tokenService;
    private readonly IStreakService _streakService;
    private bool _disposed;

    [ObservableProperty]
    private string _quickPlayerName = string.Empty;

    public IGameStateService GameState => _gameStateService;
    public ObservableCollection<KnockoutPlayer> Players => _gameStateService.Players;
    public ObservableCollection<QuestionOptionDisplay> CurrentOptionItems { get; } = [];

    public event EventHandler<KnockoutPlayer>? SuperStreakRequested;

    public GameViewModel(
        IGameStateService gameStateService,
        ITokenService tokenService,
        IStreakService streakService)
    {
        _gameStateService = gameStateService;
        _tokenService = tokenService;
        _streakService = streakService;

        _gameStateService.SuperStreakTriggered += OnSuperStreakTriggered;
        _gameStateService.QuestionChanged += OnQuestionChanged;
        _gameStateService.AnswersEvaluated += OnAnswersEvaluated;
        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged += OnGameStatePropertyChanged;
        }

        RebuildOptionItems(_gameStateService.CurrentQuestion);
    }

    private void OnSuperStreakTriggered(object? sender, KnockoutPlayer player)
    {
        SuperStreakRequested?.Invoke(this, player);
    }

    private void OnQuestionChanged(object? sender, KnockoutQuestion? q)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => RebuildOptionItems(q));
        }
        else
        {
            RebuildOptionItems(q);
        }
    }

    private void OnAnswersEvaluated(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(UpdateOptionDistribution);
        }
        else
        {
            UpdateOptionDistribution();
        }
    }

    private void OnGameStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGameStateService.IsAnswerRevealed))
        {
            UpdateOptionDistribution();
        }
    }

    private void RebuildOptionItems(KnockoutQuestion? q)
    {
        CurrentOptionItems.Clear();
        if (q == null) return;

        for (int i = 0; i < q.Options.Count; i++)
        {
            CurrentOptionItems.Add(new QuestionOptionDisplay
            {
                Index = i,
                Letter = ((char)('A' + i)).ToString(),
                Text = q.Options[i],
                IsCorrect = i == q.CorrectAnswerIndex,
                IsRevealed = _gameStateService.IsAnswerRevealed
            });
        }
        UpdateOptionDistribution();
    }

    private void UpdateOptionDistribution()
    {
        bool revealed = _gameStateService.IsAnswerRevealed;
        var snapshot = _gameStateService.GetPlayersSnapshot();
        int totalAnswered = snapshot.Count(p => p.HasAnsweredCurrentQuestion && p.LastAnswerIndex >= 0);

        foreach (var item in CurrentOptionItems)
        {
            int count = snapshot.Count(p => p.HasAnsweredCurrentQuestion && p.LastAnswerIndex == item.Index);
            item.VoteCount = count;
            item.VotePercentage = totalAnswered > 0 ? (int)Math.Round(count * 100.0 / totalAnswered) : 0;
            item.IsRevealed = revealed;
        }
    }

    [RelayCommand]
    private void StartGame()
    {
        _gameStateService.StartGame();
    }

    [RelayCommand]
    private void ToggleTimerPause()
    {
        if (_gameStateService.IsTimerRunning)
        {
            _gameStateService.PauseTimer();
        }
        else
        {
            _gameStateService.ResumeTimer();
        }
    }

    [RelayCommand]
    private void NextQuestion()
    {
        if (!_gameStateService.IsGameActive && _gameStateService.Phase == GameStatePhase.Lobby)
        {
            _gameStateService.StartGame();
            return;
        }

        _gameStateService.NextQuestion();
    }

    [RelayCommand]
    private void RevealAnswer()
    {
        _gameStateService.RevealAnswer();
    }

    [RelayCommand]
    private void ShuffleQuestions()
    {
        _gameStateService.ShuffleQuestions();
    }

    [RelayCommand]
    private void MarkCorrect(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _gameStateService.RecordPlayerAnswer(player.Id, true);
            UpdateOptionDistribution();
        }
    }

    [RelayCommand]
    private void MarkIncorrect(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _gameStateService.RecordPlayerAnswer(player.Id, false);
            UpdateOptionDistribution();
        }
    }

    [RelayCommand]
    private void AddPlayer()
    {
        if (!string.IsNullOrWhiteSpace(QuickPlayerName))
        {
            _gameStateService.AddPlayer(QuickPlayerName);
            QuickPlayerName = string.Empty;
        }
    }

    [RelayCommand]
    private void ResetGame()
    {
        _gameStateService.ResetGame();
    }

    [RelayCommand]
    private void ClearAllPlayers()
    {
        _gameStateService.ClearAllPlayers();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameStateService.SuperStreakTriggered -= OnSuperStreakTriggered;
        _gameStateService.QuestionChanged -= OnQuestionChanged;
        _gameStateService.AnswersEvaluated -= OnAnswersEvaluated;
        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged -= OnGameStatePropertyChanged;
        }
    }
}
