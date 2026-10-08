// Created on Oct 7, 2026 @ 19:42:00 -> Helper for EBU R128 integrated loudness normalization gain factor and volume calculation
using System;

namespace Lyracist.Shared;

/// <summary>
/// Provides shared EBU R128 integrated loudness normalization calculations
/// across MediaEngine and BackgroundMusicPlayer.
/// </summary>
public static class LoudnessNormalizer
{
    public const double MinVolume = 0.0;
    public const double MaxVolume = 200.0;

    /// <summary>
    /// Computes the multiplicative gain factor from measured integrated loudness (LUFS) and target LUFS.
    /// Returns 1.0 if normalization is disabled, in hardware mixer mode, or if measured loudness is null.
    /// </summary>
    public static double ComputeGainFactor(
        double? measuredLufs,
        double targetLufs,
        bool normalizeVolumeEnabled,
        bool isHardwareMixerMode)
    {
        if (isHardwareMixerMode || !normalizeVolumeEnabled || !measuredLufs.HasValue)
        {
            return 1.0;
        }

        return Math.Pow(10.0, (targetLufs - measuredLufs.Value) / 20.0);
    }

    /// <summary>
    /// Calculates the scaled volume clamped between 0.0 and 200.0 given a base volume and loudness parameters.
    /// </summary>
    public static double CalculateVolume(
        double baseVolume,
        double? measuredLufs,
        double targetLufs,
        bool normalizeVolumeEnabled,
        bool isHardwareMixerMode)
    {
        double factor = ComputeGainFactor(measuredLufs, targetLufs, normalizeVolumeEnabled, isHardwareMixerMode);
        return Math.Clamp(baseVolume * factor, MinVolume, MaxVolume);
    }
}
