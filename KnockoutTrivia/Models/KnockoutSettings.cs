// Edited on Aug 27, 2026 @ 15:07:00 -> Added SelectedSourcePaths for multi-database and pack selection
using System;
using System.Collections.Generic;

namespace KnockoutTrivia.Models;

public enum GameAdvanceMode
{
    Manual = 0,
    Automatic = 1
}

public class KnockoutSettings
{
    // DJ Panel & Game Rules
    public int PointsPerCorrectAnswer { get; set; } = 10;
    public int NumberOfQuestionsPerGame { get; set; } = 20;
    public bool UnlimitedQuestions { get; set; } = false;
    public GameAdvanceMode GameMode { get; set; } = GameAdvanceMode.Manual;
    public int AutoAdvanceDelaySeconds { get; set; } = 5;

    // Token & Streak Settings
    public int MaxTokens { get; set; } = 3;
    public int StreakRequirement { get; set; } = 5;

    // Super Streak Settings
    public bool SuperStreakEnabled { get; set; } = true;
    public int SuperStreakThreshold { get; set; } = 20;
    public int SuperStreakTokensRemovedPerHit { get; set; } = 1;

    // Database & Question Pack Sources
    public List<string> SelectedSourcePaths { get; set; } = [];

    // Display Settings
    public int SelectedGameMonitorIndex { get; set; } = 0;
    public int SelectedBannerMonitorIndex { get; set; } = 0;
    public bool DualMonitorEnabled { get; set; } = false;

    // Audio / Visual Settings
    public bool EnableSoundEffects { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
}
