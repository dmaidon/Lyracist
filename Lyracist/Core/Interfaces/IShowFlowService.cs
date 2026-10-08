// Edited on Oct 7, 2026 @ 19:45:00 -> Add RefreshPlaybackSettings method to IShowFlowService
namespace Lyracist.Core.Interfaces;

public interface IShowFlowService
{
    void SetBgmAudioDevice(string deviceId);
    void RefreshOutputSettings();
    void RefreshPlaybackSettings();
    bool IsOpeningPlaying { get; }
    bool IsFillInPlaying { get; }
    bool IsFillInDucked { get; }
    bool IsEndRotationPlaying { get; }
    bool IsOccasionPlaying { get; }

    /// <summary>Reloads all playlists from the database. Call after editing them.</summary>
    void RefreshPlaylists();

    /// <param name="startTrackPath">When provided, playback starts at this track instead of the next one in rotation.</param>
    void StartOpeningMusic(string? startTrackPath = null);
    void StopOpeningMusic();

    /// <param name="startTrackPath">When provided, playback starts at this track instead of the next one in rotation.</param>
    void PlayFillIn(string? startTrackPath = null);
    void StopFillIn();

    void DuckFillIn();
    void UnduckFillIn();

    /// <param name="startTrackPath">When provided, playback starts at this track instead of the next one in rotation.</param>
    void StartEndRotationMusic(string? startTrackPath = null);
    void StopEndRotationMusic();

    /// <summary>
    /// Plays a one-shot Special Occasion track: fill-in music pauses, the
    /// RotationWindow shows a banner, and when the track ends (or is stopped)
    /// the banner clears and fill-in resumes.
    /// </summary>
    void PlayOccasion(string occasionName, string filePath, double bassDb, double trebleDb, double preampDb);
    void StopOccasion();

    void SetOpeningVolume(double volume);
    void SetFillInVolume(double volume);
    void SetEndRotationVolume(double volume);

    /// <summary>Bass/treble/preamp gains in dB (-20..20), via LibVLC's native 10-band equalizer.</summary>
    void SetOpeningTone(double bassDb, double trebleDb, double preampDb);
    void SetFillInTone(double bassDb, double trebleDb, double preampDb);
    void SetEndRotationTone(double bassDb, double trebleDb, double preampDb);

    /// <summary>Immediately cuts fill-in/opening music when a karaoke track starts.</summary>
    void OnKaraokeTrackStarted();

    /// <summary>Suspends whatever BGM player is currently running so another window can play audio.</summary>
    void PauseBackgroundMusic();

    /// <summary>Resumes the BGM player that was running before <see cref="PauseBackgroundMusic"/>.</summary>
    void ResumeBackgroundMusic();

    event Action<int, bool>? AutoAdvanceCountdownTick;
    void CancelAutoAdvance();
    void TriggerAutoAdvanceNow();
}
