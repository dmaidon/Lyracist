// Created on Oct 8, 2026 @ 10:00:00 -> Regression tests for the review fixes to performance recording and cue-point parsing
using System;
using Lyracist.Data.Services;
using Lyracist.Services.Media;
using Lyracist.Shared;
using NAudio.Wave;
using Xunit;

namespace Lyracist.Tests;

public class FillerAndRecordingReviewTests
{
    // ─── Recording file naming ──────────────────────────────────────────────

    [Fact]
    public void RecordingPath_RepeatTakesGetNumberedSuffixInsteadOfOverwriting()
    {
        var date = new DateTime(2026, 10, 7);
        string first = PerformanceRecordingStateMachine.GetRecordingRelativePath(date, "Alice", "My Song", ".mp3");
        string second = PerformanceRecordingStateMachine.GetRecordingRelativePath(date, "Alice", "My Song", ".mp3", takeNumber: 2);

        Assert.EndsWith("Alice - My Song.mp3", first);
        Assert.EndsWith("Alice - My Song (2).mp3", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Session_UsesLocalCalendarDayForFolder_NotUtc()
    {
        // 01:30 UTC is still "yesterday evening" in the Americas; the folder must match the host's local day.
        var startedUtc = new DateTime(2026, 10, 8, 1, 30, 0, DateTimeKind.Utc);
        var session = new PerformanceRecordingSession("Alice", "Song", "Artist", startedUtc);

        string expectedFolder = startedUtc.ToLocalTime().ToString("yyyy-MM-dd");
        Assert.Contains(expectedFolder, session.RelativeWavPath);
    }

    [Theory]
    [InlineData("Wait for it...", "Wait for it")]
    [InlineData("Mr. Brightside.", "Mr. Brightside")]
    public void SanitizeFileName_TrimsTrailingDots(string input, string expected)
    {
        Assert.Equal(expected, PerformanceRecordingStateMachine.SanitizeFileName(input));
    }

    // ─── Level meter ────────────────────────────────────────────────────────

    [Fact]
    public void ComputePeak_Float32()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        byte[] buffer = new byte[16];
        BitConverter.GetBytes(0.25f).CopyTo(buffer, 0);
        BitConverter.GetBytes(-0.75f).CopyTo(buffer, 4);

        Assert.Equal(0.75f, PerformanceRecorderService.ComputePeak(format, buffer, buffer.Length), 3);
    }

    [Fact]
    public void ComputePeak_Pcm16_And_Pcm24()
    {
        var f16 = new WaveFormat(44100, 16, 2);
        byte[] b16 = new byte[4];
        BitConverter.GetBytes((short)16384).CopyTo(b16, 0); // 0.5
        Assert.Equal(0.5f, PerformanceRecorderService.ComputePeak(f16, b16, b16.Length), 3);

        var f24 = new WaveFormat(48000, 24, 2);
        int value = -4194304; // -0.5 in 24-bit
        byte[] b24 = [(byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF)];
        Assert.Equal(0.5f, PerformanceRecorderService.ComputePeak(f24, b24, b24.Length), 3);
    }

    [Fact]
    public void ComputePeak_Extensible_IsRecognizedLikeItsUnderlyingFormat()
    {
        // WASAPI's shared-mode mix format is WAVE_FORMAT_EXTENSIBLE; its Encoding is "Extensible", which the
        // old meter didn't recognize, so the level bar stayed at zero on most devices.
        var format = new WaveFormatExtensible(48000, 32, 2);
        var standard = format.ToStandardWaveFormat();

        byte[] buffer = new byte[8];
        if (standard.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            BitConverter.GetBytes(0.5f).CopyTo(buffer, 0);
        }
        else
        {
            BitConverter.GetBytes(int.MaxValue / 2).CopyTo(buffer, 0);
        }

        Assert.Equal(0.5f, PerformanceRecorderService.ComputePeak(format, buffer, buffer.Length), 2);
    }

    // ─── silencedetect parsing ──────────────────────────────────────────────

    [Fact]
    public void ParseSilenceDetect_HandlesNegativeLeadingSilenceStart()
    {
        // ffmpeg occasionally reports a tiny negative silence_start for leading silence; ignoring the sign used to
        // drop that entry and shift every start/end pair out of alignment.
        string output = """
            [silencedetect @ 0x1] silence_start: -0.0013
            [silencedetect @ 0x1] silence_end: 2.5 | silence_duration: 2.5013
            [silencedetect @ 0x1] silence_start: 200.1
            """;

        var (mixIn, mixOut) = FFmpegService.ParseSilenceDetectOutput(output, 210);

        Assert.Equal(2500, mixIn);
        Assert.Equal(200100, mixOut);
    }
}
