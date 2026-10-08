// Edited on Oct 7, 2026 @ 20:07:00 -> Add ActiveSongTitle, ActiveSongArtist, IsMusicTrack to MediaEngine
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lyracist.Core.Interfaces;
using Lyracist.Core.Helpers;
using Lyracist.Media.Video;
using Lyracist.Services.Media.Cdg;

namespace Lyracist.Services.Media;

public class MediaEngine : Lyracist.Core.Interfaces.IMediaEngine, IDisposable
{
    private bool _isDisposed;
    private readonly ICDGDecoder _cdgDecoder;
    private readonly ICdgFrameScheduler _scheduler;
    private readonly IVideoBackend _video;
    private readonly ILibraryService _libraryService;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _positionTimer;
    private bool _isPlaying;
    private bool _isMp4Mode;
    private string _currentSongPath = string.Empty;
    private string? _activeSingerName;
    private string? _activeDuetPartnerName;
    private string? _tempAudioPath;
    private string? _tempCdgPath;
    private string? _tempDir;
    private string? _loadedAudioPath;
    private CancellationTokenSource? _pitchChangeCts;
    private bool _isRenderingFrame;

    public event Action<ImageSource>? FrameReady;
    public event Action? Started;
    public event Action? Stopped;
    public event Action? SongEnded;
    public event Action? PositionChanged;

    public double Volume
    {
        get => _video.Volume;
        set => _video.Volume = value;
    }

    public double Speed
    {
        get => _video.Speed;
        set => _video.Speed = value;
    }

    public int Pitch
    {
        get => _video.Pitch;
        set
        {
            if (_video.Pitch != value)
            {
                _video.Pitch = value;

                // Hot-reload track with new pitch filter if currently playing
                if (_isPlaying && !string.IsNullOrEmpty(_loadedAudioPath))
                {
                    _pitchChangeCts?.Cancel();
                    _pitchChangeCts = new CancellationTokenSource();
                    var token = _pitchChangeCts.Token;

                    // Snapshot the path now, on the caller's thread, so a concurrent LoadSong()
                    // swapping _loadedAudioPath can't make this task reload the wrong song.
                    string loadedPath = _loadedAudioPath;

                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(150, token); // Debounce slider drags
                            token.ThrowIfCancellationRequested();

                            var currentPos = _video.Position;
                            bool wasPlaying = _isPlaying;

                            if (!_isMp4Mode)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _timer?.Stop());
                            }

                            // Load the media backend again
                            await _video.LoadAsync(loadedPath);
                            token.ThrowIfCancellationRequested();

                            // Apply equalizer, speed, volume, and the new Pitch setting
                            UpdateAudioParameters();

                            // Seek to the exact same position
                            await _video.SeekAsync(currentPos);
                            token.ThrowIfCancellationRequested();

                            if (wasPlaying && _isPlaying)
                            {
                                await _video.PlayAsync();
                                if (!_isMp4Mode)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _timer?.Start());
                                }
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (Exception ex)
                        {
                            Lyracist.Shared.Globals.LogError("Lyracist", "Error during pitch hot-reload", ex);
                        }
                    }, token);
                }
            }
        }
    }

    public double Treble
    {
        get => _video.Treble;
        set => _video.Treble = value;
    }

    public double Mid
    {
        get => _video.Mid;
        set => _video.Mid = value;
    }

    public double Bass
    {
        get => _video.Bass;
        set => _video.Bass = value;
    }

    public double Compressor
    {
        get => _video.Compressor;
        set => _video.Compressor = value;
    }

    public bool EnableKillVocal
    {
        get => _video.EnableKillVocal;
        set
        {
            if (_video.EnableKillVocal != value)
            {
                _video.EnableKillVocal = value;
                AppSettings.EnableKillVocal = value;
            }
        }
    }

    public double Limiter
    {
        get => _video.Limiter;
        set => _video.Limiter = value;
    }

    public string? ActiveSingerName
    {
        get => _activeSingerName;
        set
        {
            if (_activeSingerName != value)
            {
                _activeSingerName = value;
                UpdateAudioParameters();
            }
        }
    }

    public string? ActiveSongTitle { get; set; }
    public string? ActiveSongArtist { get; set; }
    public bool IsMusicTrack { get; set; }

    public string? ActiveDuetPartnerName
    {
        get => _activeDuetPartnerName;
        set
        {
            if (_activeDuetPartnerName != value)
            {
                _activeDuetPartnerName = value;
                UpdateAudioParameters();
            }
        }
    }

    private string? _activePerformerKey;
    public string? ActivePerformerKey
    {
        get => _activePerformerKey;
        set
        {
            if (_activePerformerKey != value)
            {
                _activePerformerKey = value;
                UpdateAudioParameters();
            }
        }
    }

    private double _activePerformerTempo = 1.0;
    public double ActivePerformerTempo
    {
        get => _activePerformerTempo;
        set
        {
            if (Math.Abs(_activePerformerTempo - value) > 0.01)
            {
                _activePerformerTempo = value;
                UpdateAudioParameters();
            }
        }
    }

    public MediaEngine(ICDGDecoder cdgDecoder, ICdgFrameScheduler scheduler, IVideoBackend video, ILibraryService libraryService)
    {
        _cdgDecoder = cdgDecoder;
        _scheduler = scheduler;
        _video = video;
        _libraryService = libraryService;

        _video.FrameReady += OnVideoFrameReady;
        _video.EndReached += (s, e) =>
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                bool wasPlaying = _isPlaying;
                _isPlaying = false;
                _loadedAudioPath = null;
                if (!_isMp4Mode)
                {
                    _timer?.Stop();
                    _scheduler.Reset();
                }
                SongEnded?.Invoke();
                if (wasPlaying)
                {
                    Stopped?.Invoke();
                }
            }));
        };
        InitializePlaybackTimer();
        InitializePositionTimer();
    }

    private void InitializePlaybackTimer()
    {
        // CDG delivers 300 packets/second regardless of how often this fires - a decoded lyrics
        // frame doesn't change meaningfully faster than a normal video frame rate, so 60 Hz here
        // was just extra Task.Run + Dispatcher.BeginInvoke churn (see OnPlaybackTick) for no
        // visible benefit over 30 Hz.
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _timer.Tick += OnPlaybackTick;
    }

    // Drives seek-slider updates. Deliberately separate from _timer above: _timer only runs in
    // CDG mode (it drives lyrics graphics rendering), but a seek slider needs to track position
    // in MP4 mode too. Runs for the lifetime of the app rather than being started/stopped with
    // playback - the tick handler is a no-op cost when nothing is loaded, and MediaEngine (like
    // the FrameReady subscribers on it) lives as long as the process does.
    private void InitializePositionTimer()
    {
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += (s, e) => PositionChanged?.Invoke();
        _positionTimer.Start();
    }

    private void OnVideoFrameReady(object? sender, VideoFrame frame)
    {
        if (_isMp4Mode)
        {
            FrameReady?.Invoke(frame.Bitmap);
        }
    }

    private void OnPlaybackTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _isMp4Mode || _isRenderingFrame) return;

        var audioPosition = _video.Position;
        _isRenderingFrame = true;

        Task.Run(() =>
        {
            try
            {
                if (_cdgDecoder is CdgDecoder cdg)
                {
                    _scheduler.UpdateBackground(audioPosition, cdg);
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "CDG background render error", ex);
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_isPlaying && !_isMp4Mode)
                        {
                            var frame = _scheduler.GetFrame();
                            if (frame != null)
                            {
                                FrameReady?.Invoke(frame);
                            }
                        }
                    }
                    finally
                    {
                        _isRenderingFrame = false;
                    }
                }));
            }
        });
    }

    public async Task LoadSong(string path)
    {
        await Stop();
        CleanUpTempFiles();

        if (string.IsNullOrEmpty(path)) return;

        _currentSongPath = path;

        string audioToLoad = path;
        string cdgToLoad = string.Empty;
        bool isZip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        _isMp4Mode = path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        if (isZip)
        {
            // Extraction is synchronous file I/O (ZipFile.OpenRead + ExtractToFile of a
            // multi-MB entry) - LoadSong is awaited straight from a UI command, so without
            // Task.Run this used to block the UI thread for however long the extraction took,
            // visible as a hitch every time a zipped karaoke track loads mid-show.
            await Task.Run(() =>
            {
                try
                {
                    _tempDir = Path.Combine(Path.GetTempPath(), "LyracistPlayback_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(_tempDir);

                    using var archive = ZipFile.OpenRead(path);
                    var cdgEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase));
                    var audioEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));

                    if (cdgEntry != null && audioEntry != null)
                    {
                        _tempAudioPath = Path.Combine(_tempDir, "audio" + Path.GetExtension(audioEntry.FullName));
                        _tempCdgPath = Path.Combine(_tempDir, "lyrics.cdg");

                        audioEntry.ExtractToFile(_tempAudioPath, true);
                        cdgEntry.ExtractToFile(_tempCdgPath, true);

                        audioToLoad = _tempAudioPath;
                        cdgToLoad = _tempCdgPath;
                    }
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "Error extracting ZIP playback", ex);
                    audioToLoad = path;
                }
            });
        }
        else if (!_isMp4Mode)
        {
            cdgToLoad = Path.ChangeExtension(path, ".cdg");
        }

        // Apply dynamic settings merging (Song -> Singer -> Defaults)
        UpdateAudioParameters();

        _loadedAudioPath = audioToLoad;

        // Always load the file (mp4 or mp3 audio) in the unmanaged video player to play the audio track
        await Task.Run(async () => await _video.LoadAsync(audioToLoad));

        if (!string.IsNullOrEmpty(cdgToLoad))
        {
            await _cdgDecoder.LoadAsync(cdgToLoad);
            if (_cdgDecoder is CdgDecoder cdg)
            {
                _scheduler.LoadPackets(cdg.Packets);
                _scheduler.Reset();
            }
        }
    }

    public async Task Play()
    {
        if (_isPlaying) return;
        _isPlaying = true;

        // Start playback on video/audio backend
        await Task.Run(async () => await _video.PlayAsync());

        if (!_isMp4Mode)
        {
            _timer?.Start();
        }

        Started?.Invoke();
    }

    public async Task Pause()
    {
        _isPlaying = false;

        await Task.Run(async () => await _video.PauseAsync());

        if (!_isMp4Mode)
        {
            _timer?.Stop();
        }
    }

    public async Task Stop()
    {
        // Cancel any in-flight pitch hot-reload so it can't load/seek/play over
        // whatever gets loaded next (see the Pitch setter).
        _pitchChangeCts?.Cancel();

        bool wasPlaying = _isPlaying;
        _isPlaying = false;
        _loadedAudioPath = null;

        await Task.Run(async () => await _video.StopAsync());
        CleanUpTempFiles();

        if (!_isMp4Mode)
        {
            _timer?.Stop();
            _scheduler.Reset();
        }

        if (wasPlaying)
        {
            Stopped?.Invoke();
        }
    }

    private void CleanUpTempFiles()
    {
        try
        {
            if (!string.IsNullOrEmpty(_tempAudioPath) && File.Exists(_tempAudioPath))
            {
                File.Delete(_tempAudioPath);
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to delete temp audio file", ex);
        }

        try
        {
            if (!string.IsNullOrEmpty(_tempCdgPath) && File.Exists(_tempCdgPath))
            {
                File.Delete(_tempCdgPath);
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to delete temp CDG file", ex);
        }

        try
        {
            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to delete temp directory", ex);
        }

        _tempAudioPath = null;
        _tempCdgPath = null;
        _tempDir = null;
    }

    public double Position => _video.Position.TotalSeconds;

    public double Duration => _video.Duration.TotalSeconds;

    public void Seek(double positionSeconds)
    {
        // Fire-and-forget: Seek() is called from slider drag events on the UI thread and must
        // return immediately, but the backend seek is async (it may need to demux to the target
        // position). Errors are logged rather than thrown since there's no caller left to observe
        // a faulted Task here.
        _ = SeekInternalAsync(positionSeconds);
    }

    private async Task SeekInternalAsync(double positionSeconds)
    {
        try
        {
            await _video.SeekAsync(TimeSpan.FromSeconds(Math.Max(0, positionSeconds)));

            // CDG frame rendering is driven by _scheduler's own notion of "how far into the audio
            // are we", built up incrementally by OnPlaybackTick. A seek jumps the audio position
            // without going through that incremental path, so the scheduler needs to be told
            // directly or the lyrics graphics stay stuck at wherever they were before the seek.
            if (!_isMp4Mode && _cdgDecoder is CdgDecoder cdg)
            {
                _scheduler.UpdateBackground(_video.Position, cdg);
                var frame = _scheduler.GetFrame();
                if (frame != null)
                {
                    FrameReady?.Invoke(frame);
                }
            }
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "MediaEngine.Seek", ex);
        }
    }

    public void UpdateAudioParameters()
    {
        if (string.IsNullOrEmpty(_currentSongPath))
        {
            // Reset to global defaults
            _video.Volume = 100.0;
            _video.Speed = 1.0;
            _video.Pitch = 0;
            _video.Treble = 0.0;
            _video.Mid = 0.0;
            _video.Bass = 0.0;
            _video.Compressor = 0.0;
            _video.Limiter = 0.0;
            _video.EnableKillVocal = AppSettings.EnableKillVocal;
            return;
        }

        var songSettings = _libraryService.GetAudioSettings(_currentSongPath);
        var singerSettings = !string.IsNullOrEmpty(ActiveSingerName) && ActiveSingerName != "None"
            ? _libraryService.GetSingerSettings(ActiveSingerName)
            : null;
        var partnerSettings = !string.IsNullOrEmpty(ActiveDuetPartnerName) && ActiveDuetPartnerName != "None"
            ? _libraryService.GetSingerSettings(ActiveDuetPartnerName)
            : null;

        // Base defaults
        double mergedVolume = 100.0;
        double mergedSpeed = 1.0;
        int mergedPitch = 0;
        double mergedTreble = 0.0;
        double mergedMid = 0.0;
        double mergedBass = 0.0;
        double mergedCompressor = 0.0;
        double mergedLimiter = 0.0;

        // Apply Song settings first (if customized or record exists)
        if (songSettings != null && songSettings.SongId > 0)
        {
            mergedVolume = songSettings.Gain > 0 ? songSettings.Gain : 100.0;
            mergedSpeed = songSettings.Tempo;
            mergedPitch = songSettings.Key;
            mergedTreble = songSettings.Treble;
            mergedMid = songSettings.Mid;
            mergedBass = songSettings.Bass;
            mergedCompressor = songSettings.Compressor;
            mergedLimiter = songSettings.Limiter;
        }

        int performerKeyOffset = 0;
        if (!string.IsNullOrWhiteSpace(ActivePerformerKey) && ActivePerformerKey != "0")
        {
            string clean = ActivePerformerKey.TrimStart('+');
            int.TryParse(clean, out performerKeyOffset);
        }
        double performerTempo = ActivePerformerTempo > 0.1 ? ActivePerformerTempo : 1.0;

        // Apply Singer settings (merge/fallback)
        if (singerSettings != null && singerSettings.SingerId > 0)
        {
            // If performer requested/recalled key is set, use it; otherwise use singer default key
            int effectiveKey = performerKeyOffset != 0 ? performerKeyOffset : singerSettings.Key;
            mergedPitch = Math.Clamp(mergedPitch + effectiveKey, -6, 6);

            // If performer requested/recalled tempo is set, use it; otherwise use singer default tempo
            double effectiveTempo = Math.Abs(performerTempo - 1.0) > 0.01 ? performerTempo : singerSettings.Tempo;
            mergedSpeed = Math.Clamp(mergedSpeed * effectiveTempo, 0.5, 2.0);

            // Volume attenuation multiplication
            double singerGain = singerSettings.Gain > 0 ? singerSettings.Gain : 100.0;
            mergedVolume = Math.Clamp((mergedVolume / 100.0) * (singerGain / 100.0) * 100.0, 0.0, 200.0);
            // Equalization filters sum (clamp to -10dB to +10dB)
            mergedTreble = Math.Clamp(mergedTreble + singerSettings.Treble, -10.0, 10.0);
            mergedMid = Math.Clamp(mergedMid + singerSettings.Mid, -10.0, 10.0);
            mergedBass = Math.Clamp(mergedBass + singerSettings.Bass, -10.0, 10.0);
            // Compressor threshold: use strongest threshold (Max)
            mergedCompressor = Math.Clamp(Math.Max(mergedCompressor, singerSettings.Compressor), 0.0, 100.0);
            // Limiter threshold: use lowest (most restrictive) dB ceiling (Min)
            mergedLimiter = Math.Clamp(Math.Min(mergedLimiter, singerSettings.Limiter), -20.0, 0.0);
        }
        else
        {
            if (performerKeyOffset != 0)
                mergedPitch = Math.Clamp(mergedPitch + performerKeyOffset, -6, 6);
            if (Math.Abs(performerTempo - 1.0) > 0.01)
                mergedSpeed = Math.Clamp(mergedSpeed * performerTempo, 0.5, 2.0);
        }

        // Merge in Duet Partner settings
        if (partnerSettings != null && partnerSettings.SingerId > 0)
        {
            double partnerGain = partnerSettings.Gain > 0 ? partnerSettings.Gain : 100.0;
            mergedVolume = Math.Clamp((mergedVolume / 100.0) * (partnerGain / 100.0) * 100.0, 0.0, 200.0);
            mergedTreble = Math.Clamp((mergedTreble + partnerSettings.Treble) / 2.0, -10.0, 10.0);
            mergedMid = Math.Clamp((mergedMid + partnerSettings.Mid) / 2.0, -10.0, 10.0);
            mergedBass = Math.Clamp((mergedBass + partnerSettings.Bass) / 2.0, -10.0, 10.0);
            mergedCompressor = Math.Clamp(Math.Max(mergedCompressor, partnerSettings.Compressor), 0.0, 100.0);
            mergedLimiter = Math.Clamp(Math.Min(mergedLimiter, partnerSettings.Limiter), -20.0, 0.0);
        }

        // Volume normalization: level perceived loudness across tracks using each track's measured
        // integrated loudness (LUFS), applied as one more multiplicative factor alongside the
        // Song/Singer/Duet gain above. Bypassed under hardware mixer mode, same as the EQ/
        // compressor/limiter settings below, since an external mixer is expected to own levels.
        if (!AppSettings.IsHardwareMixerMode && AppSettings.NormalizeVolumeEnabled)
        {
            double? measuredLufs = _libraryService.GetMeasuredLoudness(_currentSongPath);
            if (measuredLufs.HasValue)
            {
                double normFactor = Math.Pow(10.0, (AppSettings.TargetLoudnessLufs - measuredLufs.Value) / 20.0);
                mergedVolume = Math.Clamp(mergedVolume * normFactor, 0.0, 200.0);
            }
            else
            {
                // Not measured yet - measure it in the background (ffmpeg decodes the whole file,
                // so this must never block playback) and cache the result for next time.
                _ = _libraryService.MeasureAndSaveLoudnessAsync(_currentSongPath);
            }
        }

        // Apply merged results to the unmanaged player backend
        _video.AudioDeviceId = AppSettings.SelectedKaraokeAudioDevice;
        if (AppSettings.IsHardwareMixerMode)
        {
            _video.Volume = 100.0;
            _video.Speed = mergedSpeed;
            _video.Pitch = mergedPitch;
            _video.Treble = 0.0;
            _video.Mid = 0.0;
            _video.Bass = 0.0;
            _video.Compressor = 0.0;
            _video.Limiter = 0.0;
            _video.EnableKillVocal = false;
        }
        else
        {
            _video.Volume = mergedVolume;
            _video.Speed = mergedSpeed;
            _video.Pitch = mergedPitch;
            _video.Treble = mergedTreble;
            _video.Mid = mergedMid;
            _video.Bass = mergedBass;
            _video.Compressor = mergedCompressor;
            _video.Limiter = mergedLimiter;
            _video.EnableKillVocal = AppSettings.EnableKillVocal;
        }
    }

    // MediaEngine is registered AddSingleton<IMediaEngine, MediaEngine>() in App.xaml.cs, so this
    // runs once, from Host.Dispose() during app shutdown - not from any explicit call site.
    // IVideoBackend is its own separately-registered singleton and disposes itself; this only
    // needs to clean up what MediaEngine itself owns.
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _timer?.Stop();
        _positionTimer?.Stop();

        _pitchChangeCts?.Cancel();
        _pitchChangeCts?.Dispose();

        _video.FrameReady -= OnVideoFrameReady;
        // The EndReached handler was registered as an anonymous lambda in the constructor and so
        // can't be unsubscribed here directly - harmless to leave subscribed, since _video is
        // disposed independently and won't raise further events once torn down.

        GC.SuppressFinalize(this);
    }
}
