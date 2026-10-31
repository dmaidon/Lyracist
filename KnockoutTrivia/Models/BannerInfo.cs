// Edited on Oct 31, 2026 @ 10:35:00 -> Add frozen ImageSource property to avoid file locks and unbounded URI image caching
using System;
using System.Windows.Media.Imaging;

namespace KnockoutTrivia.Models;

public enum BannerType
{
    Intro,
    Intermission,
    Winner,
    Elimination,
    SuperStreak,
    CustomStatic,
    Sponsor
}

public class BannerInfo
{
    public BannerType Type { get; set; } = BannerType.Intro;
    public string Title { get; set; } = "KNOCKOUT TRIVIA";
    public string Subtitle { get; set; } = "Get Ready for the Challenge!";
    public string DetailText { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public BitmapSource? ImageSource { get; set; }
    public string? WinnerName { get; set; }
    public int FinalScore { get; set; }
    public string AccentColorHex { get; set; } = "#10B981";
}
