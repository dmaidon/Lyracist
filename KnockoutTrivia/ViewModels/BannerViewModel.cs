// Created on Aug 27, 2026 @ 14:36:45 -> BannerViewModel for dynamic and static 16:9 bar presentations
using System.Collections.ObjectModel;
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
        var sampleWinner = _gameStateService.Players.Count > 0 
            ? _gameStateService.Players[0] 
            : new KnockoutPlayer { Name = "Champion", Score = 150 };
        CurrentBanner = _bannerService.CreateWinnerBanner(sampleWinner);
    }

    [RelayCommand]
    public void ShowSuperStreak()
    {
        var samplePlayer = _gameStateService.Players.Count > 0 
            ? _gameStateService.Players[0] 
            : new KnockoutPlayer { Name = "Trivia Master" };
        CurrentBanner = _bannerService.CreateSuperStreakBanner(samplePlayer);
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
