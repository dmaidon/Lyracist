// Edited on Sep 9, 2026 @ 16:33:00 -> Implement RefreshOutputSettings to update BGM player preamps
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Lyracist.Core.Interfaces;
using Lyracist.Media.Audio;
using Lyracist.Services.Display;
using Lyracist.ViewModels;

namespace Lyracist.Services.Media;

/// <summary>
/// Wires the Opening, Fill-In, and End-of-Rotation background music players
/// to the karaoke MediaEngine and singer rotation lifecycle: Opening music
/// stops for good once the first singer starts, Fill-In music pauses/resumes
/// between every singer, and End-of-Rotation music takes over once the
/// rotation queue empties out for the night (handing back to Fill-In if a
/// latecomer signs up and the queue fills again).
/// </summary>
public class ShowFlowService : IShowFlowService
{
    private readonly BackgroundMusicPlayer _opening;
    private readonly BackgroundMusicPlayer _fillIn;
    private readonly BackgroundMusicPlayer _endRotation;
    private readonly BackgroundMusicPlayer _occasion;
    private readonly IPlaylistService _playlists;
    private readonly IDisplayService _display;
    private readonly RotationViewModel _rotation;

    private System.Threading.CancellationTokenSource? _fillInDelayCts;
    private System.Threading.CancellationTokenSource? _endRotationDelayCts;
    private System.Threading.CancellationTokenSource? _unduckTimerCts;
    private static readonly Random _rng = new();

    public bool IsOpeningPlaying => _opening.IsPlaying;
    public bool IsFillInPlaying => _fillIn.IsPlaying;
    public bool IsFillInDucked => _fillIn.IsDucked;
    public bool IsEndRotationPlaying => _endRotation.IsPlaying;
    public bool IsOccasionPlaying => _occasion.IsPlaying;

    public ShowFlowService(
        [FromKeyedServices("Opening")] BackgroundMusicPlayer opening,
        [FromKeyedServices("FillIn")] BackgroundMusicPlayer fillIn,
        [FromKeyedServices("EndRotation")] BackgroundMusicPlayer endRotation,
        [FromKeyedServices("Occasion")] BackgroundMusicPlayer occasion,
        IPlaylistService playlists,
        IMediaEngine mediaEngine,
        IDisplayService displayService,
        RotationViewModel rotation)
    {
        _opening = opening;
        _fillIn = fillIn;
        _endRotation = endRotation;
        _occasion = occasion;
        _playlists = playlists;
        _display = displayService;
        _rotation = rotation;

        RefreshPlaylists();

        _rotation.RotationStateChanged += OnRotationStateChanged;

        // Occasion tracks play once; when one finishes naturally, drop the
        // banner and hand the room back to fill-in music.
        _occasion.PlaybackFinished += (_, _) =>
        {
            _display.SetRotationAnnouncement(string.Empty, false);
            _fillIn.Resume();
        };

        // A singer's song starting means the opening set is over for the
        // night, and any fill-in or occasion track playing in the gap needs
        // to duck out.
        mediaEngine.Started += OnKaraokeTrackStarted;

        // The singer's song ending re-opens the gap for fill-in music.
        mediaEngine.Stopped += () =>
        {
            ScheduleFillInMusic();
        };

        // The rotation queue running dry means the night's singers are done:
        // stop the between-singer fill-in music and send the room off with
        // the end-of-rotation playlist after a 10-second delay.
        displayService.RotationCompleted += () => ScheduleEndRotationMusic();

        // A latecomer joining an empty queue means the show isn't actually
        // over -- cut the send-off music and go back to normal fill-in.
        displayService.RotationResumed += () =>
        {
            CancelFillInSchedules();
            _endRotation.Stop();
            _fillIn.Resume();
            _fillIn.Unduck();
        };

        SetBgmAudioDevice(Lyracist.Core.Helpers.AppSettings.SelectedBgmAudioDevice);
    }

    public void RefreshOutputSettings()
    {
        _opening.UpdatePreamp();
        _fillIn.UpdatePreamp();
        _endRotation.UpdatePreamp();
        _occasion.UpdatePreamp();
    }

    public void RefreshPlaylists()
    {
        var opening = _playlists.GetOpeningPlaylist();
        _opening.LoadPlaylist(opening.ConvertAll(t => t.AudioPath));

        var fillIn = _playlists.GetFillInPlaylist();
        _fillIn.LoadPlaylist(fillIn.ConvertAll(t => t.AudioPath));

        var endRotation = _playlists.GetEndRotationPlaylist();
        _endRotation.LoadPlaylist(endRotation.ConvertAll(t => t.AudioPath));
    }

    public void StartOpeningMusic(string? startTrackPath = null) => _opening.Play(startTrackPath);
    public void StopOpeningMusic() => _opening.Stop();

    public void PlayFillIn(string? startTrackPath = null) => _fillIn.Play(startTrackPath);
    public void StopFillIn() => _fillIn.Stop();

    public void DuckFillIn() => _fillIn.Duck();
    public void UnduckFillIn() => _fillIn.Unduck();

    public void StartEndRotationMusic(string? startTrackPath = null) => _endRotation.Play(startTrackPath);
    public void StopEndRotationMusic() => _endRotation.Stop();

    public async void PlayOccasion(string occasionName, string filePath, double bassDb, double trebleDb, double preampDb)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        _occasion.Stop();
        _fillIn.Pause();

        _occasion.BassDb = bassDb;
        _occasion.TrebleDb = trebleDb;
        _occasion.PreampDb = preampDb;

        string finalPath = filePath;

        if (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open external link: {ex.Message}", "Browser Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            _display.SetRotationAnnouncement($"🎉 {occasionName}!", true);
            return;
        }



        _occasion.LoadPlaylist(new[] { finalPath });
        _occasion.Play();

        _display.SetRotationAnnouncement($"🎉 {occasionName}!", true);
    }

    public void StopOccasion()
    {
        _occasion.Stop();
        _display.SetRotationAnnouncement(string.Empty, false);
        _fillIn.Resume();
    }

    public void SetBgmAudioDevice(string deviceId)
    {
        _opening.AudioDeviceId = deviceId;
        _fillIn.AudioDeviceId = deviceId;
        _endRotation.AudioDeviceId = deviceId;
        _occasion.AudioDeviceId = deviceId;
    }

    public void SetOpeningVolume(double volume) => _opening.Volume = volume;
    public void SetFillInVolume(double volume) => _fillIn.Volume = volume;
    public void SetEndRotationVolume(double volume) => _endRotation.Volume = volume;

    public void SetOpeningTone(double bassDb, double trebleDb, double preampDb)
    {
        _opening.BassDb = bassDb;
        _opening.TrebleDb = trebleDb;
        _opening.PreampDb = preampDb;
    }

    public void SetFillInTone(double bassDb, double trebleDb, double preampDb)
    {
        _fillIn.BassDb = bassDb;
        _fillIn.TrebleDb = trebleDb;
        _fillIn.PreampDb = preampDb;
    }

    public void SetEndRotationTone(double bassDb, double trebleDb, double preampDb)
    {
        _endRotation.BassDb = bassDb;
        _endRotation.TrebleDb = trebleDb;
        _endRotation.PreampDb = preampDb;
    }

    private string _pausedPlayer = string.Empty;

    public void OnKaraokeTrackStarted()
    {
        StopAutoAdvanceCountdown();
        CancelFillInSchedules();
        _opening.Stop();
        _fillIn.Pause();
        if (_occasion.IsPlaying)
        {
            _occasion.Stop();
            _display.SetRotationAnnouncement(string.Empty, false);
        }
    }

    public void PauseBackgroundMusic()
    {
        if (_opening.IsPlaying)
        {
            _opening.Pause();
            _pausedPlayer = "Opening";
        }
        else if (_endRotation.IsPlaying)
        {
            _endRotation.Pause();
            _pausedPlayer = "EndRotation";
        }
        else if (_occasion.IsPlaying)
        {
            _occasion.Pause();
            _pausedPlayer = "Occasion";
        }
        else if (_fillIn.IsPlaying)
        {
            _fillIn.Pause();
            _pausedPlayer = "FillIn";
        }
    }

    public void ResumeBackgroundMusic()
    {
        if (_pausedPlayer == "Opening")
        {
            _opening.Resume();
        }
        else if (_pausedPlayer == "EndRotation")
        {
            _endRotation.Resume();
        }
        else if (_pausedPlayer == "Occasion")
        {
            _occasion.Resume();
        }
        else if (_pausedPlayer == "FillIn" || string.IsNullOrEmpty(_pausedPlayer))
        {
            _fillIn.Resume();
        }
        _pausedPlayer = string.Empty;
    }

    // ─── Automated Transition Helpers ─────────────────────────────────────

    private void ScheduleFillInMusic()
    {
        CancelFillInSchedules();

        _fillInDelayCts = new System.Threading.CancellationTokenSource();
        var token = _fillInDelayCts.Token;

        int delayMs = Lyracist.Core.Helpers.AppSettings.FillInDelaySeconds * 1000;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, token);
                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _fillIn.Duck(); // Start at reduced volume
                    _fillIn.Resume();
                    ScheduleAutoUnduck();
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void ScheduleAutoUnduck()
    {
        CancelUnduckTimer();

        if (_rotation.Rotation.Count == 0) return;

        _unduckTimerCts = new System.Threading.CancellationTokenSource();
        var token = _unduckTimerCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(15000, token); // 15 seconds quiet/KJ speaking announcement window
                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_rotation.Rotation.Count > 0)
                    {
                        _fillIn.Unduck();
                    }
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void OnRotationStateChanged()
    {
        if (_fillIn.IsPlaying)
        {
            if (_rotation.Rotation.Count > 0)
            {
                _fillIn.Unduck();
                CancelUnduckTimer();
            }
            else
            {
                _fillIn.Duck();
            }
        }
    }

    private void ScheduleEndRotationMusic()
    {
        CancelFillInSchedules();

        _endRotationDelayCts = new System.Threading.CancellationTokenSource();
        var token = _endRotationDelayCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(10000, token); // 10 seconds delay
                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _fillIn.Stop();
                    _endRotation.Play();
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private void CancelFillInSchedules()
    {
        _fillInDelayCts?.Cancel();
        _fillInDelayCts = null;

        _endRotationDelayCts?.Cancel();
        _endRotationDelayCts = null;

        CancelUnduckTimer();
    }

    private void CancelUnduckTimer()
    {
        _unduckTimerCts?.Cancel();
        _unduckTimerCts = null;
    }

    public event Action<int, bool>? AutoAdvanceCountdownTick;
    private DispatcherTimer? _autoAdvanceTimer;
    private int _autoAdvanceSecondsRemaining;

    private void StartAutoAdvanceCountdown()
    {
        StopAutoAdvanceCountdown();

        if (!Lyracist.Core.Helpers.AppSettings.EnableAutoAdvance)
        {
            return;
        }

        // Only advance if there are actually singers in rotation
        if (_rotation.Rotation.Count == 0)
        {
            return;
        }

        _autoAdvanceSecondsRemaining = Lyracist.Core.Helpers.AppSettings.AutoAdvanceCountdownSeconds;
        AutoAdvanceCountdownTick?.Invoke(_autoAdvanceSecondsRemaining, true);

        _autoAdvanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoAdvanceTimer.Tick += (s, e) =>
        {
            _autoAdvanceSecondsRemaining--;
            if (_autoAdvanceSecondsRemaining <= 0)
            {
                TriggerAutoAdvanceNow();
            }
            else
            {
                AutoAdvanceCountdownTick?.Invoke(_autoAdvanceSecondsRemaining, true);
            }
        };
        _autoAdvanceTimer.Start();
    }

    public void CancelAutoAdvance()
    {
        StopAutoAdvanceCountdown();
    }

    public void TriggerAutoAdvanceNow()
    {
        StopAutoAdvanceCountdown();

        // Advance rotation to next singer
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // The current singer is the one marked as IsCurrent
            var currentSinger = _rotation.Rotation.FirstOrDefault(s => s.IsCurrent);
            if (currentSinger == null)
            {
                currentSinger = _rotation.Rotation.FirstOrDefault();
            }
            if (currentSinger != null)
            {
                _rotation.DoneSingerCommand.Execute(currentSinger);
            }
        });
    }

    private void StopAutoAdvanceCountdown()
    {
        _autoAdvanceTimer?.Stop();
        _autoAdvanceTimer = null;
        AutoAdvanceCountdownTick?.Invoke(0, false);
    }
}
