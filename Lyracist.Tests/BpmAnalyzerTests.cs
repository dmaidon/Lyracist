// Created on Oct 7, 2026 @ 19:56:00 -> Unit tests for managed BpmAnalyzer onset detection, tempo folding, and confidence gating
using System;
using Lyracist.Data.Services;
using Xunit;

namespace Lyracist.Tests;

public class BpmAnalyzerTests
{
    private static float[] GenerateClickTrack(int bpm, double durationSeconds, int sampleRate = 11025)
    {
        int totalSamples = (int)(durationSeconds * sampleRate);
        float[] samples = new float[totalSamples];
        double intervalSeconds = 60.0 / bpm;
        int clickIntervalSamples = (int)(intervalSeconds * sampleRate);

        // Generate synthetic clicks: a short burst of noise or 800Hz decaying sine wave for 10ms
        int clickLength = (int)(0.015 * sampleRate); // 15ms click

        for (int clickStart = 0; clickStart + clickLength < totalSamples; clickStart += clickIntervalSamples)
        {
            for (int i = 0; i < clickLength; i++)
            {
                double t = (double)i / sampleRate;
                // Decaying 500 Hz tone
                double decay = Math.Exp(-i / (0.005 * sampleRate));
                double tone = Math.Sin(2 * Math.PI * 500 * t);
                samples[clickStart + i] = (float)(tone * decay * 0.9);
            }
        }

        return samples;
    }

    [Theory]
    [InlineData(100)]
    [InlineData(120)]
    [InlineData(140)]
    public void AnalyzeSamples_DetectsExactTempo_ForSyntheticClickTracks(int targetBpm)
    {
        float[] clickTrack = GenerateClickTrack(targetBpm, 15.0);
        int? detected = BpmAnalyzer.AnalyzeSamples(clickTrack, 11025);

        Assert.NotNull(detected);
        // Should be within +/- 2 BPM of target
        Assert.InRange(detected.Value, targetBpm - 2, targetBpm + 2);
    }

    [Fact]
    public void AnalyzeSamples_FoldsSlowTempo_IntoStandardRange()
    {
        // 60 BPM click track -> should fold into 120 BPM
        float[] clickTrack = GenerateClickTrack(60, 15.0);
        int? detected = BpmAnalyzer.AnalyzeSamples(clickTrack, 11025);

        Assert.NotNull(detected);
        Assert.InRange(detected.Value, 118, 122);
    }

    [Fact]
    public void AnalyzeSamples_ReturnsNull_ForSilence()
    {
        float[] silence = new float[11025 * 10];
        int? detected = BpmAnalyzer.AnalyzeSamples(silence, 11025);

        Assert.Null(detected);
    }

    [Fact]
    public void AnalyzeSamples_ReturnsNull_ForUniformNoise()
    {
        var rng = new Random(42);
        float[] noise = new float[11025 * 10];
        for (int i = 0; i < noise.Length; i++)
        {
            noise[i] = (float)(rng.NextDouble() * 0.2 - 0.1);
        }

        int? detected = BpmAnalyzer.AnalyzeSamples(noise, 11025);
        Assert.Null(detected);
    }
}
