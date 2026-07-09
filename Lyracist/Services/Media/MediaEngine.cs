using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lyracist.Core.Interfaces;
using Lyracist.Media.Video;
using Lyracist.Services.Media.Cdg;

namespace Lyracist.Services.Media;

public class MediaEngine : Lyracist.Core.Interfaces.IMediaEngine
{
    private readonly ICDGDecoder _cdgDecoder;
    private readonly ICdgFrameScheduler _scheduler;
    private readonly IVideoBackend _video;
    private readonly ILibraryService _libraryService;
    private DispatcherTimer? _timer;
    private double _position;
    private bool _isPlaying;
    private DateTime _lastTickTime;
    private bool _isMp4Mode;
    private string _currentSongPath = string.Empty;
    private string? _activeSingerName;
    private string? _tempAudioPath;
    private string? _tempCdgPath;
    private string? _tempDir;
    private string? _loadedAudioPath;

    public event Action<ImageSource>? FrameReady;
    public event Action? Started;
    public event Action? Stopped;

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
                    Task.Run(async () =>
                    {
                        var currentPos = _video.Position;
                        bool wasPlaying = _isPlaying;

                        if (!_isMp4Mode)
                        {
                            _timer?.Stop();
                        }

                        // Load the media backend again
                        await _video.LoadAsync(_loadedAudioPath);
                        
                        // Apply equalizer, speed, volume, and the new Pitch setting
                        UpdateAudioParameters();

                        // Seek to the exact same position
                        await _video.SeekAsync(currentPos);

                        if (wasPlaying)
                        {
                            await _video.PlayAsync();
                            if (!_isMp4Mode)
                            {
                                _timer?.Start();
                            }
                        }
                    });
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

    public MediaEngine(ICDGDecoder cdgDecoder, ICdgFrameScheduler scheduler, IVideoBackend video, ILibraryService libraryService)
    {
        _cdgDecoder = cdgDecoder;
        _scheduler = scheduler;
        _video = video;
        _libraryService = libraryService;

        _video.FrameReady += OnVideoFrameReady;
        InitializePlaybackTimer();
    }

    private void InitializePlaybackTimer()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _timer.Tick += OnPlaybackTick;
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
        if (!_isPlaying || _isMp4Mode) return;

        // Sync CDG frame scheduler directly using the backend's audio position
        var audioPosition = _video.Position;
        _scheduler.Update(audioPosition);
        var frame = _scheduler.GetFrame();

        if (frame != null)
        {
            FrameReady?.Invoke(frame);
        }
    }

    public async Task LoadSong(string path)
    {
        await Stop();
        CleanUpTempFiles();
        _position = 0;

        if (string.IsNullOrEmpty(path)) return;

        _currentSongPath = path;

        string audioToLoad = path;
        string cdgToLoad = string.Empty;
        bool isZip = path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        _isMp4Mode = path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        if (isZip)
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
                System.Diagnostics.Debug.WriteLine($"Error extracting ZIP playback: {ex.Message}");
                audioToLoad = path;
            }
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
            _lastTickTime = DateTime.UtcNow;
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
        bool wasPlaying = _isPlaying;
        _isPlaying = false;
        _position = 0;
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
            System.Diagnostics.Debug.WriteLine($"Failed to delete temp audio file: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to delete temp CDG file: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Failed to delete temp directory: {ex.Message}");
        }

        _tempAudioPath = null;
        _tempCdgPath = null;
        _tempDir = null;
    }

    public void Seek(double position)
    {
        _position = position;
        if (!_isMp4Mode)
        {
            _lastTickTime = DateTime.UtcNow;
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
            return;
        }

        var songSettings = _libraryService.GetAudioSettings(_currentSongPath);
        var singerSettings = !string.IsNullOrEmpty(ActiveSingerName) && ActiveSingerName != "None"
            ? _libraryService.GetSingerSettings(ActiveSingerName)
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
            mergedVolume = songSettings.Gain;
            mergedSpeed = songSettings.Tempo;
            mergedPitch = songSettings.Key;
            mergedTreble = songSettings.Treble;
            mergedMid = songSettings.Mid;
            mergedBass = songSettings.Bass;
            mergedCompressor = songSettings.Compressor;
            mergedLimiter = songSettings.Limiter;
        }

        // Apply Singer settings (merge/fallback)
        if (singerSettings != null && singerSettings.SingerId > 0)
        {
            // Pitch offset sum (clamp to standard bounds -6 to +6)
            mergedPitch = Math.Clamp(mergedPitch + singerSettings.Key, -6, 6);
            // Speed factor multiplication (clamp to 0.5x to 2.0x)
            mergedSpeed = Math.Clamp(mergedSpeed * singerSettings.Tempo, 0.5, 2.0);
            // Volume attenuation multiplication
            mergedVolume = Math.Clamp((mergedVolume / 100.0) * (singerSettings.Gain / 100.0) * 100.0, 0.0, 100.0);
            // Equalization filters sum (clamp to -10dB to +10dB)
            mergedTreble = Math.Clamp(mergedTreble + singerSettings.Treble, -10.0, 10.0);
            mergedMid = Math.Clamp(mergedMid + singerSettings.Mid, -10.0, 10.0);
            mergedBass = Math.Clamp(mergedBass + singerSettings.Bass, -10.0, 10.0);
            // Compressor threshold: use strongest threshold (Max)
            mergedCompressor = Math.Clamp(Math.Max(mergedCompressor, singerSettings.Compressor), 0.0, 100.0);
            // Limiter threshold: use lowest (most restrictive) dB ceiling (Min)
            mergedLimiter = Math.Clamp(Math.Min(mergedLimiter, singerSettings.Limiter), -20.0, 0.0);
        }

        // Apply merged results to the unmanaged player backend
        _video.Volume = mergedVolume;
        _video.Speed = mergedSpeed;
        _video.Pitch = mergedPitch;
        _video.Treble = mergedTreble;
        _video.Mid = mergedMid;
        _video.Bass = mergedBass;
        _video.Compressor = mergedCompressor;
        _video.Limiter = mergedLimiter;
    }
}
