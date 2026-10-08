// Created on Oct 7, 2026 @ 20:01:00 -> Unit tests for performance recording state machine, gating, sanitizing, and retention pruning
using System;
using System.IO;
using Lyracist.Shared;
using Xunit;

namespace Lyracist.Tests;

public class PerformanceRecordingStateMachineTests
{
    [Theory]
    [InlineData("Alice/Bob:Test*Song?", "Alice_Bob_Test_Song")]
    [InlineData("Singer <Name> \"Quote\"", "Singer _Name_ _Quote")]
    [InlineData("  Clean Artist  ", "Clean Artist")]
    [InlineData("A__B___C", "A_B_C")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    public void SanitizeFileName_CleansIllegalCharacters(string input, string expected)
    {
        string actual = PerformanceRecordingStateMachine.SanitizeFileName(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanRecord_EnforcesAllGatingRules()
    {
        long sufficientDisk = 600L * 1024 * 1024;
        long lowDisk = 400L * 1024 * 1024;

        // Valid baseline
        Assert.True(PerformanceRecordingStateMachine.CanRecord(true, "{device-guid}", "Alice", true, sufficientDisk));

        // Disabled
        Assert.False(PerformanceRecordingStateMachine.CanRecord(false, "{device-guid}", "Alice", true, sufficientDisk));

        // Empty or None device
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "", "Alice", true, sufficientDisk));
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "None", "Alice", true, sufficientDisk));
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, null, "Alice", true, sufficientDisk));

        // Singer not opted in
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "{device-guid}", "Alice", false, sufficientDisk));

        // Unknown or empty singer
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "{device-guid}", "", true, sufficientDisk));
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "{device-guid}", "Unknown", true, sufficientDisk));
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "{device-guid}", "unknown", true, sufficientDisk));

        // Low disk space (< 500 MB)
        Assert.False(PerformanceRecordingStateMachine.CanRecord(true, "{device-guid}", "Alice", true, lowDisk));
    }

    [Fact]
    public void CompleteTake_DiscardsUnder10Seconds()
    {
        var started = new DateTime(2026, 10, 7, 20, 0, 0, DateTimeKind.Utc);
        var session = new PerformanceRecordingSession("Alice", "My Song", "My Artist", started);

        // 8 seconds -> discard
        var (keepShort, durationShort) = session.Complete(started.AddSeconds(8.0));
        Assert.False(keepShort);
        Assert.Equal(8.0, durationShort, 1);

        // 12 seconds -> keep
        var (keepLong, durationLong) = session.Complete(started.AddSeconds(12.0));
        Assert.True(keepLong);
        Assert.Equal(12.0, durationLong, 1);
    }

    [Fact]
    public void GetExpiredDirectories_RespectsRetentionThreshold()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LyracistRecTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            DateTime now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

            // Create directories: 40 days ago, 20 days ago, today
            string oldDir = Path.Combine(tempDir, now.AddDays(-40).ToString("yyyy-MM-dd"));
            string recentDir = Path.Combine(tempDir, now.AddDays(-20).ToString("yyyy-MM-dd"));
            string todayDir = Path.Combine(tempDir, now.ToString("yyyy-MM-dd"));

            Directory.CreateDirectory(oldDir);
            Directory.CreateDirectory(recentDir);
            Directory.CreateDirectory(todayDir);

            // Retention = 30 days -> oldDir is expired
            var expired30 = PerformanceRecordingStateMachine.GetExpiredDirectories(tempDir, 30, now);
            Assert.Single(expired30);
            Assert.Contains(oldDir, expired30);

            // Retention = 0 (keep forever) -> nothing expired
            var expired0 = PerformanceRecordingStateMachine.GetExpiredDirectories(tempDir, 0, now);
            Assert.Empty(expired0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
