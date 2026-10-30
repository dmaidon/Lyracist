// Edited on Oct 30, 2026 @ 09:20:00 -> Select top surviving scorer in ShowWinner and top streaker in ShowSuperStreak
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;

namespace KnockoutTrivia.ViewModels;

public partial class BannerViewModel : ViewModelBase
{
    private readonly IBannerService _bannerService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private BannerInfo _currentBanner;

    [ObservableProperty]
    private string? _selectedStaticBannerPath;

    public ObservableCollection<string> StaticBanners { get; } = [];

    public BannerViewModel(IBannerService bannerService, IGameStateService gameStateService)
    {
        _bannerService = bannerService;
        _gameStateService = gameStateService;

        _currentBanner = _bannerService.CreateIntroBanner();
        RefreshStaticBanners();
    }

    [RelayCommand]
    public void ShowIntro()
    {
        CurrentBanner = _bannerService.CreateIntroBanner();
    }

    [RelayCommand]
    public void ShowIntermission()
    {
        CurrentBanner = _bannerService.CreateIntermissionBanner();
    }

    [RelayCommand]
    public void ShowWinner()
    {
        var winner = _gameStateService.Players
            .OrderByDescending(p => !p.IsEliminated)
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.StrikeCount)
            .FirstOrDefault() ?? new KnockoutPlayer { Name = "Champion", Score = 150 };
        CurrentBanner = _bannerService.CreateWinnerBanner(winner);
    }

    [RelayCommand]
    public void ShowSuperStreak()
    {
        var streaker = _gameStateService.Players
            .OrderByDescending(p => p.StreakCount)
            .ThenByDescending(p => p.Score)
            .FirstOrDefault() ?? new KnockoutPlayer { Name = "Trivia Master" };
        CurrentBanner = _bannerService.CreateSuperStreakBanner(streaker);
    }

    [RelayCommand]
    public void RefreshStaticBanners()
    {
        StaticBanners.Clear();
        foreach (var b in _bannerService.GetStaticBanners())
        {
            StaticBanners.Add(b);
        }
    }

    [RelayCommand]
    public void DisplaySelectedStaticBanner()
    {
        if (!string.IsNullOrEmpty(SelectedStaticBannerPath))
        {
            CurrentBanner = new BannerInfo
            {
                Type = BannerType.CustomStatic,
                Title = "SPONSORED ANNOUNCEMENT",
                ImagePath = SelectedStaticBannerPath
            };
        }
    }
}
