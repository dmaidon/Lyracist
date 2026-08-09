// Edited on Aug 8, 2026 @ 19:18:20 -> Add UpdateSpecialEvent to IDisplayService
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Shared;

namespace Lyracist.Services.Display;

public interface IDisplayService
{
    /// <summary>Fires when the singer rotation queue transitions from having singers to being empty.</summary>
    event Action? RotationCompleted;

    /// <summary>Fires when a singer is added back to a previously-empty rotation queue.</summary>
    event Action? RotationResumed;

    /// <summary>Fires when the screen/monitor assignments for displays change.</summary>
    event Action? ScreenAssignmentsChanged;

    bool IsLyricsActive { get; set; }
    bool IsRotationActive { get; set; }
    bool IsDjBannerActive { get; set; }

    IReadOnlyList<ScreenInfo> GetScreens();

    void ShowRotationWindow();
    void ShowLyricsWindow();
    void ShowDjBannerWindow();

    void MoveRotationToScreen(int? screenIndex);
    void MoveLyricsToScreen(int? screenIndex);
    void MoveDjBannerToScreen(int? screenIndex);

    void FullscreenRotation();
    void FullscreenLyrics();

    /// <summary>Reapplies the last saved monitor assignments and mirror state.</summary>
    void RestoreAssignments();
    DisplayPreferences GetPreferences();

    void UpdateRotation(System.Collections.Generic.List<Lyracist.Models.Singer> singers);
    void HighlightSinger(Lyracist.Models.Singer singer);
    void UpdateLyricsFrame(System.Windows.Media.ImageSource frame);
    void SetLyricsMirror(bool mirrored);
    void SetLyricsFallbackText(string text);

    /// <summary>Flashes a temporary banner over the lyrics display (Scaryoke results, shout-outs).</summary>
    void ShowLyricsOverlay(string text, int seconds = 8);
    void SetRotationAnnouncement(string message, bool visible);
    void SetRotationViewMode(string mode);
    void SetCrawlBannerText(string text);
    void UpdateDjBanner(string path);
    void UpdateSpecialEvent(string eventName);
    void HideDjBannerWindow();

    Task<bool> MoveRotationTo(DisplayTarget target, ChromecastDevice? device = null);
    Task StopRotationCasting();

    void UpdateWindowVisibilities();
}

