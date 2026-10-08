// Edited on Oct 7, 2026 @ 20:05:00 -> Added FFmpegService using and CS0618 pragma suppression for WasapiCapture
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;
using NAudio.CoreAudioApi;
using NAudio.Wave;

#pragma warning disable CS0618 // WasapiCapture is obsolete in NAudio 3.1.0

namespace Lyracist.Services.Media;

public class PerformanceRecorderService : IPerformanceRecorderService, IDisposable
{
    private readonly object _lock = new();
    private WasapiCapture? _capture;
    private WaveFileWriter? _writer;
    private PerformanceRecordingSession? _currentSession;
    private string? _currentWavPath;
    private bool _isRecording;

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (_isRecording != value)
            {
                _isRecording = value;
                RecordingStateChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<bool>? RecordingStateChanged;
    public event EventHandler<float>? PeakLevelChanged;

    private readonly IMediaEngine? _mediaEngine;

    public PerformanceRecorderService(IMediaEngine? mediaEngine = null)
    {
        _mediaEngine = mediaEngine;
        if (_mediaEngine != null)
        {
            _mediaEngine.Started += OnMediaEngineStarted;
            _mediaEngine.Stopped += OnMediaEngineStopped;
            _mediaEngine.SongEnded += OnMediaEngineStopped;
        }

        // Prune expired recordings on startup in the background
        Task.Run(PruneExpiredRecordings);
    }

    private async void OnMediaEngineStarted()
    {
        if (_mediaEngine == null || _mediaEngine.IsMusicTrack) return;
        string? singer = _mediaEngine.ActiveSingerName;
        string? title = _mediaEngine.ActiveSongTitle;
        string? artist = _mediaEngine.ActiveSongArtist;
        await StartRecordingIfEligibleAsync(singer, title, artist);
    }

    private async void OnMediaEngineStopped()
    {
        await StopRecordingAsync();
    }

    private static long GetAvailableDiskSpace(string path)
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private static bool CheckSingerAllowsRecording(string singerName)
    {
        try
        {
            using var context = new LyracistDbContext();
            var singer = context.Singers
                .AsNoTracking()
                .FirstOrDefault(s => s.Name == singerName);
            return singer?.AllowRecording ?? false;
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", $"Failed to check AllowRecording for {singerName}", ex);
            return false;
        }
    }

    public async Task StartRecordingIfEligibleAsync(string? singerName, string? songTitle, string? artist)
    {
        if (string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle))
            return;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        long freeSpace = GetAvailableDiskSpace(baseDir);
        bool optedIn = CheckSingerAllowsRecording(singerName);

        if (!PerformanceRecordingStateMachine.CanRecord(
            AppSettings.RecordPerformancesEnabled,
            AppSettings.RecordingInputDevice,
            singerName,
            optedIn,
            freeSpace))
        {
            return;
        }

        await StopRecordingAsync();

        lock (_lock)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                MMDevice? device = null;
                try
                {
                    device = enumerator.GetDevice(AppSettings.RecordingInputDevice);
                }
                catch
                {
                    // Device missing or unplugged
                }

                if (device == null || device.State != DeviceState.Active)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", $"Recording device {AppSettings.RecordingInputDevice} not available. Performance recording skipped.", "PerformanceRecorderService");
                    return;
                }

                var session = new PerformanceRecordingSession(singerName, songTitle, artist ?? "Unknown", DateTime.UtcNow);
                string fullWavPath = Path.Combine(baseDir, session.RelativeWavPath);
                string? folder = Path.GetDirectoryName(fullWavPath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                var capture = new WasapiCapture(device);
                var writer = new WaveFileWriter(fullWavPath, capture.WaveFormat);

                capture.DataAvailable += (s, a) =>
                {
                    writer.Write(a.Buffer, 0, a.BytesRecorded);

                    // Compute peak amplitude for level meter
                    float peak = 0f;
                    if (capture.WaveFormat.BitsPerSample == 16)
                    {
                        for (int i = 0; i < a.BytesRecorded; i += 2)
                        {
                            short val = BitConverter.ToInt16(a.Buffer, i);
                            float abs = Math.Abs(val / 32768f);
                            if (abs > peak) peak = abs;
                        }
                    }
                    else if (capture.WaveFormat.BitsPerSample == 32 && capture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        for (int i = 0; i < a.BytesRecorded; i += 4)
                        {
                            float val = BitConverter.ToSingle(a.Buffer, i);
                            float abs = Math.Abs(val);
                            if (abs > peak) peak = abs;
                        }
                    }
                    PeakLevelChanged?.Invoke(this, peak);
                };

                capture.RecordingStopped += (s, a) =>
                {
                    try { writer.Flush(); writer.Dispose(); } catch { }
                    try { capture.Dispose(); } catch { }
                };

                capture.StartRecording();

                _capture = capture;
                _writer = writer;
                _currentSession = session;
                _currentWavPath = fullWavPath;
                IsRecording = true;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Failed to start performance recording", ex);
                IsRecording = false;
            }
        }
    }

    public Task StopRecordingAsync()
    {
        PerformanceRecordingSession? session;
        string? wavPath;
        WasapiCapture? capture;
        WaveFileWriter? writer;

        lock (_lock)
        {
            if (!IsRecording) return Task.CompletedTask;

            session = _currentSession;
            wavPath = _currentWavPath;
            capture = _capture;
            writer = _writer;

            _capture = null;
            _writer = null;
            _currentSession = null;
            _currentWavPath = null;
            IsRecording = false;
        }

        if (capture != null)
        {
            try
            {
                capture.StopRecording();
            }
            catch { }
        }

        if (session != null && wavPath != null)
        {
            var (keep, duration) = session.Complete(DateTime.UtcNow);
            if (!keep)
            {
                // Discard takes under 10 seconds
                _ = Task.Run(() =>
                {
                    try
                    {
                        // Give writer brief moment to release file lock
                        Thread.Sleep(200);
                        if (File.Exists(wavPath)) File.Delete(wavPath);
                    }
                    catch { }
                });
            }
            else
            {
                // Convert WAV to MP3 in the background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        Thread.Sleep(300);
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string fullMp3Path = Path.Combine(baseDir, session.RelativeMp3Path);
                        string? folder = Path.GetDirectoryName(fullMp3Path);
                        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = FFmpegService.FFmpegPath,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.Add("-hide_banner");
                        startInfo.ArgumentList.Add("-y");
                        startInfo.ArgumentList.Add("-i");
                        startInfo.ArgumentList.Add(wavPath);
                        startInfo.ArgumentList.Add("-codec:a");
                        startInfo.ArgumentList.Add("libmp3lame");
                        startInfo.ArgumentList.Add("-qscale:a");
                        startInfo.ArgumentList.Add("2");
                        startInfo.ArgumentList.Add(fullMp3Path);

                        using var proc = new Process { StartInfo = startInfo };
                        proc.Start();
                        await proc.WaitForExitAsync();

                        if (File.Exists(fullMp3Path) && new FileInfo(fullMp3Path).Length > 0)
                        {
                            // Delete WAV on successful encode
                            try { File.Delete(wavPath); } catch { }

                            using var context = new LyracistDbContext();
                            var singer = await context.Singers.FirstOrDefaultAsync(s => s.Name == session.SingerName);
                            var record = new PerformanceRecording
                            {
                                SingerName = session.SingerName,
                                SingerId = singer?.SingerId,
                                SongTitle = session.SongTitle,
                                Artist = session.Artist,
                                StartedUtc = session.StartedUtc,
                                DurationSeconds = duration,
                                FilePath = fullMp3Path
                            };
                            context.PerformanceRecordings.Add(record);
                            await context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Lyracist.Shared.Globals.LogError("Lyracist", $"Failed to encode performance recording to MP3 for {session.SingerName}", ex);
                    }
                });
            }
        }

        return Task.CompletedTask;
    }

    public async Task<string?> RecordTestClipAsync(int seconds = 5)
    {
        if (string.IsNullOrWhiteSpace(AppSettings.RecordingInputDevice) || AppSettings.RecordingInputDevice == "None")
            return null;

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(AppSettings.RecordingInputDevice);
            if (device == null || device.State != DeviceState.Active)
                return null;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string testDir = Path.Combine(baseDir, "Recordings", "Test");
            Directory.CreateDirectory(testDir);
            string testFile = Path.Combine(testDir, "test_recording.wav");

            var capture = new WasapiCapture(device);
            var writer = new WaveFileWriter(testFile, capture.WaveFormat);

            capture.DataAvailable += (s, a) =>
            {
                writer.Write(a.Buffer, 0, a.BytesRecorded);

                float peak = 0f;
                if (capture.WaveFormat.BitsPerSample == 16)
                {
                    for (int i = 0; i < a.BytesRecorded; i += 2)
                    {
                        short val = BitConverter.ToInt16(a.Buffer, i);
                        float abs = Math.Abs(val / 32768f);
                        if (abs > peak) peak = abs;
                    }
                }
                else if (capture.WaveFormat.BitsPerSample == 32)
                {
                    for (int i = 0; i < a.BytesRecorded; i += 4)
                    {
                        float val = BitConverter.ToSingle(a.Buffer, i);
                        float abs = Math.Abs(val);
                        if (abs > peak) peak = abs;
                    }
                }
                PeakLevelChanged?.Invoke(this, peak);
            };

            capture.StartRecording();
            await Task.Delay(seconds * 1000);
            capture.StopRecording();

            writer.Flush();
            writer.Dispose();
            capture.Dispose();

            return testFile;
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Test recording clip failed", ex);
            return null;
        }
    }

    public void PruneExpiredRecordings()
    {
        try
        {
            int retention = AppSettings.RecordingRetentionDays;
            if (retention <= 0) return; // 0 = keep forever

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string recordingsRoot = Path.Combine(baseDir, "Recordings");
            var expired = PerformanceRecordingStateMachine.GetExpiredDirectories(recordingsRoot, retention, DateTime.UtcNow);

            foreach (string dir in expired)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, true);
                    }
                }
                catch { }
            }

            // Also prune records in database older than retention
            DateTime cutoff = DateTime.UtcNow.Date.AddDays(-retention);
            using var context = new LyracistDbContext();
            var expiredRecords = context.PerformanceRecordings.Where(r => r.StartedUtc < cutoff).ToList();
            if (expiredRecords.Count > 0)
            {
                context.PerformanceRecordings.RemoveRange(expiredRecords);
                context.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to prune expired recordings", ex);
        }
    }

    public void Dispose()
    {
        _ = StopRecordingAsync();
    }
}

#pragma warning restore CS0618
