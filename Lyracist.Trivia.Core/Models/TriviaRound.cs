// Created on Aug 17, 2026 @ 13:00:00 -> TriviaRound data model
using System;
using System.Collections.Generic;

namespace Lyracist.Trivia.Core.Models;

public class TriviaRound
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    public int RoundNumber { get; set; } = 1;
    public string Title { get; set; } = "Round 1";
    public string Category { get; set; } = "General Knowledge";
    public double PointMultiplier { get; set; } = 1.0;
    public List<TriviaQuestion> Questions { get; set; } = [];

    public int TotalQuestions => Questions.Count;
}
