// Created on Aug 29, 2026 @ 10:45:00 -> SimulatorViewModel for in-app DJ bot simulation, diagnostics, and testing panel
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class SimulatorViewModel : ObservableObject
{
    private readonly ISimulatorService _simulatorService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private int _botCountToSpawn = 10;

    [ObservableProperty]
    private int _accuracyRate = 75;

    [ObservableProperty]
    private bool _isAutoPlayEnabled;

    public ObservableCollection<string> ActivityLogs => _simulatorService.ActivityLogs;
    public int ActiveBotsCount => _simulatorService.BotCount;
    public int TotalPlayersCount => _gameStateService.Players.Count;
    public string CurrentPhaseText => _gameStateService.Phase.ToString();

    public SimulatorViewModel(ISimulatorService simulatorService, IGameStateService gameStateService)
    {
        _simulatorService = simulatorService;
        _gameStateService = gameStateService;

        _gameStateService.PhaseChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CurrentPhaseText));
            OnPropertyChanged(nameof(ActiveBotsCount));
            OnPropertyChanged(nameof(TotalPlayersCount));
        };

        _gameStateService.QuestionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CurrentPhaseText));
            OnPropertyChanged(nameof(ActiveBotsCount));
            OnPropertyChanged(nameof(TotalPlayersCount));
        };
    }

    partial void OnAccuracyRateChanged(int value)
    {
        _simulatorService.AccuracyRate = value;
    }

    partial void OnIsAutoPlayEnabledChanged(bool value)
    {
        _simulatorService.IsAutoPlayEnabled = value;
    }

    [RelayCommand]
    private void SpawnBots()
    {
        _simulatorService.SpawnBots(BotCountToSpawn);
        OnPropertyChanged(nameof(ActiveBotsCount));
        OnPropertyChanged(nameof(TotalPlayersCount));
    }

    [RelayCommand]
    private void ClearBots()
    {
        _simulatorService.ClearBots();
        OnPropertyChanged(nameof(ActiveBotsCount));
        OnPropertyChanged(nameof(TotalPlayersCount));
    }

    [RelayCommand]
    private async Task SimulateAnswersAsync()
    {
        await _simulatorService.SimulateQuestionAnswersAsync(AccuracyRate);
    }

    [RelayCommand]
    private void TriggerSuperStreak()
    {
        _simulatorService.TriggerSimulatedSuperStreak();
        OnPropertyChanged(nameof(ActiveBotsCount));
        OnPropertyChanged(nameof(TotalPlayersCount));
    }

    [RelayCommand]
    private void FastForward()
    {
        _simulatorService.FastForwardRound();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _simulatorService.ClearLogs();
    }
}
