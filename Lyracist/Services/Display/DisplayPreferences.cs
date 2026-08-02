// Edited on Aug 1, 2026 @ 12:09:00 -> Add RotationTarget property to DisplayPreferences
using Lyracist.Shared;

namespace Lyracist.Services.Display;

public class DisplayPreferences
{
    public int? RotationScreenIndex { get; set; }
    public int? LyricsScreenIndex { get; set; }
    public bool IsLyricsMirrored { get; set; }
    public string RotationViewMode { get; set; } = "Normal List";
    public int? DjBannerScreenIndex { get; set; }
    public string SelectedDjBannerPath { get; set; } = string.Empty;
    public DisplayTarget RotationTarget { get; set; } = DisplayTarget.Monitor;
}

