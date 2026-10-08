// Created on Oct 7, 2026 @ 20:00:00 -> State machine, filename sanitizing, retention pruning, and gating for performance recording
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Lyracist.Shared;

public static class PerformanceRecordingStateMachine
{
    public const long MinFreeDiskBytes = 500L * 1024 * 1024; // 500 MB
    public const double MinTakeDurationSeconds = 10.0;

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "Unknown";

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        while (fileName.Contains("__"))
        {
            fileName = fileName.Replace("__", "_");
        }

        fileName = fileName.Trim(' ', '_');
        return string.IsNullOrWhiteSpace(fileName) ? "Unknown" : fileName;
    }

    public static string GetRecordingRelativePath(DateTime date, string singerName, string songTitle, string ext = ".wav")
    {
        string dateFolder = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string safeSinger = SanitizeFileName(singerName);
        string safeTitle = SanitizeFileName(songTitle);
        string file = $"{safeSinger} - {safeTitle}{ext}";
        return Path.Combine("Recordings", dateFolder, file);
    }

    public static bool CanRecord(
        bool enabled,
        string? inputDeviceId,
        string? singerName,
        bool allowRecording,
        long freeDiskBytes)
    {
        if (!enabled) return false;
        if (string.IsNullOrWhiteSpace(inputDeviceId) || inputDeviceId == "None") return false;
        if (string.IsNullOrWhiteSpace(singerName)) return false;
        if (string.Equals(singerName.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase)) return false;
        if (!allowRecording) return false;
        if (freeDiskBytes < MinFreeDiskBytes) return false;

        return true;
    }

    public static List<string> GetExpiredDirectories(string recordingsRootDir, int retentionDays, DateTime nowUtc)
    {
        var expiredDirs = new List<string>();
        if (retentionDays <= 0 || !Directory.Exists(recordingsRootDir))
            return expiredDirs;

        DateTime cutoff = nowUtc.Date.AddDays(-retentionDays);

        foreach (string dir in Directory.GetDirectories(recordingsRootDir))
        {
            string name = Path.GetFileName(dir);
            if (DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dirDate))
            {
                if (dirDate < cutoff)
                {
                    expiredDirs.Add(dir);
                }
            }
        }

        return expiredDirs;
    }
}

public class PerformanceRecordingSession
{
    public string SingerName { get; }
    public string SongTitle { get; }
    public string Artist { get; }
    public DateTime StartedUtc { get; }
    public string RelativeWavPath { get; }
    public string RelativeMp3Path { get; }

    public PerformanceRecordingSession(string singerName, string songTitle, string artist, DateTime startedUtc)
    {
        SingerName = singerName;
        SongTitle = songTitle;
        Artist = artist;
        StartedUtc = startedUtc;
        RelativeWavPath = PerformanceRecordingStateMachine.GetRecordingRelativePath(startedUtc, singerName, songTitle, ".wav");
        RelativeMp3Path = PerformanceRecordingStateMachine.GetRecordingRelativePath(startedUtc, singerName, songTitle, ".mp3");
    }

    public (bool Keep, double DurationSeconds) Complete(DateTime stoppedUtc)
    {
        double duration = (stoppedUtc - StartedUtc).TotalSeconds;
        bool keep = duration >= PerformanceRecordingStateMachine.MinTakeDurationSeconds;
        return (keep, duration);
    }
}
