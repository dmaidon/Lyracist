// Created on Oct 7, 2026 @ 19:56:00 -> Unit tests for FFmpeg silencedetect cue point parsing (MixInMs and MixOutMs)
using Lyracist.Data.Services;
using Xunit;

namespace Lyracist.Tests;

public class CuePointsTests
{
    [Fact]
    public void ParseSilenceDetectOutput_ExtractsLeadingAndTrailingSilence()
    {
        string ffmpegStderr = """
            [silencedetect @ 000001] silence_start: 0
            [silencedetect @ 000001] silence_end: 1.542 | silence_duration: 1.542
            [silencedetect @ 000001] silence_start: 195.420
            [silencedetect @ 000001] silence_end: 200.000 | silence_duration: 4.580
            """;

        var (mixIn, mixOut) = FFmpegService.ParseSilenceDetectOutput(ffmpegStderr, 200.0);

        Assert.Equal(1542, mixIn);
        Assert.Equal(195420, mixOut);
    }

    [Fact]
    public void ParseSilenceDetectOutput_HandlesNoLeadingSilence()
    {
        string ffmpegStderr = """
            [silencedetect @ 000001] silence_start: 120.500
            [silencedetect @ 000001] silence_end: 121.200 | silence_duration: 0.700
            [silencedetect @ 000001] silence_start: 198.000
            """;

        var (mixIn, mixOut) = FFmpegService.ParseSilenceDetectOutput(ffmpegStderr, 205.0);

        Assert.Null(mixIn);
        Assert.Equal(198000, mixOut);
    }

    [Fact]
    public void ParseSilenceDetectOutput_IgnoresMidTrackSilence_WhenNoEndingSilence()
    {
        string ffmpegStderr = """
            [silencedetect @ 000001] silence_start: 45.200
            [silencedetect @ 000001] silence_end: 46.100 | silence_duration: 0.900
            """;

        // Track is 240 seconds long, middle silence is at 45s
        var (mixIn, mixOut) = FFmpegService.ParseSilenceDetectOutput(ffmpegStderr, 240.0);

        Assert.Null(mixIn);
        Assert.Null(mixOut);
    }

    [Fact]
    public void ParseSilenceDetectOutput_ReturnsNull_ForEmptyOrInvalidText()
    {
        var (mixIn, mixOut) = FFmpegService.ParseSilenceDetectOutput("", 100.0);
        Assert.Null(mixIn);
        Assert.Null(mixOut);
    }
}
