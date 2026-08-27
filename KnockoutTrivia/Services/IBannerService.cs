// Created on Aug 27, 2026 @ 14:36:00 -> IBannerService and BannerService for dynamic and static 16:9 banner generation
using System;
using System.Collections.Generic;
using System.IO;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface IBannerService
{
    BannerInfo CreateIntroBanner();
    BannerInfo CreateIntermissionBanner(string? customMessage = null);
    BannerInfo CreateWinnerBanner(KnockoutPlayer winner);
    BannerInfo CreateEliminationBanner(KnockoutPlayer player);
    BannerInfo CreateSuperStreakBanner(KnockoutPlayer player);
    List<string> GetStaticBanners();
}

public class BannerService : IBannerService
{
    private static readonly string BannersDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KoTrivia", "Banners");

    public BannerService()
    {
        Directory.CreateDirectory(BannersDir);
    }

    public BannerInfo CreateIntroBanner()
    {
        return new BannerInfo
        {
            Type = BannerType.Intro,
            Title = "KNOCKOUT TRIVIA",
            Subtitle = "The Ultimate Bar-Friendly Trivia Showdown",
            DetailText = "Shields Up • Watch Your Strikes • Avoid the Wheel!",
            AccentColorHex = "#10B981"
        };
    }

    public BannerInfo CreateIntermissionBanner(string? customMessage = null)
    {
        return new BannerInfo
        {
            Type = BannerType.Intermission,
            Title = "ROUND INTERMISSION",
            Subtitle = customMessage ?? "Grab a drink & check the leaderboard!",
            DetailText = "Trivia returns in just a moment...",
            AccentColorHex = "#3B82F6"
        };
    }

    public BannerInfo CreateWinnerBanner(KnockoutPlayer winner)
    {
        return new BannerInfo
        {
            Type = BannerType.Winner,
            Title = "CHAMPION KNOCKOUT!",
            Subtitle = $"{winner.Name} WINS THE GAME!",
            WinnerName = winner.Name,
            FinalScore = winner.Score,
            DetailText = $"Final Score: {winner.Score} Points",
            AccentColorHex = "#F59E0B"
        };
    }

    public BannerInfo CreateEliminationBanner(KnockoutPlayer player)
    {
        return new BannerInfo
        {
            Type = BannerType.Elimination,
            Title = "KNOCKED OUT!",
            Subtitle = $"{player.Name} has received 3 strikes!",
            DetailText = "Out of the round, but can still cheer on the survivors!",
            AccentColorHex = "#EF4444"
        };
    }

    public BannerInfo CreateSuperStreakBanner(KnockoutPlayer player)
    {
        return new BannerInfo
        {
            Type = BannerType.SuperStreak,
            Title = "⚡ SUPER STREAK UNLOCKED! ⚡",
            Subtitle = $"{player.Name} has hit the Super Streak milestone!",
            DetailText = "Prepare to spin the wheel and steal an opponent's shield!",
            AccentColorHex = "#8B5CF6"
        };
    }

    public List<string> GetStaticBanners()
    {
        if (!Directory.Exists(BannersDir)) return [];
        return [.. Directory.GetFiles(BannersDir, "*.*")
            .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))];
    }
}
