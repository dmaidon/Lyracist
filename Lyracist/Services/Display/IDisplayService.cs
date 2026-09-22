// Edited on Sep 22, 2026 @ 07:56:00 -> Add SetAutoRotateDurationSeconds to IDisplayService
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Services;

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
    void SetLastRound(bool isLastRound);
    void SetRotationViewMode(string mode);
    void SetAutoRotateProjectionViews(bool enabled);
    void SetAutoRotateDurationSeconds(int seconds);
    void SetProjectionRotationSchedule(System.Collections.Generic.List<ProjectionRotationEntry> schedule);
    void SetCrawlBannerText(string text);
    void SetShowEstimatedWaitTime(bool show);
    void UpdateDjBanner(string path);
    void UpdateSpecialEvent(string eventName);
    void HideDjBannerWindow();

    /// <summary>Re-pushes the currently active DJ banner path to the banner window, forcing a fresh image load (e.g. after the underlying file was regenerated in place).</summary>
    void RefreshActiveBanner();

    Task<bool> MoveRotationTo(DisplayTarget target, ChromecastDevice? device = null);
    Task StopRotationCasting();

    void UpdateWindowVisibilities();

    void SetTriviaGameEngine(TriviaGameEngine? engine);
    TriviaGameEngine? GetTriviaGameEngine();
}

