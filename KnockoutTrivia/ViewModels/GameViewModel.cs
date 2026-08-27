// Edited on Aug 27, 2026 @ 15:20:15 -> Added ShuffleQuestionsCommand for host control
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class GameViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;
    private readonly ITokenService _tokenService;
    private readonly IStreakService _streakService;

    [ObservableProperty]
    private string _quickPlayerName = string.Empty;

    public IGameStateService GameState => _gameStateService;
    public ObservableCollection<KnockoutPlayer> Players => _gameStateService.Players;

    public event EventHandler<KnockoutPlayer>? SuperStreakRequested;

    public GameViewModel(
        IGameStateService gameStateService,
        ITokenService tokenService,
        IStreakService streakService)
    {
        _gameStateService = gameStateService;
        _tokenService = tokenService;
        _streakService = streakService;

        _gameStateService.SuperStreakTriggered += (s, player) =>
        {
            SuperStreakRequested?.Invoke(this, player);
        };
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
        }
    }

    [RelayCommand]
    private void MarkIncorrect(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _gameStateService.RecordPlayerAnswer(player.Id, false);
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
}
