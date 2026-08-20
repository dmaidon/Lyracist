// Created on Aug 20, 2026 @ 09:51:30 -> Add AutoAdvanceState enum for karaoke hosting auto-advance state machine
namespace Lyracist.Models;

/// <summary>
/// Defines the lifecycle states for the karaoke hosting Auto-Advance system.
/// </summary>
public enum AutoAdvanceState
{
    /// <summary>
    /// System is idle (no active countdown or song preparation in progress).
    /// </summary>
    Idle,

    /// <summary>
    /// Song has ended and the grace period countdown is active with fill-in music playing.
    /// </summary>
    GracePeriod,

    /// <summary>
    /// Rotation advanced to the next singer, but no song has been selected/assigned yet.
    /// </summary>
    WaitingForSongSelection,

    /// <summary>
    /// Singer and song are loaded and ready to be started.
    /// </summary>
    ReadyToStart,

    /// <summary>
    /// Song playback transition is in progress (guards against accidental double-starts).
    /// </summary>
    StartingSong
}
