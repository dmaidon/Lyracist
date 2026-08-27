// Created on Aug 27, 2026 @ 14:35:30 -> BannerInfo model for dynamic and static 16:9 banner displays
using System;

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
    public string? WinnerName { get; set; }
    public int FinalScore { get; set; }
    public string AccentColorHex { get; set; } = "#10B981";
}
