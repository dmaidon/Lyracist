// Created on Oct 7, 2026 @ 19:43:00 -> Lightweight record representing a track in the background filler playlist
namespace Lyracist.Shared;

/// <summary>
/// A lightweight representation of a filler track for playback, loudness leveling,
/// cue point navigation, and smart shuffle sequencing.
/// </summary>
public sealed record FillInTrack(string Path, string? Artist = null, double? Bpm = null);
