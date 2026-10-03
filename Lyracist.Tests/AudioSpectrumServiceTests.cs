// Edited on Oct 3, 2026 @ 12:37:00 -> Add unit tests for two-way visualizer mode, style, and opacity checkable bindings
using System;
using Lyracist.Core.Helpers;
using Lyracist.Services.Display;
using Lyracist.Services.Media;
using Lyracist.ViewModels;
using Moq;
using Xunit;

namespace Lyracist.Tests;

public class AudioSpectrumServiceTests
{
    [Fact]
    public void AudioSpectrumService_Initializes_AndReturnsEmptyOnZeroOrNegativeCount()
    {
        using var service = new AudioSpectrumService();
        Assert.Empty(service.GetFrequencyBands(0));
        Assert.Empty(service.GetFrequencyBands(-5));
    }

    [Fact]
    public void AudioSpectrumService_ReturnsExpectedBandCount_WithValuesInRange()
    {
        using var service = new AudioSpectrumService();
        var bands = service.GetFrequencyBands(36);

        Assert.Equal(36, bands.Length);
        foreach (var val in bands)
        {
            Assert.InRange(val, 0.0f, 1.0f);
        }
    }

    [Fact]
    public void AppSettings_LyricsVisualizerSettings_PersistAndReadDefaults()
    {
        // Verify defaults
        Assert.True(AppSettings.EnableLyricsVisualizer);
        Assert.Equal("Audio Spectrum (Live FFT)", AppSettings.LyricsVisualizerMode);
        Assert.Contains(AppSettings.LyricsVisualizerStyle, new[] { "Neon Sunset", "Cyberpunk", "Emerald Pulse", "Solar Flare", "Electric Blue", "Rainbow Spectrum", "Monochrome Glow" });
        Assert.InRange(AppSettings.LyricsVisualizerOpacity, 0.1, 1.0);
        Assert.Contains(AppSettings.LyricsVisualizerBarWidth, new[] { "Slim", "Normal", "Wide", "Extra Wide" });

        // Test mutating values
        var origStyle = AppSettings.LyricsVisualizerStyle;
        try
        {
            AppSettings.LyricsVisualizerStyle = "Cyberpunk";
            Assert.Equal("Cyberpunk", AppSettings.LyricsVisualizerStyle);

            AppSettings.LyricsVisualizerBarWidth = "Wide";
            Assert.Equal("Wide", AppSettings.LyricsVisualizerBarWidth);

            AppSettings.LyricsVisualizerOpacity = 0.55;
            Assert.Equal(0.55, AppSettings.LyricsVisualizerOpacity, 2);
        }
        finally
        {
            AppSettings.LyricsVisualizerStyle = origStyle;
        }
    }

    [Fact]
    public void LyricsWindowViewModel_VisualizerCommands_UpdateProperties()
    {
        var displayMock = new Mock<IDisplayService>();
        displayMock.Setup(d => d.GetPreferences()).Returns(new DisplayPreferences());

        var vm = new LyricsWindowViewModel(displayMock.Object);

        // Test mode command
        vm.SetVisualizerModeCommand.Execute("Simulated / Ambient");
        Assert.Equal("Simulated / Ambient", vm.VisualizerMode);
        Assert.True(vm.IsModeSimulated);
        Assert.False(vm.IsModeLiveFft);

        vm.SetVisualizerModeCommand.Execute("Audio Spectrum (Live FFT)");
        Assert.Equal("Audio Spectrum (Live FFT)", vm.VisualizerMode);
        Assert.True(vm.IsModeLiveFft);
        Assert.False(vm.IsModeSimulated);

        // Test style command
        vm.SetVisualizerStyleCommand.Execute("Solar Flare");
        Assert.Equal("Solar Flare", vm.VisualizerStyle);
        Assert.True(vm.IsStyleSolarFlare);
        Assert.False(vm.IsStyleNeonSunset);

        // Test bar width command
        vm.SetVisualizerBarWidthCommand.Execute("Extra Wide");
        Assert.Equal("Extra Wide", vm.VisualizerBarWidth);
        Assert.True(vm.IsBarWidthExtraWide);
        Assert.False(vm.IsBarWidthSlim);

        // Test opacity command
        vm.SetVisualizerOpacityCommand.Execute("0.65");
        Assert.Equal(0.65, vm.VisualizerOpacity, 2);

        // Test direct two-way checkable properties (as WPF MenuItem does)
        vm.IsModeSimulated = true;
        Assert.Equal("Simulated / Ambient", vm.VisualizerMode);
        Assert.True(vm.IsModeSimulated);
        Assert.False(vm.IsModeLiveFft);

        vm.IsModeLiveFft = true;
        Assert.Equal("Audio Spectrum (Live FFT)", vm.VisualizerMode);
        Assert.True(vm.IsModeLiveFft);
        Assert.False(vm.IsModeSimulated);

        // Test style checkable property
        vm.IsStyleCyberpunk = true;
        Assert.Equal("Cyberpunk", vm.VisualizerStyle);
        Assert.True(vm.IsStyleCyberpunk);
        Assert.False(vm.IsStyleSolarFlare);

        // Test bar width checkable property
        vm.IsBarWidthSlim = true;
        Assert.Equal("Slim", vm.VisualizerBarWidth);
        Assert.True(vm.IsBarWidthSlim);

        // Test opacity checkable property
        vm.IsOpacity50 = true;
        Assert.Equal(0.50, vm.VisualizerOpacity, 2);
        Assert.True(vm.IsOpacity50);
        Assert.False(vm.IsOpacity100);
    }
}

