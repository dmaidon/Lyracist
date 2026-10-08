// Created on Oct 7, 2026 @ 19:42:00 -> Unit tests for LoudnessNormalizer helper
using Lyracist.Shared;
using Xunit;

namespace Lyracist.Tests;

public class LoudnessNormalizerTests
{
    [Fact]
    public void ComputeGainFactor_WhenHardwareMixerMode_ReturnsOne()
    {
        double factor = LoudnessNormalizer.ComputeGainFactor(
            measuredLufs: -24.0,
            targetLufs: -16.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: true);

        Assert.Equal(1.0, factor);
    }

    [Fact]
    public void ComputeGainFactor_WhenNormalizeDisabled_ReturnsOne()
    {
        double factor = LoudnessNormalizer.ComputeGainFactor(
            measuredLufs: -24.0,
            targetLufs: -16.0,
            normalizeVolumeEnabled: false,
            isHardwareMixerMode: false);

        Assert.Equal(1.0, factor);
    }

    [Fact]
    public void ComputeGainFactor_WhenMeasuredLufsIsNull_ReturnsOne()
    {
        double factor = LoudnessNormalizer.ComputeGainFactor(
            measuredLufs: null,
            targetLufs: -16.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: false);

        Assert.Equal(1.0, factor);
    }

    [Fact]
    public void ComputeGainFactor_WhenMeasuredEqualsTarget_ReturnsOne()
    {
        double factor = LoudnessNormalizer.ComputeGainFactor(
            measuredLufs: -16.0,
            targetLufs: -16.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: false);

        Assert.Equal(1.0, factor, 5);
    }

    [Fact]
    public void ComputeGainFactor_WhenMeasuredQuieterThanTarget_BoostsGai()
    {
        // Target -16, Measured -22 => diff +6 dB => 10^(6/20) ~ 1.995
        double factor = LoudnessNormalizer.ComputeGainFactor(
            measuredLufs: -22.0,
            targetLufs: -16.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: false);

        Assert.True(factor > 1.99 && factor < 2.0);
    }

    [Fact]
    public void ComputeGainFactor_WhenMeasuredLouderThanTarget_Attenuates()
    {
        // Target -16, Measured -10 => diff -6 dB => 10^(-6/20) ~ 0.501
        double factor = LoudnessNormalizer.ComputeGainFactor(
            measuredLufs: -10.0,
            targetLufs: -16.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: false);

        Assert.True(factor > 0.50 && factor < 0.51);
    }

    [Fact]
    public void CalculateVolume_ClampsAtMaximum200()
    {
        // Base volume 150 with a huge gain boost would exceed 200 without clamping
        double volume = LoudnessNormalizer.CalculateVolume(
            baseVolume: 150.0,
            measuredLufs: -40.0,
            targetLufs: -14.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: false);

        Assert.Equal(200.0, volume);
    }

    [Fact]
    public void CalculateVolume_ClampsAtMinimumZero()
    {
        double volume = LoudnessNormalizer.CalculateVolume(
            baseVolume: 0.0,
            measuredLufs: -20.0,
            targetLufs: -16.0,
            normalizeVolumeEnabled: true,
            isHardwareMixerMode: false);

        Assert.Equal(0.0, volume);
    }
}
