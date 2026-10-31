// Edited on Oct 31, 2026 @ 10:46:00 -> Load static banners with frozen BitmapCacheOption.OnLoad and break winner ties by response speed
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Lyracist.Shared;

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
            .ThenBy(p => p.AverageResponseTimeMs)
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

        if (string.IsNullOrEmpty(SelectedStaticBannerPath) && StaticBanners.Count > 0)
        {
            SelectedStaticBannerPath = StaticBanners[0];
        }
    }

    [RelayCommand]
    public void DisplaySelectedStaticBanner()
    {
        if (!string.IsNullOrEmpty(SelectedStaticBannerPath) && File.Exists(SelectedStaticBannerPath))
        {
            BitmapSource? loadedImage = null;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(SelectedStaticBannerPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                loadedImage = bitmap;
            }
            catch (Exception ex)
            {
                Globals.LogError("KnockoutTrivia", "BannerViewModel.DisplaySelectedStaticBanner", ex);
            }

            CurrentBanner = new BannerInfo
            {
                Type = BannerType.CustomStatic,
                Title = "SPONSORED ANNOUNCEMENT",
                ImagePath = SelectedStaticBannerPath,
                ImageSource = loadedImage
            };
        }
    }
}
