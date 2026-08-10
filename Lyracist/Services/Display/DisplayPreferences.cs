// Edited on Aug 10, 2026 @ 12:59:00 -> Default projection active flags to false on app load
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
    public bool IsLyricsActive { get; set; } = false;
    public bool IsRotationActive { get; set; } = false;
    public bool IsDjBannerActive { get; set; } = false;
}

