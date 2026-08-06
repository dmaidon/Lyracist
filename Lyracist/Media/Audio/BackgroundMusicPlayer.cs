// Edited on Aug 6, 2026 @ 07:01:27 -> Dispose the outgoing Media instance on track change to stop leaking native libVLC handles
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace Lyracist.Media.Audio;

/// <summary>
/// Loops an ordered list of audio tracks (Opening or Fill-In background music)
/// on a dedicated LibVLC instance, independent from the karaoke MediaEngine.
/// Crossfades between tracks automatically and supports ducking for live
/// announcements without stopping playback.
/// </summary>
public class BackgroundMusicPlayer : IDisposable
{
    private static readonly TimeSpan CrossfadeDuration = TimeSpan.FromSeconds(5);

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
    private List<string> _playlist = [];
    private int _currentIndex = -1;
    private int _pendingNextIndex = -1;
    private bool _isCrossfading;
    private DateTime _crossfadeStart;

    private double _baseVolume = 70;
    private double _duckMultiplier = 1.0;
    private double _bassDb;
    private double _trebleDb;
    private double _preampDb;

    public bool IsPlaying { get; private set; }
    public bool IsDucked => _duckMultiplier < 1.0;

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
        if (!string.IsNullOrEmpty(_audioDeviceId) && _audioDeviceId != "Default System Device")
        {
            _playerA.SetAudioOutput("mmdevice");
            _playerA.SetOutputDevice(_audioDeviceId);
            _playerB.SetAudioOutput("mmdevice");
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
            _baseVolume = Math.Clamp(value, 0, 100);
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

    private int CurrentTargetVolume => (int)Math.Round(_baseVolume * _duckMultiplier);

    public BackgroundMusicPlayer()
    {
        Lyracist.Core.Helpers.AppLogger.InitializeLibVlc();

        _libVLC = new LibVLC();
        _playerA = new MediaPlayer(_libVLC);
        _playerB = new MediaPlayer(_libVLC);
        _active = _playerA;
        _inactive = _playerB;
        _equalizer = new Equalizer();

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _monitorTimer.Tick += OnMonitorTick;
    }

    public void LoadPlaylist(IReadOnlyList<string> trackPaths)
    {
        _playlist = [.. trackPaths];
        ShufflePlaylist();
        if (_currentIndex >= _playlist.Count)
        {
            _currentIndex = _playlist.Count > 0 ? 0 : -1;
        }

        // Validate paths asynchronously in background to prune invalid entries
        var pathsCopy = trackPaths.ToList();
        System.Threading.Tasks.Task.Run(() =>
        {
            var validPaths = pathsCopy.Where(File.Exists).ToList();
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Preserve current track if it's still valid
                string? currentTrack = _currentIndex >= 0 && _currentIndex < _playlist.Count ? _playlist[_currentIndex] : null;

                _playlist = validPaths;
                ShufflePlaylist();

                if (currentTrack != null && _playlist.Contains(currentTrack))
                {
                    _currentIndex = _playlist.IndexOf(currentTrack);
                }
                else if (_currentIndex >= _playlist.Count)
                {
                    _currentIndex = _playlist.Count > 0 ? 0 : -1;
                }
            }));
        });
    }

    private static readonly Random _rng = new();

    private void ShufflePlaylist()
    {
        if (_playlist == null || _playlist.Count <= 1) return;
        for (int i = _playlist.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_playlist[i], _playlist[j]) = (_playlist[j], _playlist[i]);
        }
    }

    /// <param name="startTrackPath">
    /// When provided and found in the loaded playlist, playback starts at that
    /// track instead of wherever the internal (possibly shuffled) index points.
    /// </param>
    public void Play(string? startTrackPath = null)
    {
        if (IsPlaying || _playlist.Count == 0) return;

        if (!string.IsNullOrEmpty(startTrackPath))
        {
            int requestedIndex = _playlist.FindIndex(p => string.Equals(p, startTrackPath, StringComparison.OrdinalIgnoreCase));
            if (requestedIndex >= 0)
            {
                _currentIndex = requestedIndex;
            }
        }

        if (_currentIndex < 0) _currentIndex = 0;
        PlayTrack(_active, _playlist[_currentIndex]);
        _active.Volume = CurrentTargetVolume;
        _monitorTimer.Start();
        IsPlaying = true;
    }

    /// <summary>Resumes from wherever playback left off, or starts fresh if nothing was ever loaded.</summary>
    public void Resume()
    {
        if (IsPlaying || _playlist.Count == 0) return;

        if (_currentIndex < 0)
        {
            Play();
            return;
        }

        IsPlaying = true;
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
        _monitorTimer.Stop();
        _active.Stop();
        _inactive.Stop();
        _currentIndex = _playlist.Count > 0 ? 0 : -1;
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
        if (!string.IsNullOrEmpty(_audioDeviceId) && _audioDeviceId != "Default System Device")
        {
            player.SetAudioOutput("mmdevice");
            player.SetOutputDevice(_audioDeviceId);
        }
        player.Play();
        // Re-applied per track: LibVLC associates the equalizer with the
        // player's current media, so a fresh load can drop the last setting.
        player.SetEqualizer(_equalizer);
    }

    private void ApplyEqualizer()
    {
        if (Lyracist.Core.Helpers.AppSettings.IsHardwareMixerMode)
        {
            _equalizer.SetPreamp(0.0f);
            for (uint i = 0; i < 10; i++)
            {
                _equalizer.SetAmp(0.0f, i);
            }
        }
        else
        {
            _equalizer.SetPreamp((float)_preampDb);
            foreach (uint band in BassBands) _equalizer.SetAmp((float)_bassDb, band);
            foreach (uint band in TrebleBands) _equalizer.SetAmp((float)_trebleDb, band);
        }

        _playerA.SetEqualizer(_equalizer);
        _playerB.SetEqualizer(_equalizer);
    }

    private void OnMonitorTick(object? sender, EventArgs e)
    {
        if (!IsPlaying || _playlist.Count == 0) return;

        if (_isCrossfading)
        {
            UpdateCrossfade();
            return;
        }

        long length = _active.Length;
        long time = _active.Time;

        // Non-looping playlists play the final track to its true end instead
        // of crossfading back to the start.
        if (!Loop && _currentIndex >= _playlist.Count - 1)
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

        if (length > 0 && length - time <= CrossfadeDuration.TotalMilliseconds)
        {
            StartCrossfade();
        }
    }

    private void StartCrossfade()
    {
        _pendingNextIndex = (_currentIndex + 1) % _playlist.Count;
        PlayTrack(_inactive, _playlist[_pendingNextIndex]);
        _inactive.Volume = 0;
        _isCrossfading = true;
        _crossfadeStart = DateTime.UtcNow;
    }

    private void UpdateCrossfade()
    {
        double elapsed = (DateTime.UtcNow - _crossfadeStart).TotalMilliseconds;
        double t = Math.Clamp(elapsed / CrossfadeDuration.TotalMilliseconds, 0, 1);

        int maxVolume = CurrentTargetVolume;
        _active.Volume = (int)(maxVolume * (1 - t));
        _inactive.Volume = (int)(maxVolume * t);

        if (t >= 1.0)
        {
            _active.Stop();
            (_active, _inactive) = (_inactive, _active);
            _currentIndex = _pendingNextIndex;
            _isCrossfading = false;
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
