// Edited on Oct 31, 2026 @ 10:35:00 -> Add QuestionOptionDisplay model for option letters and live answer distribution
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KnockoutTrivia.Models;

public enum TriviaDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

public enum TriviaQuestionType
{
    MultipleChoice = 0,
    TrueFalse = 1,
    TextEntry = 2
}

public partial class QuestionOptionDisplay : ObservableObject
{
    public int Index { get; init; }
    public string Letter { get; init; } = "A";
    public string Text { get; init; } = string.Empty;
    public bool IsCorrect { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCorrectHighlight))]
    [NotifyPropertyChangedFor(nameof(ShowDistribution))]
    private bool _isRevealed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistributionBadge))]
    private int _voteCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistributionBadge))]
    private int _votePercentage;

    public bool ShowCorrectHighlight => IsRevealed && IsCorrect;
    public bool ShowDistribution => IsRevealed;
    public string DistributionBadge => $"{VotePercentage}% ({VoteCount})";
}

public class KnockoutQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public string Category { get; set; } = "General";
    public TriviaDifficulty Difficulty { get; set; } = TriviaDifficulty.Medium;
    public TriviaQuestionType QuestionType { get; set; } = TriviaQuestionType.MultipleChoice;
    public string Prompt { get; set; } = string.Empty;
    public List<string> Options { get; set; } = [];
    public int CorrectAnswerIndex { get; set; } = 0;
    public string Explanation { get; set; } = string.Empty;
    public string? AudioSnippetPath { get; set; }
    public int TimeLimitSeconds { get; set; } = 15;

    public string CorrectAnswerText => (Options.Count > CorrectAnswerIndex && CorrectAnswerIndex >= 0)
        ? Options[CorrectAnswerIndex]
        : string.Empty;
}
