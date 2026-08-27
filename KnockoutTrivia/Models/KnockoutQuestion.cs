// Created on Aug 27, 2026 @ 14:35:10 -> KnockoutQuestion data model compatible with Lyracist.Trivia database format
using System;
using System.Collections.Generic;

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
