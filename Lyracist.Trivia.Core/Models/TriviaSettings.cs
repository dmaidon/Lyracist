// Edited on Aug 18, 2026 @ 17:48:00 -> Added WrongAnswerDeductionPoints setting defaulting to 0
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
    public bool SoundEffectsEnabled { get; set; } = true;
    public bool AutoAdvanceQuestions { get; set; } = true;
    public bool AutoStartNextGameEnabled { get; set; } = true;
    public int NextGameDelayMinutes { get; set; } = 3;
    public bool MarqueeEnabled { get; set; } = true;
    public double MarqueeSpeed { get; set; } = 50.0;
    public int DisplayMonitorIndex { get; set; } = 1;
    public string SelectedMonitorDevice { get; set; } = string.Empty;
}
