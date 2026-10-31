// Edited on Oct 31, 2026 @ 10:42:00 -> Add OptionItems with A/B/C/D badges and live answer distribution on reveal, sync SecondsRemaining, and implement IDisposable
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

public partial class QuestionViewModel : ViewModelBase, IDisposable
{
    private readonly IGameStateService _gameStateService;
    private bool _disposed;

    [ObservableProperty]
    private KnockoutQuestion? _currentQuestion;

    [ObservableProperty]
    private int _currentQuestionNumber = 1;

    [ObservableProperty]
    private int _totalQuestions = 20;

    [ObservableProperty]
    private bool _isAnswerRevealed;

    [ObservableProperty]
    private int _secondsRemaining = 15;

    public ObservableCollection<string> Options { get; } = [];
    public ObservableCollection<QuestionOptionDisplay> OptionItems { get; } = [];

    public QuestionViewModel(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _gameStateService.QuestionChanged += OnQuestionChanged;
        _gameStateService.TimerTicked += OnTimerTicked;
        _gameStateService.AnswersEvaluated += OnAnswersEvaluated;

        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged += OnGameStatePropertyChanged;
        }

        // Initial question preview
        if (_gameStateService.CurrentQuestion != null)
        {
            UpdateFromQuestion(_gameStateService.CurrentQuestion);
        }
        else
        {
            CurrentQuestion = new KnockoutQuestion
            {
                Prompt = "What band released the iconic 1977 album 'Rumours'?",
                Category = "Rock & Roll",
                Difficulty = TriviaDifficulty.Medium,
                Options = ["Fleetwood Mac", "The Eagles", "Led Zeppelin", "Heart"],
                CorrectAnswerIndex = 0,
                Explanation = "'Rumours' became one of the best-selling albums of all time with over 40 million copies sold."
            };
            UpdateFromQuestion(CurrentQuestion);
        }
    }

    private void OnTimerTicked(object? sender, int sec)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => SecondsRemaining = sec);
        }
        else
        {
            SecondsRemaining = sec;
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
            IsAnswerRevealed = _gameStateService.IsAnswerRevealed;
            UpdateOptionDistribution();
        }
        else if (e.PropertyName == nameof(IGameStateService.TotalQuestions))
        {
            TotalQuestions = _gameStateService.TotalQuestions;
        }
        else if (e.PropertyName == nameof(IGameStateService.SecondsRemaining))
        {
            SecondsRemaining = _gameStateService.SecondsRemaining;
        }
        else if (e.PropertyName == nameof(IGameStateService.CurrentQuestionNumber))
        {
            CurrentQuestionNumber = _gameStateService.CurrentQuestionNumber;
        }
    }

    private void OnQuestionChanged(object? sender, KnockoutQuestion? q)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => UpdateFromQuestion(q));
        }
        else
        {
            UpdateFromQuestion(q);
        }
    }

    private void UpdateFromQuestion(KnockoutQuestion? q)
    {
        CurrentQuestion = q;
        Options.Clear();
        OptionItems.Clear();
        if (q != null)
        {
            for (int i = 0; i < q.Options.Count; i++)
            {
                string opt = q.Options[i];
                Options.Add(opt);
                OptionItems.Add(new QuestionOptionDisplay
                {
                    Index = i,
                    Letter = ((char)('A' + i)).ToString(),
                    Text = opt,
                    IsCorrect = i == q.CorrectAnswerIndex,
                    IsRevealed = _gameStateService.IsAnswerRevealed
                });
            }
            SecondsRemaining = _gameStateService.SecondsRemaining > 0 ? _gameStateService.SecondsRemaining : q.TimeLimitSeconds;
        }
        CurrentQuestionNumber = _gameStateService.CurrentQuestionNumber;
        TotalQuestions = _gameStateService.TotalQuestions;
        IsAnswerRevealed = _gameStateService.IsAnswerRevealed;
        UpdateOptionDistribution();
    }

    private void UpdateOptionDistribution()
    {
        bool revealed = _gameStateService.IsAnswerRevealed;
        var snapshot = _gameStateService.GetPlayersSnapshot();
        int totalAnswered = snapshot.Count(p => p.HasAnsweredCurrentQuestion && p.LastAnswerIndex >= 0);

        foreach (var item in OptionItems)
        {
            int count = snapshot.Count(p => p.HasAnsweredCurrentQuestion && p.LastAnswerIndex == item.Index);
            item.VoteCount = count;
            item.VotePercentage = totalAnswered > 0 ? (int)Math.Round(count * 100.0 / totalAnswered) : 0;
            item.IsRevealed = revealed;
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
        IsAnswerRevealed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameStateService.QuestionChanged -= OnQuestionChanged;
        _gameStateService.TimerTicked -= OnTimerTicked;
        _gameStateService.AnswersEvaluated -= OnAnswersEvaluated;
        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged -= OnGameStatePropertyChanged;
        }
    }
}
