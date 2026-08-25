// Edited on Aug 20, 2026 @ 06:19:00 -> Added VisibleOptionsAtSubmission and RemainingSecondsAtSubmission for tiered scoring calculation
using System;

namespace Lyracist.Trivia.Core.Models;

public class TriviaPlayer
{
    public string PlayerId { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string Name { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int TotalScore { get; set; } = 0;
    public int CurrentStreak { get; set; } = 0;
    public int MaxStreak { get; set; } = 0;
    public int TotalCorrect { get; set; } = 0;
    public int TotalAnswered { get; set; } = 0;
    public int LastAnswerIndex { get; set; } = -1;
    public double LastResponseTimeMs { get; set; } = 0;
    public bool HasAnsweredCurrentQuestion { get; set; } = false;
    public int LastPointsEarned { get; set; } = 0;
    public int VisibleOptionsAtSubmission { get; set; } = 4;
    public int RemainingSecondsAtSubmission { get; set; } = 0;

    /// CurrentStreak/MaxStreak as they stood immediately before this question's answer was
    /// scored - lets TriviaGameEngine.VoidCurrentQuestion restore them exactly instead of
    /// leaving streak counters inflated by a voided question. See ScoreAnswer.
    public int StreakBeforeAnswer { get; set; } = 0;
    public int MaxStreakBeforeAnswer { get; set; } = 0;
    public bool IsConnected { get; set; } = true;
    public DateTime ConnectedAt { get; set; } = DateTime.Now;
    public DateTime LastSeenAt { get; set; } = DateTime.Now;

    public string DisplayName => !string.IsNullOrWhiteSpace(TeamName) && TeamName != Name
        ? $"{Name} ({TeamName})"
        : Name;
}
