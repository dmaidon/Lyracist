// Created on Oct 3, 2026 @ 15:20:00 -> Tests for the shared SpectrumAnalyzer used by Lyracist loopback and KSRotation line-in synth bars
using System;
using System.Linq;
using Lyracist.Shared;
using Xunit;

namespace Lyracist.Tests;

public class SpectrumAnalyzerTests
{
    private const int SampleRate = 48000;
    private const int Bands = 32;

    private static float[] Sine(double freq, int samples, double amplitude)
    {
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = (float)(amplitude * Math.Sin(2 * Math.PI * freq * i / SampleRate));
        return data;
    }

    private static float[] Settle(SpectrumAnalyzer analyzer, float[] samples)
    {
        float[] bands = [];
        for (int i = 0; i < 20; i++)
        {
            analyzer.WriteSamples(samples);
            bands = analyzer.GetBands(Bands, SampleRate);
        }
        return bands;
    }

    [Fact]
    public void NoInput_ReturnsAllZeroBands()
    {
        var analyzer = new SpectrumAnalyzer();
        float[] bands = analyzer.GetBands(Bands, SampleRate);
        Assert.Equal(Bands, bands.Length);
        Assert.All(bands, b => Assert.Equal(0f, b));
    }

    [Fact]
    public void Tone_PeaksInTheBandContainingItsFrequency_AndStaysInRange()
    {
        var analyzer = new SpectrumAnalyzer();
        float[] bands = Settle(analyzer, Sine(1000, 2048, 0.3));

        Assert.All(bands, b => Assert.InRange(b, 0f, 1f));

        // Band edges are logarithmic between 32 Hz and 16 kHz.
        int expected = (int)(Math.Log(1000.0 / 32.0) / Math.Log(16000.0 / 32.0) * Bands);
        int peak = Array.IndexOf(bands, bands.Max());
        Assert.InRange(peak, expected - 1, expected + 1);
        Assert.True(bands.Max() > 0.5f, "a clear tone should drive its band well above the floor");
    }

    [Fact]
    public void AutoGain_QuietAndLoudToneReachSimilarHeights()
    {
        var quiet = Settle(new SpectrumAnalyzer(), Sine(1000, 2048, 0.02)).Max();
        var loud = Settle(new SpectrumAnalyzer(), Sine(1000, 2048, 0.6)).Max();

        Assert.True(quiet > 0.5f, $"quiet input should still fill the display (got {quiet})");
        Assert.True(loud - quiet < 0.5f, "loudness should not change bar height by orders of magnitude");
    }

    [Fact]
    public void Sensitivity_LetsVeryQuietInputRegister()
    {
        var samples = Sine(1000, 2048, 0.0006);
        float low = Settle(new SpectrumAnalyzer { Sensitivity = 0.25f }, samples).Max();
        float high = Settle(new SpectrumAnalyzer { Sensitivity = 4f }, samples).Max();

        Assert.True(high > low, $"higher sensitivity should show more ({high} vs {low})");
    }

    [Fact]
    public void Reset_LetsBarsFallAwayToZero()
    {
        var analyzer = new SpectrumAnalyzer();
        Settle(analyzer, Sine(1000, 2048, 0.3));
        analyzer.Reset();

        float[] bands = [];
        for (int i = 0; i < 60; i++) bands = analyzer.GetBands(Bands, SampleRate);

        Assert.All(bands, b => Assert.Equal(0f, b));
    }
}
