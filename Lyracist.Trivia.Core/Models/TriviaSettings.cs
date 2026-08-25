// Edited on Aug 25, 2026 @ 06:15:00 -> Add XML summary tags to TotalGamesToPlay documentation comment
using System;

namespace Lyracist.Trivia.Core.Models;

public class TriviaSettings
{
    public int Port { get; set; } = 8085;
    public string VenueName { get; set; } = "The Main Stage Lounge";
    public string HostName { get; set; } = "Trivia Master";
    public string WifiSsid { get; set; } = string.Empty;
    public string WifiPassword { get; set; } = string.Empty;
    public int PreGameCountdownMinutes { get; set; } = 5;
    public bool AutoStartAfterCountdown { get; set; } = true;
    public int QuestionsPerGame { get; set; } = 10;
    public int WarningCountdownSeconds { get; set; } = 3;
    public int DefaultQuestionSeconds { get; set; } = 15;
    public int AnswerEliminationIntervalSeconds { get; set; } = 5;
    public int PostRevealDelaySeconds { get; set; } = 5;
    public int RevealBufferSeconds { get; set; } = 5;
    public int BasePointsPerQuestion { get; set; } = 1000;
    public int WrongAnswerDeductionPoints { get; set; } = 0;
    public bool SpeedBonusEnabled { get; set; } = true;
    public int MaxSpeedBonus { get; set; } = 500;
    public double StreakBonusMultiplier { get; set; } = 0.1;

    // Tiered Option Value Scoring (100% / 70% / 40%)
    public bool TieredScoringEnabled { get; set; } = true;

    public int Points4OptionsPercent { get; set; } = 100;
    public int Points3OptionsPercent { get; set; } = 70;
    public int Points2OptionsPercent { get; set; } = 40;

    public bool SoundEffectsEnabled { get; set; } = true;
    public bool AutoAdvanceQuestions { get; set; } = true;
    public bool AutoStartNextGameEnabled { get; set; } = true;
    public int NextGameDelayMinutes { get; set; } = 3;

    /// <summary>
    /// Caps how many games auto-start plays before stopping instead of looping forever.
    /// 0 (or less) means unlimited - the pre-existing behavior. Only meaningful when
    /// AutoStartNextGameEnabled is true; see TriviaGameEngine.GamesPlayedCount/CompleteGame.
    /// </summary>
    public int TotalGamesToPlay { get; set; } = 0;

    public bool MarqueeEnabled { get; set; } = true;
    public double MarqueeSpeed { get; set; } = 50.0;
    public int DisplayMonitorIndex { get; set; } = 1;
    public string SelectedMonitorDevice { get; set; } = string.Empty;

    public const string DefaultInstructionBannerText = "Welcome to Live Pub Trivia Night at {venue}! Your GameMaster is {dj}.";

    public string InstructionBannerText { get; set; } = DefaultInstructionBannerText;

    /// <summary>
    /// Resolves {venue} and {dj} tokens in InstructionBannerText (case-insensitive) against the
    /// current venue/host names, for display on the pre-game lobby screen.
    /// </summary>
    public string GetResolvedInstructionBanner(string venueName, string hostName) =>
        ResolveInstructionBanner(InstructionBannerText, venueName, hostName);

    /// <summary>
    /// Resolves {venue} and {dj} tokens (case-insensitive) in a raw instruction banner template.
    /// Shared by every display ViewModel so the substitution logic isn't duplicated per app.
    /// </summary>
    public static string ResolveInstructionBanner(string? template, string? venueName, string? hostName)
    {
        string raw = string.IsNullOrWhiteSpace(template) ? DefaultInstructionBannerText : template.Trim();
        return raw
            .Replace("{venue}", venueName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{dj}", hostName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}