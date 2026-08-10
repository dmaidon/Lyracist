// Edited on Aug 10, 2026 @ 14:18:00 -> Default SelectedSpecialEvent to "None"
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
    public string SelectedSpecialEvent { get; set; } = "None";
    public DisplayTarget RotationTarget { get; set; } = DisplayTarget.Monitor;
    public bool IsLyricsActive { get; set; } = true;
    public bool IsRotationActive { get; set; } = true;
    public bool IsDjBannerActive { get; set; } = true;
}

