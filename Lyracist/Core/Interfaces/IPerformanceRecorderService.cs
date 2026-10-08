// Created on Oct 7, 2026 @ 20:05:00 -> Interface for performance audio recording service
using System;
using System.Threading.Tasks;

namespace Lyracist.Core.Interfaces;

public interface IPerformanceRecorderService
{
    bool IsRecording { get; }
    event EventHandler<bool>? RecordingStateChanged;
    event EventHandler<float>? PeakLevelChanged;
    Task StartRecordingIfEligibleAsync(string? singerName, string? songTitle, string? artist);
    Task StopRecordingAsync();
    void PruneExpiredRecordings();
    Task<string?> RecordTestClipAsync(int seconds = 5);
}
