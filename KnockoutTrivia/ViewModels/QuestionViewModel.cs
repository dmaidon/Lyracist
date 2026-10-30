// Edited on Oct 30, 2026 @ 09:20:00 -> Marshal OnQuestionChanged via Dispatcher and sync TotalQuestions on property change
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class QuestionViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;

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

    public QuestionViewModel(IGameStateService gameStateService)
    {
        _gameStateService = gameStateService;
        _gameStateService.QuestionChanged += OnQuestionChanged;
        _gameStateService.TimerTicked += (s, sec) =>
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => SecondsRemaining = sec);
            }
            else
            {
                SecondsRemaining = sec;
            }
        };

        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IGameStateService.IsAnswerRevealed))
                {
                    IsAnswerRevealed = _gameStateService.IsAnswerRevealed;
                }
                else if (e.PropertyName == nameof(IGameStateService.TotalQuestions))
                {
                    TotalQuestions = _gameStateService.TotalQuestions;
                }
            };
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
        if (q != null)
        {
            foreach (var opt in q.Options)
            {
                Options.Add(opt);
            }
            SecondsRemaining = _gameStateService.SecondsRemaining > 0 ? _gameStateService.SecondsRemaining : q.TimeLimitSeconds;
        }
        CurrentQuestionNumber = _gameStateService.CurrentQuestionIndex + 1;
        TotalQuestions = _gameStateService.TotalQuestions;
        IsAnswerRevealed = _gameStateService.IsAnswerRevealed;
    }

    [RelayCommand]
    private void NextQuestion()
    {
        _gameStateService.NextQuestion();
    }

    [RelayCommand]
    private void RevealAnswer()
    {
        _gameStateService.RevealAnswer();
        IsAnswerRevealed = true;
    }
}

