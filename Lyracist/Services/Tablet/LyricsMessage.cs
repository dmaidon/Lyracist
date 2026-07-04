using System;

namespace Lyracist.Services.Tablet;

public class LyricsMessage
{
    public string Title { get; set; } = string.Empty;
    public string CurrentLine { get; set; } = string.Empty;
    public string NextLine { get; set; } = string.Empty;
    public TimeSpan Position { get; set; }
}
