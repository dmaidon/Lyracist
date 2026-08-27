// Created on Aug 27, 2026 @ 14:36:35 -> QuestionViewModel for big screen bar-friendly trivia display
using System.Collections.ObjectModel;
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

        // Fallback sample question for immediate preview
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
        UpdateFromQuestion(q);
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
            SecondsRemaining = q.TimeLimitSeconds;
        }
        CurrentQuestionNumber = _gameStateService.CurrentQuestionIndex + 1;
        TotalQuestions = _gameStateService.TotalQuestions > 0 ? _gameStateService.TotalQuestions : 20;
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
