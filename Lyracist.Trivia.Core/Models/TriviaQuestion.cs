// Created on Aug 17, 2026 @ 12:59:45 -> TriviaQuestion data model
using System;
using System.Collections.Generic;

namespace Lyracist.Trivia.Core.Models;

public class TriviaQuestion
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
