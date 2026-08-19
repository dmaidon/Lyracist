// Created on Aug 19, 2026 @ 11:22:00 -> TriviaHelpTopic model for in-app help documentation
using System;

namespace Lyracist.Trivia.Core.Models;

public class TriviaHelpTopic
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "🎯";
    public string AccentColor { get; set; } = "#38BDF8";
    public string DescriptionHeader { get; set; } = string.Empty;
    public string DescriptionContent { get; set; } = string.Empty;
}
