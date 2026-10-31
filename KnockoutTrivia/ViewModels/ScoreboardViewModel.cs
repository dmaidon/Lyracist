// Edited on Oct 31, 2026 @ 10:48:00 -> Add RevivePlayerCommand and route ResetStrikes through GameStateService
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class ScoreboardViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;
    private readonly ITokenService _tokenService;

    [ObservableProperty]
    private string _newPlayerName = string.Empty;

    [ObservableProperty]
    private KnockoutPlayer? _selectedPlayer;

    public ObservableCollection<KnockoutPlayer> Players => _gameStateService.Players;

    public ScoreboardViewModel(IGameStateService gameStateService, ITokenService tokenService)
    {
        _gameStateService = gameStateService;
        _tokenService = tokenService;
    }

    [RelayCommand]
    private void AddPlayer()
    {
        if (!string.IsNullOrWhiteSpace(NewPlayerName))
        {
            _gameStateService.AddPlayer(NewPlayerName);
            NewPlayerName = string.Empty;
        }
    }

    [RelayCommand]
    private void RemovePlayer(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _gameStateService.RemovePlayer(player.Id);
        }
    }

    [RelayCommand]
    private void AwardToken(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _tokenService.AwardToken(player, _gameStateService.Settings.MaxTokens);
        }
    }

    [RelayCommand]
    private void DeductToken(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _tokenService.DeductToken(player);
        }
    }

    [RelayCommand]
    private void AddStrike(KnockoutPlayer? player)
    {
        if (player != null && player.StrikeCount < 3)
        {
            _gameStateService.AddPlayerStrike(player);
        }
    }

    [RelayCommand]
    private void ResetStrikes(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _gameStateService.ResetPlayerStrikes(player);
        }
    }

    [RelayCommand]
    private void RevivePlayer(KnockoutPlayer? player)
    {
        if (player != null)
        {
            _gameStateService.RevivePlayer(player);
        }
    }
}
