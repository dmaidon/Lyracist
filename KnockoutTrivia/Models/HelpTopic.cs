// Created on Aug 27, 2026 @ 15:07:05 -> HelpTopic model for DJ game guide and rules reference
namespace KnockoutTrivia.Models;

public class HelpTopic
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "Help24";
    public string AccentColor { get; set; } = "#10B981";
    public string DescriptionHeader { get; set; } = string.Empty;
    public string DescriptionContent { get; set; } = string.Empty;
}
