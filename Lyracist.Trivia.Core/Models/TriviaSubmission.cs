// Created on Aug 17, 2026 @ 13:00:30 -> TriviaSubmission answer payload
using System;

namespace Lyracist.Trivia.Core.Models;

public class TriviaSubmission
{
    public string PlayerId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public int SelectedOptionIndex { get; set; } = -1;
    public double ResponseTimeMs { get; set; } = 0;
    public DateTime SubmittedAt { get; set; } = DateTime.Now;
}
