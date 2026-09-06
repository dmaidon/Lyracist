// Created on Sep 6, 2026 @ 12:15:00 -> ViewModel for Track Preview Player with 5s audio, waveform, spectrogram, video, and dual-audio channels
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Data.Services;
using Lyracist.Services.Store;
using Lyracist.Shared;

namespace Lyracist.ViewModels;

public partial class TrackPreviewViewModel : ObservableObject, IDisposable
{
    private readonly MediaPlayer _mediaPlayer = new();
    private readonly DispatcherTimer _positionTimer;
    private readonly List<string> _tempFiles = [];
    private CancellationTokenSource? _loadCts;
    private bool _isDisposed;
    private string? _currentSourcePath;

    public TrackPreviewViewModel()
    {
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        _mediaPlayer.MediaEnded += OnMediaEnded;
        _mediaPlayer.MediaFailed += OnMediaFailed;
    }

    [ObservableProperty]
    private bool _hasTrack;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _filename = string.Empty;

    [ObservableProperty]
    private string _provider = "Local";

    [ObservableProperty]
    private string _fileType = "Audio";

    [ObservableProperty]
    private string _durationText = "--:--";

    [ObservableProperty]
    private string _musicalKey = "--";

    [ObservableProperty]
    private string _bpmText = "--";

    [ObservableProperty]
    private string _quality = "Medium";

    [ObservableProperty]
    private string _difficulty = "Medium";

    [ObservableProperty]
    private string _vocalPresence = "no vocals";

    [ObservableProperty]
    private bool _hasDualAudio;

    [ObservableProperty]
    private bool _isVideo;

    [ObservableProperty]
    private string? _waveformImagePath;

    [ObservableProperty]
    private string? _spectrogramImagePath;

    [ObservableProperty]
    private string? _audioPreviewPath;

    [ObservableProperty]
    private string? _videoPreviewPath;

    [ObservableProperty]
    private int _selectedDualChannel = 0; // 0 = Channel A (Guide), 1 = Channel B (Instrumental)

    [ObservableProperty]
    private bool _isPlayingAudio;

    [ObservableProperty]
    private double _currentTimeSec;

    [ObservableProperty]
    private double _totalTimeSec = 5.0;

    [ObservableProperty]
    private string _timeDisplayText = "0:00 / 0:05";

    [ObservableProperty]
    private int _selectedVisualTab = 0; // 0 = Waveform, 1 = Spectrogram, 2 = Video (if applicable)

    public async Task LoadFromRecentImportAsync(PurchasedTrackItem item)
    {
        if (item == null) return;

        string durText = item.DurationSeconds.HasValue && item.DurationSeconds.Value > 0
            ? TimeSpan.FromSeconds(item.DurationSeconds.Value).ToString(@"m\:ss")
            : "--:--";

        await LoadPreviewAsync(
            primaryPath: item.FilePath,
            title: item.Title,
            artist: item.Artist,
            provider: item.Source,
            fileType: item.KaraokeType,
            hasDualAudio: item.HasDualAudio,
            key: item.Key,
            bpm: item.BPM,
            quality: item.Quality,
            difficulty: item.Difficulty,
            vocalPresence: item.VocalPresence,
            durationText: durText,
            existingWaveform: item.WaveformPath);
    }

    public async Task LoadFromBulkCandidateAsync(BulkImportCandidate candidate)
    {
        if (candidate == null) return;

        await LoadPreviewAsync(
            primaryPath: candidate.PrimaryFilePath,
            title: candidate.Title,
            artist: candidate.Artist,
            provider: candidate.Provider,
            fileType: candidate.FileType,
            hasDualAudio: candidate.HasDualAudio,
            key: candidate.Key,
            bpm: candidate.Bpm,
            quality: candidate.Quality,
            difficulty: candidate.Difficulty,
            vocalPresence: candidate.VocalPresence,
            durationText: candidate.DurationText,
            existingWaveform: null);
    }

    public async Task LoadPreviewAsync(
        string primaryPath,
        string? title = null,
        string? artist = null,
        string? provider = null,
        string? fileType = null,
        bool hasDualAudio = false,
        string? key = null,
        double? bpm = null,
        string? quality = null,
        string? difficulty = null,
        string? vocalPresence = null,
        string? durationText = null,
        string? existingWaveform = null)
    {
        if (string.IsNullOrWhiteSpace(primaryPath) || !File.Exists(primaryPath))
        {
            HasTrack = false;
            return;
        }

        StopAudio();
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        _currentSourcePath = primaryPath;
        HasTrack = true;
        IsLoading = true;
        StatusMessage = "Extracting preview media...";

        Title = !string.IsNullOrWhiteSpace(title) ? title : Path.GetFileNameWithoutExtension(primaryPath);
        Artist = !string.IsNullOrWhiteSpace(artist) ? artist : "Unknown Artist";
        Filename = Path.GetFileName(primaryPath);
        Provider = !string.IsNullOrWhiteSpace(provider) ? provider : "Local";
        FileType = !string.IsNullOrWhiteSpace(fileType) ? fileType : Path.GetExtension(primaryPath).TrimStart('.').ToUpperInvariant();
        DurationText = !string.IsNullOrWhiteSpace(durationText) ? durationText : "--:--";
        MusicalKey = !string.IsNullOrWhiteSpace(key) ? key : "--";
        BpmText = bpm.HasValue && bpm.Value > 0 ? $"{bpm.Value:F0}" : "--";
        Quality = !string.IsNullOrWhiteSpace(quality) ? quality : "Medium";
        Difficulty = !string.IsNullOrWhiteSpace(difficulty) ? difficulty : "Medium";
        VocalPresence = !string.IsNullOrWhiteSpace(vocalPresence) ? vocalPresence : "no vocals";
        HasDualAudio = hasDualAudio;

        string ext = Path.GetExtension(primaryPath).ToLowerInvariant();
        IsVideo = ext == ".mp4";

        WaveformImagePath = null;
        SpectrogramImagePath = null;
        AudioPreviewPath = null;
        VideoPreviewPath = null;
        SelectedDualChannel = 0;
        CurrentTimeSec = 0;
        TimeDisplayText = "0:00 / 0:05";

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_Previews");
            Directory.CreateDirectory(tempDir);

            string fileGuid = Guid.NewGuid().ToString("N")[..8];

            // 1. Audio Preview (first 5 seconds)
            string audioPreviewFile = Path.Combine(tempDir, $"{fileGuid}_preview.mp3");
            _tempFiles.Add(audioPreviewFile);

            int? channelParam = HasDualAudio ? SelectedDualChannel : null;
            bool audioOk = await FFmpegService.GenerateAudioPreviewAsync(primaryPath, audioPreviewFile, channelParam);
            if (audioOk && File.Exists(audioPreviewFile) && !token.IsCancellationRequested)
            {
                AudioPreviewPath = audioPreviewFile;
                _mediaPlayer.Open(new Uri(audioPreviewFile));
            }

            // 2. Waveform Preview
            if (!string.IsNullOrWhiteSpace(existingWaveform) && File.Exists(existingWaveform))
            {
                WaveformImagePath = existingWaveform;
            }
            else
            {
                string waveFile = Path.Combine(tempDir, $"{fileGuid}_waveform.png");
                _tempFiles.Add(waveFile);
                bool waveOk = await FFmpegService.GenerateWaveformPreviewAsync(primaryPath, waveFile);
                if (waveOk && File.Exists(waveFile) && !token.IsCancellationRequested)
                {
                    WaveformImagePath = waveFile;
                }
            }

            // 3. Spectrogram Preview
            string specFile = Path.Combine(tempDir, $"{fileGuid}_spectrogram.png");
            _tempFiles.Add(specFile);
            bool specOk = await FFmpegService.GenerateSpectrogramAsync(primaryPath, specFile);
            if (specOk && File.Exists(specFile) && !token.IsCancellationRequested)
            {
                SpectrogramImagePath = specFile;
            }

            // 4. Video Preview (if MP4)
            if (IsVideo)
            {
                string videoFile = Path.Combine(tempDir, $"{fileGuid}_video.mp4");
                _tempFiles.Add(videoFile);
                bool vidOk = await FFmpegService.GenerateVideoPreviewAsync(primaryPath, videoFile);
                if (vidOk && File.Exists(videoFile) && !token.IsCancellationRequested)
                {
                    VideoPreviewPath = videoFile;
                }
            }

            StatusMessage = "Preview ready";
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview error: {ex.Message}";
            Globals.LogError("Lyracist", $"Failed to generate track preview for {primaryPath}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PlayAudio()
    {
        if (string.IsNullOrWhiteSpace(AudioPreviewPath) || !File.Exists(AudioPreviewPath)) return;

        try
        {
            _mediaPlayer.Play();
            _positionTimer.Start();
            IsPlayingAudio = true;
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Failed to start preview audio playback", ex);
        }
    }

    [RelayCommand]
    private void PauseAudio()
    {
        try
        {
            _mediaPlayer.Pause();
            _positionTimer.Stop();
            IsPlayingAudio = false;
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Failed to pause preview audio playback", ex);
        }
    }

    [RelayCommand]
    private void TogglePlayAudio()
    {
        if (IsPlayingAudio)
        {
            PauseAudio();
        }
        else
        {
            PlayAudio();
        }
    }

    [RelayCommand]
    private void StopAudio()
    {
        try
        {
            _positionTimer.Stop();
            _mediaPlayer.Stop();
            IsPlayingAudio = false;
            CurrentTimeSec = 0;
            TimeDisplayText = "0:00 / 0:05";
        }
        catch
        {
            // Ignore on stop
        }
    }

    [RelayCommand]
    private async Task SelectDualChannelAsync(int channel)
    {
        if (SelectedDualChannel == channel) return;
        SelectedDualChannel = channel;

        if (string.IsNullOrWhiteSpace(_currentSourcePath) || !File.Exists(_currentSourcePath)) return;

        StopAudio();
        IsLoading = true;
        StatusMessage = channel == 0 ? "Switching to Channel A (Guide Vocals)..." : "Switching to Channel B (Instrumental)...";

        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "Lyracist_Previews");
            string audioPreviewFile = Path.Combine(tempDir, $"{Guid.NewGuid():N[..8]}_ch{channel}.mp3");
            _tempFiles.Add(audioPreviewFile);

            bool ok = await FFmpegService.GenerateAudioPreviewAsync(_currentSourcePath, audioPreviewFile, channel);
            if (ok && File.Exists(audioPreviewFile))
            {
                AudioPreviewPath = audioPreviewFile;
                _mediaPlayer.Open(new Uri(audioPreviewFile));
                StatusMessage = channel == 0 ? "Channel A (Guide Vocals) ready" : "Channel B (Instrumental) ready";
                PlayAudio();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Channel switch error: {ex.Message}";
            Globals.LogError("Lyracist", "Failed to switch dual audio channel", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClosePreview()
    {
        StopAudio();
        HasTrack = false;
        _currentSourcePath = null;
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        try
        {
            double sec = _mediaPlayer.Position.TotalSeconds;
            if (sec > TotalTimeSec) sec = TotalTimeSec;
            CurrentTimeSec = sec;

            int m = (int)sec / 60;
            int s = (int)sec % 60;
            TimeDisplayText = $"{m}:{s:D2} / 0:05";
        }
        catch
        {
            // Timer tick error ignored
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        StopAudio();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        StopAudio();
        StatusMessage = "Audio playback failed";
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _positionTimer.Stop();
        _positionTimer.Tick -= OnPositionTimerTick;

        try
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Close();
        }
        catch
        {
            // Ignore
        }

        // Clean up temp preview files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
        _tempFiles.Clear();
    }
}
