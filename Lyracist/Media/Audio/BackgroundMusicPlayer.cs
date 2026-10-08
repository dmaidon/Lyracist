// Edited on Oct 7, 2026 @ 19:43:00 -> Add per-track loudness leveling, automix crossfade, cue points, and smart shuffle
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Lyracist.Shared;

namespace Lyracist.Media.Audio;

/// <summary>
/// Loops an ordered list of audio tracks (Opening or Fill-In background music)
/// on a dedicated LibVLC instance, independent from the karaoke MediaEngine.
/// Crossfades between tracks automatically and supports ducking for live
/// announcements without stopping playback.
/// </summary>
public class BackgroundMusicPlayer : IDisposable
{
    // Standard 10-band graphic EQ (31.25Hz .. 16kHz). Bass/Treble knobs move
    // the low and high bands together rather than exposing all 10 to the KJ.
    private static readonly uint[] BassBands = [0, 1, 2];
    private static readonly uint[] TrebleBands = [7, 8, 9];

    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _playerA;
    private readonly MediaPlayer _playerB;
    private MediaPlayer _active;
    private MediaPlayer _inactive;
    private readonly Equalizer _equalizer;

    private readonly DispatcherTimer _monitorTimer;
    private List<FillInTrack> _tracks = [];
    private int _currentIndex = -1;
    private int _pendingNextIndex = -1;
    private bool _isCrossfading;
    private DateTime _crossfadeStart;
    private double _currentCrossfadeDurationMs;

    private double _baseVolume = 70;
    private double _duckMultiplier = 1.0;
    private double _bassDb;
    private double _trebleDb;
    private double _preampDb;

    private double _activeTrackGain = 1.0;
    private double _inactiveTrackGain = 1.0;
    private string? _activeTrackPath;
    private string? _inactiveTrackPath;

    private readonly Queue<string> _recentHistory = new();
    private const int MaxHistoryCount = 5;

    public bool IsPlaying { get; private set; }
    public bool IsDucked => _duckMultiplier < 1.0;

    /// <summary>Configurable crossfade duration between tracks.</summary>
    public TimeSpan CrossfadeDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Automix elapsed track cutoff in seconds (0 = disabled/off).</summary>
    public int MaxTrackSeconds { get; set; } = 0;

    /// <summary>When true, uses smart shuffle (BPM, artist, history) instead of sequential wrap.</summary>
    public bool FillInSmartShuffle { get; set; } = false;

    /// <summary>Optional lookup for track integrated loudness (LUFS).</summary>
    public Func<string, double?>? LoudnessLookup { get; set; }

    /// <summary>Callback kicked off when a track lacks a loudness measurement.</summary>
    public Action<string>? RequestLoudnessMeasurement { get; set; }

    /// <summary>Optional lookup for track mix-in cue point in milliseconds.</summary>
    public Func<string, int?>? MixInLookup { get; set; }

    /// <summary>Optional lookup for track mix-out cue point in milliseconds.</summary>
    public Func<string, int?>? MixOutLookup { get; set; }

    private string? _audioDeviceId;
    public string? AudioDeviceId
    {
        get => _audioDeviceId;
        set
        {
            _audioDeviceId = value;
            ApplyAudioDevice();
        }
    }

    private void ApplyAudioDevice()
    {
        _playerA.SetAudioOutput("mmdevice");
        _playerB.SetAudioOutput("mmdevice");
        if (!string.IsNullOrEmpty(_audioDeviceId) && _audioDeviceId != "Default System Device")
        {
            _playerA.SetOutputDevice(_audioDeviceId);
            _playerB.SetOutputDevice(_audioDeviceId);
        }
    }

    /// <summary>
    /// When true (default) the playlist wraps around forever. When false,
    /// playback stops after the last track and PlaybackFinished fires —
    /// used for one-shot Special Occasion tracks.
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>Raised when a non-looping playlist reaches its natural end (not on manual Stop).</summary>
    public event EventHandler? PlaybackFinished;

    public double Volume
    {
        get => _baseVolume;
        set
        {
            _baseVolume = Math.Clamp(value, 0, 200);
            if (!_isCrossfading)
            {
                _active.Volume = CurrentTargetVolume;
            }
        }
    }

    /// <summary>Gain applied to the bottom three EQ bands (31-125 Hz), in dB (-20..20).</summary>
    public double BassDb
    {
        get => _bassDb;
        set { _bassDb = Math.Clamp(value, -20, 20); ApplyEqualizer(); }
    }

    /// <summary>Gain applied to the top three EQ bands (4-16 kHz), in dB (-20..20).</summary>
    public double TrebleDb
    {
        get => _trebleDb;
        set { _trebleDb = Math.Clamp(value, -20, 20); ApplyEqualizer(); }
    }

    /// <summary>Overall pre-amplifier gain, in dB (-20..20).</summary>
    public double PreampDb
    {
        get => _preampDb;
        set { _preampDb = Math.Clamp(value, -20, 20); ApplyEqualizer(); }
    }

    private int CurrentTargetVolume => GetTargetVolume(_activeTrackGain);
    private int InactiveTargetVolume => GetTargetVolume(_inactiveTrackGain);

    private int GetTargetVolume(double trackGain) =>
        (int)Math.Clamp(Math.Round(_baseVolume * _duckMultiplier * trackGain), 0, 200);

    public BackgroundMusicPlayer()
    {
        Lyracist.Core.Helpers.AppLogger.InitializeLibVlc();

        _libVLC = new LibVLC();
        _playerA = new MediaPlayer(_libVLC);
        _playerB = new MediaPlayer(_libVLC);
        _active = _playerA;
        _inactive = _playerB;
        _equalizer = new Equalizer();
        ApplyAudioDevice();

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _monitorTimer.Tick += OnMonitorTick;
    }

    public void LoadPlaylist(IReadOnlyList<string> trackPaths)
    {
        LoadPlaylist(trackPaths.Select(p => new FillInTrack(p)).ToList());
    }

    public void LoadPlaylist(IReadOnlyList<FillInTrack> tracks)
    {
        _tracks = [.. tracks];
        ShufflePlaylist();
        if (_currentIndex >= _tracks.Count)
        {
            _currentIndex = _tracks.Count > 0 ? 0 : -1;
        }

        // Validate paths asynchronously in background to prune invalid entries
        var tracksCopy = tracks.ToList();
        System.Threading.Tasks.Task.Run(() =>
        {
            var validTracks = tracksCopy.Where(t => File.Exists(t.Path)).ToList();
            System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                FillInTrack? currentTrack = _currentIndex >= 0 && _currentIndex < _tracks.Count ? _tracks[_currentIndex] : null;

                _tracks = validTracks;
                ShufflePlaylist();

                if (currentTrack != null)
                {
                    int found = _tracks.FindIndex(t => string.Equals(t.Path, currentTrack.Path, StringComparison.OrdinalIgnoreCase));
                    if (found >= 0)
                    {
                        _currentIndex = found;
                        return;
                    }
                }
                if (_currentIndex >= _tracks.Count)
                {
                    _currentIndex = _tracks.Count > 0 ? 0 : -1;
                }
            }));
        });
    }

    private static readonly Random _rng = new();

    private void ShufflePlaylist()
    {
        if (_tracks == null || _tracks.Count <= 1) return;
        for (int i = _tracks.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_tracks[i], _tracks[j]) = (_tracks[j], _tracks[i]);
        }
    }

    /// <param name="startTrackPath">
    /// When provided and found in the loaded playlist, playback starts at that
    /// track instead of wherever the internal (possibly shuffled) index points.
    /// </param>
    public void Play(string? startTrackPath = null)
    {
        if (IsPlaying || _tracks.Count == 0) return;

        if (!string.IsNullOrEmpty(startTrackPath))
        {
            int requestedIndex = _tracks.FindIndex(t => string.Equals(t.Path, startTrackPath, StringComparison.OrdinalIgnoreCase));
            if (requestedIndex >= 0)
            {
                _currentIndex = requestedIndex;
            }
        }

        if (_currentIndex < 0) _currentIndex = 0;
        string path = _tracks[_currentIndex].Path;
        _activeTrackPath = path;
        _activeTrackGain = ComputeTrackGain(path);
        PlayTrack(_active, path);
        _active.Volume = CurrentTargetVolume;
        RecordHistory(path);
        _monitorTimer.Start();
        IsPlaying = true;
    }

    /// <summary>Resumes from wherever playback left off, or starts fresh if nothing was ever loaded.</summary>
    public void Resume()
    {
        if (IsPlaying || _tracks.Count == 0) return;

        if (_currentIndex < 0)
        {
            Play();
            return;
        }

        IsPlaying = true;
        string path = _tracks[_currentIndex].Path;
        _activeTrackPath = path;
        _activeTrackGain = ComputeTrackGain(path);
        _active.Volume = CurrentTargetVolume;
        _active.Play();
        _monitorTimer.Start();
    }

    public void Pause()
    {
        if (!IsPlaying) return;

        IsPlaying = false;
        _monitorTimer.Stop();
        _active.Pause();
        if (_isCrossfading)
        {
            _inactive.Pause();
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        _isCrossfading = false;
        _currentCrossfadeDurationMs = 0;
        _monitorTimer.Stop();
        _active.Stop();
        _inactive.Stop();
        _currentIndex = _tracks.Count > 0 ? 0 : -1;
    }

    /// <summary>Temporarily lowers volume (e.g. so the KJ can talk over the mic) without stopping playback.</summary>
    public void Duck(double multiplier = 0.35)
    {
        _duckMultiplier = Math.Clamp(multiplier, 0, 1);
        if (!_isCrossfading)
        {
            _active.Volume = CurrentTargetVolume;
        }
    }

    public void Unduck()
    {
        _duckMultiplier = 1.0;
        if (!_isCrossfading)
        {
            _active.Volume = CurrentTargetVolume;
        }
    }

    private void PlayTrack(MediaPlayer player, string path)
    {
        var media = new LibVLCSharp.Shared.Media(_libVLC, new Uri(path));
        var oldMedia = player.Media;
        player.Media = media;
        oldMedia?.Dispose();
        player.SetAudioOutput("mmdevice");
        if (!string.IsNullOrEmpty(_audioDeviceId) && _audioDeviceId != "Default System Device")
        {
            player.SetOutputDevice(_audioDeviceId);
        }
        player.Play();
        // Re-applied per track: LibVLC associates the equalizer with the
        // player's current media, so a fresh load can drop the last setting.
        player.SetEqualizer(_equalizer);
    }

    public void UpdatePreamp() => ApplyEqualizer();

    private void ApplyEqualizer()
    {
        float masterBoost = (float)Lyracist.Core.Helpers.AppSettings.MasterOutputBoostDb;
        if (Lyracist.Core.Helpers.AppSettings.IsHardwareMixerMode)
        {
            _equalizer.SetPreamp(masterBoost);
            for (uint i = 0; i < 10; i++)
            {
                _equalizer.SetAmp(0.0f, i);
            }
        }
        else
        {
            _equalizer.SetPreamp((float)Math.Clamp(_preampDb + masterBoost, -20.0, 20.0));
            foreach (uint band in BassBands) _equalizer.SetAmp((float)_bassDb, band);
            foreach (uint band in TrebleBands) _equalizer.SetAmp((float)_trebleDb, band);
        }

        _playerA.SetEqualizer(_equalizer);
        _playerB.SetEqualizer(_equalizer);
    }

    private void OnMonitorTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || _tracks.Count == 0) return;

        if (_isCrossfading)
        {
            UpdateCrossfade();
            return;
        }

        long length = _active.Length;
        long time = _active.Time;

        // Non-looping playlists play the final track to its true end instead
        // of crossfading back to the start.
        if (!Loop && _currentIndex >= _tracks.Count - 1)
        {
            bool ended = _active.State == VLCState.Ended
                         || (length > 0 && length - time <= 250);
            if (ended)
            {
                Stop();
                PlaybackFinished?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        // Short track handling: if length is known and shorter than the crossfade duration,
        // use an effective crossfade of min(CrossfadeDuration, length / 2) with a minimum of 250ms.
        double effectiveCrossfadeMs = CrossfadeDuration.TotalMilliseconds;
        if (length > 0 && length < CrossfadeDuration.TotalMilliseconds)
        {
            effectiveCrossfadeMs = Math.Max(250, length / 2.0);
        }

        // Elapsed-time automix: if MaxTrackSeconds is enabled (> 0) and we are looping,
        // fade to next after MaxTrackSeconds elapsed time.
        if (Loop && MaxTrackSeconds > 0 && time >= MaxTrackSeconds * 1000)
        {
            _currentCrossfadeDurationMs = effectiveCrossfadeMs;
            StartCrossfade();
            return;
        }

        // Mix-out cue point (Feature E): if MixOut is set, trigger crossfade at (MixOutMs - crossfade)
        // Otherwise trigger at (length - crossfade).
        long triggerPointMs = (long)(length - effectiveCrossfadeMs);
        if (_currentIndex >= 0 && _currentIndex < _tracks.Count)
        {
            int? mixOut = MixOutLookup?.Invoke(_tracks[_currentIndex].Path);
            if (mixOut.HasValue && mixOut.Value > 0)
            {
                triggerPointMs = (long)(mixOut.Value - effectiveCrossfadeMs);
            }
        }

        if (length > 0 && time >= triggerPointMs)
        {
            _currentCrossfadeDurationMs = effectiveCrossfadeMs;
            StartCrossfade();
        }
    }

    private void StartCrossfade()
    {
        if (_tracks.Count == 0) return;

        if (FillInSmartShuffle && _tracks.Count > 1)
        {
            var currentTrack = _currentIndex >= 0 && _currentIndex < _tracks.Count ? _tracks[_currentIndex] : null;
            _pendingNextIndex = NextTrackSelector.SelectNextIndex(currentTrack, _tracks, _recentHistory, _rng);
            if (_pendingNextIndex < 0) _pendingNextIndex = (_currentIndex + 1) % _tracks.Count;
        }
        else
        {
            _pendingNextIndex = (_currentIndex + 1) % _tracks.Count;
        }

        string nextPath = _tracks[_pendingNextIndex].Path;
        _inactiveTrackPath = nextPath;
        _inactiveTrackGain = ComputeTrackGain(nextPath);

        PlayTrack(_inactive, nextPath);
        _inactive.Volume = 0;

        // Cue point (MixIn): seek incoming track if cue point is set
        int? mixIn = MixInLookup?.Invoke(nextPath);
        if (mixIn.HasValue && mixIn.Value > 0)
        {
            _inactive.Time = mixIn.Value;
        }

        RecordHistory(nextPath);
        _isCrossfading = true;
        _crossfadeStart = DateTime.UtcNow;
    }

    private void UpdateCrossfade()
    {
        double duration = _currentCrossfadeDurationMs > 0 ? _currentCrossfadeDurationMs : CrossfadeDuration.TotalMilliseconds;
        double elapsed = (DateTime.UtcNow - _crossfadeStart).TotalMilliseconds;
        double t = Math.Clamp(elapsed / duration, 0, 1);

        int activeMax = CurrentTargetVolume;
        int inactiveMax = InactiveTargetVolume;
        _active.Volume = (int)(activeMax * (1 - t));
        _inactive.Volume = (int)(inactiveMax * t);

        if (t >= 1.0)
        {
            _active.Stop();
            (_active, _inactive) = (_inactive, _active);
            _currentIndex = _pendingNextIndex;
            (_activeTrackPath, _inactiveTrackPath) = (_inactiveTrackPath, null);
            (_activeTrackGain, _inactiveTrackGain) = (_inactiveTrackGain, 1.0);
            _isCrossfading = false;
            _currentCrossfadeDurationMs = 0;
        }
    }

    private double ComputeTrackGain(string path)
    {
        if (Lyracist.Core.Helpers.AppSettings.IsHardwareMixerMode || !Lyracist.Core.Helpers.AppSettings.NormalizeVolumeEnabled)
        {
            return 1.0;
        }

        double? measured = LoudnessLookup?.Invoke(path);
        if (measured.HasValue)
        {
            return LoudnessNormalizer.ComputeGainFactor(
                measured,
                Lyracist.Core.Helpers.AppSettings.TargetLoudnessLufs,
                true,
                false);
        }

        RequestLoudnessMeasurement?.Invoke(path);
        return 1.0;
    }

    private void RecordHistory(string path)
    {
        _recentHistory.Enqueue(path);
        while (_recentHistory.Count > MaxHistoryCount)
        {
            _recentHistory.Dequeue();
        }
    }

    public void Dispose()
    {
        _monitorTimer.Stop();
        _playerA.Dispose();
        _playerB.Dispose();
        _equalizer.Dispose();
        _libVLC.Dispose();
        GC.SuppressFinalize(this);
    }
}
