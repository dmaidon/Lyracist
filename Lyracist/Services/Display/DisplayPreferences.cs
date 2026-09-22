// Edited on Sep 22, 2026 @ 07:55:00 -> Add AutoRotateDurationSeconds to DisplayPreferences
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

    /// <summary>When true, the rotation display automatically cycles through the enabled entries in
    /// <see cref="ProjectionRotationSchedule"/> instead of staying on one fixed screen.</summary>
    public bool AutoRotateProjectionViews { get; set; }

    /// <summary>How many seconds each randomly chosen screen stays up during automatic screen rotation.</summary>
    public int AutoRotateDurationSeconds { get; set; } = 180;

    /// <summary>Which projection views participate in the automatic rotation.
    /// One entry per known view; entries the DJ hasn't opted into stay disabled.</summary>
    public List<ProjectionRotationEntry> ProjectionRotationSchedule { get; set; } = [];
}

