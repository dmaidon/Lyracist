// Created on Aug 27, 2026 @ 14:36:40 -> WheelViewModel for Super Streak Scaryoke-style target wheel interaction
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class WheelViewModel : ViewModelBase
{
    private readonly IWheelService _wheelService;
    private readonly IGameStateService _gameStateService;
    private readonly ITokenService _tokenService;

    [ObservableProperty]
    private KnockoutPlayer? _superStreakPlayer;

    [ObservableProperty]
    private WheelSegment? _selectedSegment;

    [ObservableProperty]
    private double _wheelAngle;

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private string _resultText = "READY TO SPIN";

    [ObservableProperty]
    private string _statusHeader = "SUPER STREAK CHALLENGE";

    public ObservableCollection<WheelSegment> Segments { get; } = [];

    public WheelViewModel(
        IWheelService wheelService,
        IGameStateService gameStateService,
        ITokenService tokenService)
    {
        _wheelService = wheelService;
        _gameStateService = gameStateService;
        _tokenService = tokenService;

        RefreshSegments();
    }

    public void SetSuperStreakPlayer(KnockoutPlayer player)
    {
        SuperStreakPlayer = player;
        StatusHeader = $"⚡ {player.Name.ToUpperInvariant()} HIT SUPER STREAK! ⚡";
        RefreshSegments();
    }

    [RelayCommand]
    public void RefreshSegments()
    {
        var dummyPlayer = SuperStreakPlayer ?? new KnockoutPlayer { Name = "Current Star" };
        var list = _wheelService.BuildWheelSegments(_gameStateService.Players, dummyPlayer);

        Segments.Clear();
        foreach (var seg in list)
        {
            Segments.Add(seg);
        }

        ResultText = Segments.Count > 0 ? "SPIN THE WHEEL TO TARGET AN OPPONENT!" : "NO OPPONENT TOKENS AVAILABLE";
    }

    [RelayCommand]
    public async Task SpinWheelAsync()
    {
        if (IsSpinning || Segments.Count == 0) return;

        IsSpinning = true;
        ResultText = "SPINNING...";

        var target = _wheelService.SelectRandomTarget(Segments);
        SelectedSegment = target;

        // Perform multi-rotation spin math (e.g. 5 full turns + target slice offset)
        double targetMidAngle = target?.MidAngle ?? 0;
        // The pointer is at 12 o'clock (270 deg or top), so rotate canvas to match pointer
        double finalAngle = 360 * 5 + (360 - targetMidAngle);

        // Animate angle in ViewModel steps
        double current = WheelAngle % 360;
        double delta = finalAngle - current;
        int steps = 60;
        for (int i = 1; i <= steps; i++)
        {
            double progress = (double)i / steps;
            // Ease out cubic
            double ease = 1 - Math.Pow(1 - progress, 3);
            WheelAngle = current + (delta * ease);
            await Task.Delay(25);
        }

        WheelAngle = finalAngle % 360;
        IsSpinning = false;

        if (target?.Player != null)
        {
            ResultText = $"TARGET HIT: {target.Player.Name}! Shield Removed!";
            _tokenService.DeductToken(target.Player);
        }
        else
        {
            ResultText = "BONUS REWARD: +50 Bonus Points Awarded!";
            if (SuperStreakPlayer != null)
            {
                SuperStreakPlayer.Score += 50;
            }
        }
    }
}
