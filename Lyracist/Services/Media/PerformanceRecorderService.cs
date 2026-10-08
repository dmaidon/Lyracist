// Edited on Oct 8, 2026 @ 09:30:00 -> Review fixes: finalize WAV before encoding, resume-safe takes, unique take names, local-date folders, ffmpeg exit-code/stderr handling, device disposal, orphan WAV recovery
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
    // Keep recording briefly after the song ends so the last note / reverb tail isn't clipped.
    private const int StopTailMs = 1200;
    private const int EncodeTimeoutMinutes = 10;

    private readonly object _lock = new();
    private readonly SemaphoreSlim _opGate = new(1, 1);
    private readonly SemaphoreSlim _testGate = new(1, 1);
    private readonly IMediaEngine? _mediaEngine;
    private RecordingTake? _take;
    private bool _disposed;

    /// <summary>Root folder for all recordings (next to the app's Data folder, so it is as writable as the database).</summary>
    public static string RecordingsRoot => Path.Combine(Globals.DataDir, "Recordings");

    public bool IsRecording
    {
        get { lock (_lock) { return _take != null; } }
    }

    public event EventHandler<bool>? RecordingStateChanged;
    public event EventHandler<float>? PeakLevelChanged;

    public PerformanceRecorderService(IMediaEngine? mediaEngine = null)
    {
        _mediaEngine = mediaEngine;
        if (_mediaEngine != null)
        {
            _mediaEngine.Started += OnMediaEngineStarted;
            _mediaEngine.Stopped += OnMediaEngineStopped;
            _mediaEngine.SongEnded += OnMediaEngineStopped;
        }

        // Housekeeping off the UI thread: retention pruning, then rescue any WAV left behind by a crash/exit mid-encode.
        _ = Task.Run(async () =>
        {
            PruneExpiredRecordings();
            await RecoverOrphanedRecordingsAsync().ConfigureAwait(false);
        });
    }

    // ─── Media engine hooks ─────────────────────────────────────────────────

    private async void OnMediaEngineStarted()
    {
        try
        {
            if (_disposed || _mediaEngine == null || _mediaEngine.IsMusicTrack) return;
            string? singer = _mediaEngine.ActiveSingerName;
            string? title = _mediaEngine.ActiveSongTitle;
            string? artist = _mediaEngine.ActiveSongArtist;

            // Playback resumed after a pause (Pause() doesn't raise Stopped): keep the current take going
            // instead of splitting it into two takes.
            lock (_lock)
            {
                if (_take is { StopPending: false } t
                    && string.Equals(t.Session.SingerName, singer, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(t.Session.SongTitle, title, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            await StartRecordingIfEligibleAsync(singer, title, artist).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Performance recorder: start handler failed", ex);
        }
    }

    private async void OnMediaEngineStopped()
    {
        try
        {
            RecordingTake? take;
            lock (_lock)
            {
                take = _take;
                if (take == null) return;
                take.StopPending = true;
            }

            await Task.Delay(StopTailMs).ConfigureAwait(false);

            // Only stop if this is still the take that was running (a new song may have replaced it).
            lock (_lock)
            {
                if (!ReferenceEquals(_take, take)) return;
            }

            await StopRecordingAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Performance recorder: stop handler failed", ex);
        }
    }

    // ─── Start / stop ───────────────────────────────────────────────────────

    private static long GetAvailableDiskSpace(string path)
    {
        try
        {
            string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? "C:\\";
            return new DriveInfo(root).AvailableFreeSpace;
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
            Globals.LogError("Lyracist", $"Failed to check AllowRecording for {singerName}", ex);
            return false;
        }
    }

    public async Task StartRecordingIfEligibleAsync(string? singerName, string? songTitle, string? artist)
    {
        if (_disposed || string.IsNullOrWhiteSpace(singerName) || string.IsNullOrWhiteSpace(songTitle))
            return;

        // Cheap gate first so the common "recording is off" case never touches the database or disk.
        if (!AppSettings.RecordPerformancesEnabled || string.IsNullOrWhiteSpace(AppSettings.RecordingInputDevice))
            return;

        string root = RecordingsRoot;
        var (optedIn, freeSpace) = await Task.Run(() => (CheckSingerAllowsRecording(singerName), GetAvailableDiskSpace(Globals.DataDir))).ConfigureAwait(false);

        if (!PerformanceRecordingStateMachine.CanRecord(
            AppSettings.RecordPerformancesEnabled,
            AppSettings.RecordingInputDevice,
            singerName,
            optedIn,
            freeSpace))
        {
            return;
        }

        await _opGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            await StopCoreAsync().ConfigureAwait(false);

            MMDevice? device = null;
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                try { device = enumerator.GetDevice(AppSettings.RecordingInputDevice); }
                catch { /* device missing or unplugged */ }

                if (device == null || device.State != DeviceState.Active)
                {
                    device?.Dispose();
                    Globals.LogError("Lyracist", $"Recording device {AppSettings.RecordingInputDevice} not available. Performance recording skipped.", "PerformanceRecorderService");
                    return;
                }

                // A repeat take of the same song by the same singer gets " (2)", " (3)"... instead of overwriting.
                DateTime startedUtc = DateTime.UtcNow;
                PerformanceRecordingSession session;
                string fullWavPath;
                int take = 1;
                do
                {
                    session = new PerformanceRecordingSession(singerName, songTitle, artist ?? "Unknown", startedUtc, take++);
                    fullWavPath = Path.Combine(Globals.DataDir, session.RelativeWavPath);
                }
                while (take < 100 && (File.Exists(fullWavPath) || File.Exists(Path.Combine(Globals.DataDir, session.RelativeMp3Path))));

                Directory.CreateDirectory(Path.GetDirectoryName(fullWavPath)!);

                var capture = new ActiveCapture(device, fullWavPath, peak => PeakLevelChanged?.Invoke(this, peak));
                device = null; // ownership moved into the capture
                try
                {
                    capture.Start();
                }
                catch
                {
                    capture.Dispose();
                    throw;
                }

                lock (_lock)
                {
                    _take = new RecordingTake(session, fullWavPath, capture);
                }
            }
            catch (Exception ex)
            {
                device?.Dispose();
                Globals.LogError("Lyracist", "Failed to start performance recording", ex);
                return;
            }
        }
        finally
        {
            _opGate.Release();
        }

        RecordingStateChanged?.Invoke(this, true);
    }

    public async Task StopRecordingAsync()
    {
        await _opGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _opGate.Release();
        }
    }

    // Must be called with _opGate held.
    private async Task StopCoreAsync()
    {
        RecordingTake? take;
        lock (_lock)
        {
            take = _take;
            _take = null;
        }

        if (take == null) return;

        RecordingStateChanged?.Invoke(this, false);

        // Wait until NAudio has delivered the final buffers and the WAV header has been finalized;
        // ffmpeg must never read a half-written file.
        await take.Capture.StopAsync().ConfigureAwait(false);

        var (keep, duration) = take.Session.Complete(DateTime.UtcNow);
        if (!keep)
        {
            // Discard takes under 10 seconds
            TryDelete(take.WavPath);
            return;
        }

        _ = Task.Run(() => EncodeAndRegisterAsync(take.Session, take.WavPath, duration));
    }

    // ─── Encoding ───────────────────────────────────────────────────────────

    private static async Task EncodeAndRegisterAsync(PerformanceRecordingSession session, string wavPath, double duration)
    {
        string mp3Path = Path.Combine(Globals.DataDir, session.RelativeMp3Path);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mp3Path)!);

            int exitCode = await RunFfmpegAsync(
                ["-hide_banner", "-v", "error", "-nostats", "-y", "-i", wavPath, "-codec:a", "libmp3lame", "-qscale:a", "2", mp3Path],
                TimeSpan.FromMinutes(EncodeTimeoutMinutes)).ConfigureAwait(false);

            if (exitCode == 0 && File.Exists(mp3Path) && new FileInfo(mp3Path).Length > 0)
            {
                await RegisterRecordingAsync(session.SingerName, session.SongTitle, session.Artist, session.StartedUtc, duration, mp3Path).ConfigureAwait(false);
                TryDelete(wavPath); // only after the MP3 is safely on disk and registered
            }
            else
            {
                // Keep the WAV (it is recovered on next startup) and remove the partial MP3.
                Globals.LogError("Lyracist", $"ffmpeg MP3 encode failed (exit {exitCode}) for {session.SingerName} - {session.SongTitle}", "PerformanceRecorderService");
                TryDelete(mp3Path);
            }
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", $"Failed to encode performance recording to MP3 for {session.SingerName}", ex);
        }
    }

    private static async Task RegisterRecordingAsync(string singerName, string title, string artist, DateTime startedUtc, double duration, string mp3Path)
    {
        using var context = new LyracistDbContext();
        var singer = await context.Singers.AsNoTracking().FirstOrDefaultAsync(s => s.Name == singerName).ConfigureAwait(false);
        context.PerformanceRecordings.Add(new PerformanceRecording
        {
            SingerName = singerName,
            SingerId = singer?.SingerId,
            SongTitle = title,
            Artist = artist,
            StartedUtc = startedUtc,
            DurationSeconds = duration,
            FilePath = mp3Path
        });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>Runs ffmpeg with stderr drained (so a chatty encode can't block on a full pipe) and a hard timeout.</summary>
    private static async Task<int> RunFfmpegAsync(string[] args, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FFmpegService.FFmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string a in args) startInfo.ArgumentList.Add(a);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return -1;
        }

        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    // ─── Test clip ──────────────────────────────────────────────────────────

    public async Task<string?> RecordTestClipAsync(int seconds = 5)
    {
        if (string.IsNullOrWhiteSpace(AppSettings.RecordingInputDevice) || AppSettings.RecordingInputDevice == "None")
            return null;

        if (!await _testGate.WaitAsync(0).ConfigureAwait(false))
            return null; // a test clip is already being recorded

        try
        {
            MMDevice? device = null;
            using (var enumerator = new MMDeviceEnumerator())
            {
                try { device = enumerator.GetDevice(AppSettings.RecordingInputDevice); }
                catch { device = null; }
            }

            if (device == null || device.State != DeviceState.Active)
            {
                device?.Dispose();
                return null;
            }

            string testDir = Path.Combine(RecordingsRoot, "Test");
            Directory.CreateDirectory(testDir);
            string testFile = Path.Combine(testDir, "test_recording.wav");

            using var capture = new ActiveCapture(device, testFile, peak => PeakLevelChanged?.Invoke(this, peak));
            capture.Start();
            await Task.Delay(Math.Clamp(seconds, 1, 30) * 1000).ConfigureAwait(false);
            await capture.StopAsync().ConfigureAwait(false);

            return capture.Error == null ? testFile : null;
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Test recording clip failed", ex);
            return null;
        }
        finally
        {
            _testGate.Release();
        }
    }

    // ─── Retention and recovery ─────────────────────────────────────────────

    public void PruneExpiredRecordings()
    {
        try
        {
            int retention = AppSettings.RecordingRetentionDays;
            if (retention <= 0) return; // 0 = keep forever

            // Day folders are named with the host's local date, so compare against the local calendar day.
            var expired = PerformanceRecordingStateMachine.GetExpiredDirectories(RecordingsRoot, retention, DateTime.Now);

            foreach (string dir in expired)
            {
                try
                {
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
                catch { }
            }

            // Also prune database rows older than retention
            DateTime cutoff = DateTime.UtcNow.AddDays(-retention);
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
            Globals.LogError("Lyracist", "Failed to prune expired recordings", ex);
        }
    }

    /// <summary>
    /// Finishes any take whose WAV was left on disk (app closed or crashed before the MP3 encode finished):
    /// encodes it and registers it, or removes it if it is too short to keep.
    /// </summary>
    private async Task RecoverOrphanedRecordingsAsync()
    {
        try
        {
            string root = RecordingsRoot;
            if (!Directory.Exists(root)) return;

            string testDir = Path.Combine(root, "Test");
            foreach (string wav in Directory.EnumerateFiles(root, "*.wav", SearchOption.AllDirectories).ToList())
            {
                if (_disposed) return;
                if (wav.StartsWith(testDir, StringComparison.OrdinalIgnoreCase)) continue;

                lock (_lock)
                {
                    if (_take != null && string.Equals(_take.WavPath, wav, StringComparison.OrdinalIgnoreCase)) continue;
                }

                // Leave very fresh files alone: they may belong to a take that is still being written.
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(wav) < TimeSpan.FromMinutes(2)) continue;

                string mp3 = Path.ChangeExtension(wav, ".mp3");
                if (File.Exists(mp3) && new FileInfo(mp3).Length > 0)
                {
                    TryDelete(wav); // encode had finished; only the cleanup was missed
                    continue;
                }

                double duration;
                try
                {
                    using var reader = new WaveFileReader(wav);
                    duration = reader.TotalTime.TotalSeconds;
                }
                catch
                {
                    continue; // unreadable header - leave it for manual inspection
                }

                if (duration < PerformanceRecordingStateMachine.MinTakeDurationSeconds)
                {
                    TryDelete(wav);
                    continue;
                }

                // File name is "<Singer> - <Title>[ (n)].wav"
                string stem = Path.GetFileNameWithoutExtension(wav);
                int sep = stem.IndexOf(" - ", StringComparison.Ordinal);
                string singer = sep > 0 ? stem[..sep] : "Unknown";
                string title = sep > 0 ? stem[(sep + 3)..] : stem;
                DateTime startedUtc = File.GetCreationTimeUtc(wav);

                int exit = await RunFfmpegAsync(
                    ["-hide_banner", "-v", "error", "-nostats", "-y", "-i", wav, "-codec:a", "libmp3lame", "-qscale:a", "2", mp3],
                    TimeSpan.FromMinutes(EncodeTimeoutMinutes)).ConfigureAwait(false);

                if (exit == 0 && File.Exists(mp3) && new FileInfo(mp3).Length > 0)
                {
                    await RegisterRecordingAsync(singer, title, "Unknown", startedUtc, duration, mp3).ConfigureAwait(false);
                    TryDelete(wav);
                }
                else
                {
                    TryDelete(mp3);
                }
            }
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Failed to recover orphaned recordings", ex);
        }
    }

    // ─── Peak metering ──────────────────────────────────────────────────────

    /// <summary>
    /// Peak amplitude (0..1) of a captured buffer. Handles 16/24/32-bit PCM and 32-bit float, including
    /// WAVE_FORMAT_EXTENSIBLE (the usual WASAPI shared-mode mix format), which reports Encoding=Extensible.
    /// </summary>
    public static float ComputePeak(WaveFormat format, byte[] buffer, int bytesRecorded)
    {
        var encoding = format.Encoding;
        int bits = format.BitsPerSample;
        if (format is WaveFormatExtensible ext)
        {
            var std = ext.ToStandardWaveFormat();
            encoding = std.Encoding;
            bits = std.BitsPerSample;
        }

        float peak = 0f;
        if (encoding == WaveFormatEncoding.IeeeFloat && bits == 32)
        {
            for (int i = 0; i + 4 <= bytesRecorded; i += 4)
            {
                float abs = Math.Abs(BitConverter.ToSingle(buffer, i));
                if (abs > peak) peak = abs;
            }
        }
        else if (encoding == WaveFormatEncoding.Pcm)
        {
            switch (bits)
            {
                case 16:
                    for (int i = 0; i + 2 <= bytesRecorded; i += 2)
                    {
                        float abs = Math.Abs(BitConverter.ToInt16(buffer, i) / 32768f);
                        if (abs > peak) peak = abs;
                    }
                    break;
                case 24:
                    for (int i = 0; i + 3 <= bytesRecorded; i += 3)
                    {
                        int v = (buffer[i] | (buffer[i + 1] << 8) | (buffer[i + 2] << 16)) << 8 >> 8; // sign-extend 24 -> 32
                        float abs = Math.Abs(v / 8388608f);
                        if (abs > peak) peak = abs;
                    }
                    break;
                case 32:
                    for (int i = 0; i + 4 <= bytesRecorded; i += 4)
                    {
                        float abs = Math.Abs(BitConverter.ToInt32(buffer, i) / 2147483648f);
                        if (abs > peak) peak = abs;
                    }
                    break;
            }
        }

        return Math.Min(peak, 1f);
    }

    // ─── Capture plumbing ───────────────────────────────────────────────────

    private sealed class RecordingTake(PerformanceRecordingSession session, string wavPath, ActiveCapture capture)
    {
        public PerformanceRecordingSession Session { get; } = session;
        public string WavPath { get; } = wavPath;
        public ActiveCapture Capture { get; } = capture;

        /// <summary>True once the song has ended/stopped and the short tail delay is running.</summary>
        public bool StopPending { get; set; }
    }

    /// <summary>
    /// Owns one WASAPI capture + WAV writer + device. Dispose/StopAsync guarantee the WAV header is
    /// finalized and every native handle is released exactly once.
    /// </summary>
    private sealed class ActiveCapture : IDisposable
    {
        private readonly MMDevice _device;
        private readonly WasapiCapture _capture;
        private readonly WaveFileWriter _writer;
        private readonly Action<float> _onPeak;
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _cleanedUp;

        public Exception? Error { get; private set; }

        public ActiveCapture(MMDevice device, string wavPath, Action<float> onPeak)
        {
            _device = device;
            _onPeak = onPeak;
            _capture = new WasapiCapture(device);
            try
            {
                _writer = new WaveFileWriter(wavPath, _capture.WaveFormat);
            }
            catch
            {
                // The caller still owns the device if construction fails.
                _capture.Dispose();
                throw;
            }

            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
        }

        public void Start() => _capture.StartRecording();

        public async Task StopAsync(int timeoutMs = 5000)
        {
            try
            {
                _capture.StopRecording();
                await Task.WhenAny(_stopped.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Error ??= ex;
            }
            finally
            {
                Cleanup();
            }
        }

        private void OnData(object? sender, WaveInEventArgs e)
        {
            try
            {
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch (Exception ex)
            {
                Error ??= ex; // disk full / writer already closed
            }

            try
            {
                _onPeak(ComputePeak(_capture.WaveFormat, e.Buffer, e.BytesRecorded));
            }
            catch { }
        }

        private void OnStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null) Error ??= e.Exception;
            Cleanup();
            _stopped.TrySetResult();
        }

        private void Cleanup()
        {
            if (Interlocked.Exchange(ref _cleanedUp, 1) != 0) return;
            try { _writer.Dispose(); } catch { }
            try { _capture.Dispose(); } catch { }
            try { _device.Dispose(); } catch { }
        }

        public void Dispose() => Cleanup();
    }

    // ─── Shutdown ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_mediaEngine != null)
        {
            _mediaEngine.Started -= OnMediaEngineStarted;
            _mediaEngine.Stopped -= OnMediaEngineStopped;
            _mediaEngine.SongEnded -= OnMediaEngineStopped;
        }

        // Finalize the WAV synchronously so shutdown never leaves a half-written file. The MP3 encode
        // is not awaited; if the process exits first, the WAV is recovered on the next startup.
        try
        {
            Task.Run(async () =>
            {
                await _opGate.WaitAsync().ConfigureAwait(false);
                try { await StopCoreAsync().ConfigureAwait(false); }
                finally { _opGate.Release(); }
            }).Wait(TimeSpan.FromSeconds(8));
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Performance recorder: dispose failed", ex);
        }

        GC.SuppressFinalize(this);
    }
}

#pragma warning restore CS0618
