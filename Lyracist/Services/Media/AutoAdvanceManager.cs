// Created on Aug 20, 2026 @ 09:56:00 -> Add AutoAdvanceManager for safe DJ-friendly auto-advance with grace period, fill-in music, and state machine
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Lyracist.ViewModels;

namespace Lyracist.Services.Media;

/// <summary>
/// Manages the automated transition lifecycle between karaoke performances:
/// handles grace period timers, coordinates fill-in background music, advances singer rotation,
/// synchronizes projection and mobile servers, and prevents accidental double-starts via a strict state machine.
/// </summary>
public class AutoAdvanceManager
{
    private readonly IMediaEngine _mediaEngine;
    private readonly IShowFlowService _showFlow;
    private readonly IDisplayService _displayService;
    private readonly RotationViewModel _rotation;
    private readonly ITabletLyricsServer _tabletServer;
    private readonly ILibraryService _libraryService;
    private readonly LyricsWindowViewModel _lyricsWindowVm;
    private readonly Func<KaraokeViewModel>? _getKaraokeVm;
    private readonly Func<TriviaViewModel>? _getTriviaVm;

    private DispatcherTimer? _graceTimer;
    private int _secondsRemaining;
    private AutoAdvanceState _currentState = AutoAdvanceState.Idle;
    private readonly object _stateLock = new();

    public AutoAdvanceState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                StateChanged?.Invoke(_currentState);
            }
        }
    }

    public int SecondsRemaining => _secondsRemaining;
    public int MaxSeconds => AppSettings.AutoAdvanceCountdownSeconds > 0 ? AppSettings.AutoAdvanceCountdownSeconds : 15;

    public event Action<AutoAdvanceState>? StateChanged;
    public event Action<int, bool>? CountdownTick;

    public AutoAdvanceManager(
        IMediaEngine mediaEngine,
        IShowFlowService showFlow,
        IDisplayService displayService,
        RotationViewModel rotation,
        ITabletLyricsServer tabletServer,
        ILibraryService libraryService,
        LyricsWindowViewModel lyricsWindowVm,
        Func<KaraokeViewModel>? getKaraokeVm = null,
        Func<TriviaViewModel>? getTriviaVm = null)
    {
        _mediaEngine = mediaEngine;
        _showFlow = showFlow;
        _displayService = displayService;
        _rotation = rotation;
        _tabletServer = tabletServer;
        _libraryService = libraryService;
        _lyricsWindowVm = lyricsWindowVm;
        _getKaraokeVm = getKaraokeVm;
        _getTriviaVm = getTriviaVm;

        // Hook media playback completion for automatic transition
        _mediaEngine.SongEnded += OnSongEnded;
    }

    private void OnSongEnded()
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            BeginGracePeriod();
        }));
    }

    /// <summary>
    /// Begins the grace period countdown between songs, starting fill-in music and displaying the announcement.
    /// </summary>
    public void BeginGracePeriod()
    {
        lock (_stateLock)
        {
            // Safety: Never start auto-advance during Trivia mode
            if (IsTriviaModeActive())
            {
                CurrentState = AutoAdvanceState.Idle;
                return;
            }

            // Safety: Never start auto-advance during Scaryoke mode
            if (IsScaryokeModeActive())
            {
                CurrentState = AutoAdvanceState.Idle;
                return;
            }

            // Safety: Must have active singers in rotation
            var activeSingers = _rotation.Rotation.Where(s => !s.IsInactive && !s.IsPaused).ToList();
            if (activeSingers.Count == 0)
            {
                CurrentState = AutoAdvanceState.Idle;
                StopGraceTimer();
                return;
            }

            CurrentState = AutoAdvanceState.GracePeriod;
        }

        // Start / resume fill-in background music
        _showFlow.PlayFillIn();
        _showFlow.DuckFillIn();

        // Determine next upcoming singer to announce
        var nextSinger = _rotation.GetNextSinger() ?? _rotation.GetCurrentSinger();
        string singerName = nextSinger != null ? nextSinger.Name : "Next Singer";
        if (nextSinger != null && !string.IsNullOrWhiteSpace(nextSinger.DuetPartnerName))
        {
            singerName += $" & {nextSinger.DuetPartnerName}";
        }

        // Display announcement on rotation billboard
        _displayService.SetRotationAnnouncement($"Next singer: {singerName} — please come to the stage", true);

        // Start countdown timer
        _secondsRemaining = MaxSeconds;
        CountdownTick?.Invoke(_secondsRemaining, true);

        StopGraceTimer();
        _graceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _graceTimer.Tick += GraceTimer_Tick;
        _graceTimer.Start();
    }

    private void GraceTimer_Tick(object? sender, EventArgs e)
    {
        _secondsRemaining--;
        if (_secondsRemaining <= 0)
        {
            StopGraceTimer();
            AutoAdvanceToNextSinger();
        }
        else
        {
            CountdownTick?.Invoke(_secondsRemaining, true);
        }
    }

    /// <summary>
    /// Automatically advances rotation to the next singer once the grace period expires.
    /// </summary>
    public void AutoAdvanceToNextSinger()
    {
        lock (_stateLock)
        {
            if (IsTriviaModeActive() || IsScaryokeModeActive())
            {
                CurrentState = AutoAdvanceState.Idle;
                return;
            }
        }

        // Advance current singer in rotation
        var currentSinger = _rotation.GetCurrentSinger();
        if (currentSinger != null)
        {
            _rotation.DoneSingerCommand.Execute(currentSinger);
        }

        var newCurrent = _rotation.GetCurrentSinger();
        if (newCurrent == null)
        {
            CurrentState = AutoAdvanceState.Idle;
            _displayService.SetRotationAnnouncement(string.Empty, false);
            return;
        }

        // Check if new singer has a valid song selected
        bool hasSong = !string.IsNullOrWhiteSpace(newCurrent.SongTitle) || !string.IsNullOrWhiteSpace(newCurrent.ExternalLink);

        if (!hasSong)
        {
            CurrentState = AutoAdvanceState.WaitingForSongSelection;
            _displayService.SetRotationAnnouncement($"Waiting for song selection: {newCurrent.Name}", true);
            _showFlow.UnduckFillIn();
        }
        else
        {
            CurrentState = AutoAdvanceState.ReadyToStart;
            // Proceed to start song automatically if enabled
            if (AppSettings.EnableAutoAdvance)
            {
                _ = StartSongNow();
            }
        }
    }

    /// <summary>
    /// DJ Action: Stops grace timer/fill-in music, loads the current singer's song, and immediately starts playback.
    /// </summary>
    public async Task StartSongNow()
    {
        lock (_stateLock)
        {
            // Safety: Guard against double-starts
            if (CurrentState == AutoAdvanceState.StartingSong)
            {
                return;
            }
            CurrentState = AutoAdvanceState.StartingSong;
        }

        StopGraceTimer();

        // Retrieve current singer
        var currentSinger = _rotation.GetCurrentSinger();
        if (currentSinger == null)
        {
            CurrentState = AutoAdvanceState.Idle;
            return;
        }

        // Stop fill-in music and cancel schedules
        _showFlow.OnKaraokeTrackStarted();

        try
        {
            // Load singer's track
            await LoadSingerSong(currentSinger);

            // Synchronize projection window, mobile lyrics server, and billboard
            _lyricsWindowVm.LoadSong(currentSinger);
            _tabletServer.LoadSong(currentSinger);
            _displayService.SetRotationAnnouncement(string.Empty, false);
            _displayService.UpdateRotation([.. _rotation.Rotation]);

            // Start playback
            await _mediaEngine.Play();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "AutoAdvanceManager.StartSongNow: failed to load or start song");
        }
        finally
        {
            CurrentState = AutoAdvanceState.Idle;
        }
    }

    /// <summary>
    /// DJ Action: Skips the current singer, keeps fill-in music active, advances rotation, and restarts the grace period for the next singer.
    /// </summary>
    public void SkipSinger()
    {
        StopGraceTimer();

        // Ensure fill-in music continues
        _showFlow.PlayFillIn();

        // Advance rotation order without incrementing completed song stats
        _rotation.SkipCurrentSinger();
        _displayService.UpdateRotation([.. _rotation.Rotation]);

        // Begin fresh grace period for the next singer
        BeginGracePeriod();
    }

    /// <summary>
    /// Loads the audio/video/cdg media for the specified singer.
    /// </summary>
    public async Task LoadSingerSong(Singer singer)
    {
        if (singer == null) return;

        _mediaEngine.ActiveSingerName = singer.Name;
        _mediaEngine.ActiveDuetPartnerName = singer.DuetPartnerName;

        string songPath = string.Empty;

        // 1. Direct external link / file path
        if (!string.IsNullOrWhiteSpace(singer.ExternalLink) && File.Exists(singer.ExternalLink))
        {
            songPath = singer.ExternalLink;
        }
        else if (!string.IsNullOrWhiteSpace(singer.SongTitle))
        {
            // 2. Search local library for matched title/artist
            var searchResults = await _libraryService.SearchAsync(singer.SongTitle, isMusic: singer.IsMusic);
            var match = searchResults.FirstOrDefault(s =>
                string.Equals(s.Title, singer.SongTitle, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(singer.Artist) || string.Equals(s.Artist, singer.Artist, StringComparison.OrdinalIgnoreCase)))
                ?? searchResults.FirstOrDefault(s => string.Equals(s.Title, singer.SongTitle, StringComparison.OrdinalIgnoreCase))
                ?? searchResults.FirstOrDefault();

            if (match != null && !string.IsNullOrWhiteSpace(match.AudioPath))
            {
                songPath = match.AudioPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(songPath) && File.Exists(songPath))
        {
            await _mediaEngine.LoadSong(songPath);
        }
    }

    public void CancelGracePeriod()
    {
        StopGraceTimer();
        CurrentState = AutoAdvanceState.Idle;
        _displayService.SetRotationAnnouncement(string.Empty, false);
    }

    private void StopGraceTimer()
    {
        _graceTimer?.Stop();
        _graceTimer = null;
        CountdownTick?.Invoke(0, false);
    }

    private bool IsTriviaModeActive()
    {
        try
        {
            var triviaVm = _getTriviaVm?.Invoke();
            return triviaVm != null && triviaVm.IsGameRunning;
        }
        catch
        {
            return false;
        }
    }

    private bool IsScaryokeModeActive()
    {
        try
        {
            var karaokeVm = _getKaraokeVm?.Invoke();
            return karaokeVm != null && karaokeVm.IsScaryokeMode;
        }
        catch
        {
            return false;
        }
    }
}
