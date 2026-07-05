using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Interfaces;
using Lyracist.Models;

namespace Lyracist.ViewModels;

public partial class PlaylistsViewModel : BaseViewModel
{
    private readonly IPlaylistService _playlistService;
    private readonly IShowFlowService _showFlow;
    private readonly ILibraryService _libraryService;
    private readonly DispatcherTimer _statusTimer;

    public ObservableCollection<PlaylistTrack> OpeningTracks { get; } = new();
    public ObservableCollection<PlaylistTrack> FillInTracks { get; } = new();
    public ObservableCollection<PlaylistTrack> EndRotationTracks { get; } = new();
    public ObservableCollection<KaraokeSong> LibrarySongs { get; } = new();

    [ObservableProperty]
    private PlaylistTrack? _selectedOpeningTrack;

    [ObservableProperty]
    private PlaylistTrack? _selectedFillInTrack;

    [ObservableProperty]
    private PlaylistTrack? _selectedEndRotationTrack;

    [ObservableProperty]
    private KaraokeSong? _selectedLibrarySong;

    [ObservableProperty]
    private double _openingVolume = 70;

    [ObservableProperty]
    private double _fillInVolume = 70;

    [ObservableProperty]
    private double _endRotationVolume = 70;

    [ObservableProperty]
    private double _openingBass;

    [ObservableProperty]
    private double _openingTreble;

    [ObservableProperty]
    private double _openingGain;

    [ObservableProperty]
    private double _fillInBass;

    [ObservableProperty]
    private double _fillInTreble;

    [ObservableProperty]
    private double _fillInGain;

    [ObservableProperty]
    private double _endRotationBass;

    [ObservableProperty]
    private double _endRotationTreble;

    [ObservableProperty]
    private double _endRotationGain;

    [ObservableProperty]
    private bool _isOpeningPlaying;

    [ObservableProperty]
    private bool _isFillInPlaying;

    [ObservableProperty]
    private bool _isFillInDucked;

    [ObservableProperty]
    private bool _isEndRotationPlaying;

    private System.Collections.Generic.List<KaraokeSong> _allLibrarySongs = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    partial void OnSearchQueryChanged(string value)
    {
        FilterLibrarySongs();
    }

    public PlaylistsViewModel(IPlaylistService playlistService, IShowFlowService showFlow, ILibraryService libraryService)
    {
        _playlistService = playlistService;
        _showFlow = showFlow;
        _libraryService = libraryService;

        RefreshAll();

        // Reflect MediaEngine-driven state changes (auto-pause/resume) that
        // happen outside of this ViewModel's own commands.
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _statusTimer.Tick += (_, _) =>
        {
            IsOpeningPlaying = _showFlow.IsOpeningPlaying;
            IsFillInPlaying = _showFlow.IsFillInPlaying;
            IsFillInDucked = _showFlow.IsFillInDucked;
            IsEndRotationPlaying = _showFlow.IsEndRotationPlaying;
        };
        _statusTimer.Start();
    }

    private void RefreshAll()
    {
        OpeningTracks.Clear();
        foreach (var track in _playlistService.GetOpeningPlaylist())
        {
            OpeningTracks.Add(track);
        }

        FillInTracks.Clear();
        foreach (var track in _playlistService.GetFillInPlaylist())
        {
            FillInTracks.Add(track);
        }

        EndRotationTracks.Clear();
        foreach (var track in _playlistService.GetEndRotationPlaylist())
        {
            EndRotationTracks.Add(track);
        }

        _allLibrarySongs = _libraryService.GetAllSongs().ToList();
        FilterLibrarySongs();

        _showFlow.RefreshPlaylists();
    }

    private void FilterLibrarySongs()
    {
        LibrarySongs.Clear();
        var query = SearchQuery?.Trim();

        System.Collections.Generic.IEnumerable<KaraokeSong> filtered = _allLibrarySongs;
        if (!string.IsNullOrEmpty(query))
        {
            filtered = _allLibrarySongs.Where(s =>
                (s.Title != null && s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (s.Artist != null && s.Artist.Contains(query, StringComparison.OrdinalIgnoreCase))
            );
        }

        foreach (var song in filtered)
        {
            LibrarySongs.Add(song);
        }
    }

    [RelayCommand]
    private void RefreshLibrary()
    {
        RefreshAll();
    }

    [RelayCommand]
    private void AddToOpening()
    {
        if (SelectedLibrarySong == null) return;

        var match = FindSongId(SelectedLibrarySong.AudioPath);
        if (match == null) return;

        _playlistService.AddSongToOpening(match.Value);
        RefreshAll();
    }

    [RelayCommand]
    private void AddToFillIn()
    {
        if (SelectedLibrarySong == null) return;

        var match = FindSongId(SelectedLibrarySong.AudioPath);
        if (match == null) return;

        _playlistService.AddSongToFillIn(match.Value);
        RefreshAll();
    }

    [RelayCommand]
    private void AddToEndRotation()
    {
        if (SelectedLibrarySong == null) return;

        var match = FindSongId(SelectedLibrarySong.AudioPath);
        if (match == null) return;

        _playlistService.AddSongToEndRotation(match.Value);
        RefreshAll();
    }

    [RelayCommand]
    private void RemoveOpeningItem()
    {
        if (SelectedOpeningTrack == null) return;
        _playlistService.RemoveFromOpening(SelectedOpeningTrack.ItemId);
        SelectedOpeningTrack = null;
        RefreshAll();
    }

    [RelayCommand]
    private void RemoveFillInItem()
    {
        if (SelectedFillInTrack == null) return;
        _playlistService.RemoveFromFillIn(SelectedFillInTrack.ItemId);
        SelectedFillInTrack = null;
        RefreshAll();
    }

    [RelayCommand]
    private void RemoveEndRotationItem()
    {
        if (SelectedEndRotationTrack == null) return;
        _playlistService.RemoveFromEndRotation(SelectedEndRotationTrack.ItemId);
        SelectedEndRotationTrack = null;
        RefreshAll();
    }

    [RelayCommand]
    private void MoveOpeningUp()
    {
        if (SelectedOpeningTrack == null) return;
        _playlistService.MoveOpeningItem(SelectedOpeningTrack.ItemId, -1);
        RefreshAll();
    }

    [RelayCommand]
    private void MoveOpeningDown()
    {
        if (SelectedOpeningTrack == null) return;
        _playlistService.MoveOpeningItem(SelectedOpeningTrack.ItemId, 1);
        RefreshAll();
    }

    [RelayCommand]
    private void MoveFillInUp()
    {
        if (SelectedFillInTrack == null) return;
        _playlistService.MoveFillInItem(SelectedFillInTrack.ItemId, -1);
        RefreshAll();
    }

    [RelayCommand]
    private void MoveFillInDown()
    {
        if (SelectedFillInTrack == null) return;
        _playlistService.MoveFillInItem(SelectedFillInTrack.ItemId, 1);
        RefreshAll();
    }

    [RelayCommand]
    private void MoveEndRotationUp()
    {
        if (SelectedEndRotationTrack == null) return;
        _playlistService.MoveEndRotationItem(SelectedEndRotationTrack.ItemId, -1);
        RefreshAll();
    }

    [RelayCommand]
    private void MoveEndRotationDown()
    {
        if (SelectedEndRotationTrack == null) return;
        _playlistService.MoveEndRotationItem(SelectedEndRotationTrack.ItemId, 1);
        RefreshAll();
    }

    [RelayCommand]
    private void StartOpeningMusic() => _showFlow.StartOpeningMusic();

    [RelayCommand]
    private void StopOpeningMusic() => _showFlow.StopOpeningMusic();

    [RelayCommand]
    private void PlayFillIn() => _showFlow.PlayFillIn();

    [RelayCommand]
    private void StopFillIn() => _showFlow.StopFillIn();

    [RelayCommand]
    private void ToggleDuckFillIn()
    {
        if (_showFlow.IsFillInDucked)
        {
            _showFlow.UnduckFillIn();
        }
        else
        {
            _showFlow.DuckFillIn();
        }
    }

    [RelayCommand]
    private void StartEndRotationMusic() => _showFlow.StartEndRotationMusic();

    [RelayCommand]
    private void StopEndRotationMusic() => _showFlow.StopEndRotationMusic();

    partial void OnOpeningVolumeChanged(double value) => _showFlow.SetOpeningVolume(value);
    partial void OnFillInVolumeChanged(double value) => _showFlow.SetFillInVolume(value);
    partial void OnEndRotationVolumeChanged(double value) => _showFlow.SetEndRotationVolume(value);

    partial void OnOpeningBassChanged(double value) => _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, OpeningGain);
    partial void OnOpeningTrebleChanged(double value) => _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, OpeningGain);
    partial void OnOpeningGainChanged(double value) => _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, OpeningGain);

    partial void OnFillInBassChanged(double value) => _showFlow.SetFillInTone(FillInBass, FillInTreble, FillInGain);
    partial void OnFillInTrebleChanged(double value) => _showFlow.SetFillInTone(FillInBass, FillInTreble, FillInGain);
    partial void OnFillInGainChanged(double value) => _showFlow.SetFillInTone(FillInBass, FillInTreble, FillInGain);

    partial void OnEndRotationBassChanged(double value) => _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, EndRotationGain);
    partial void OnEndRotationTrebleChanged(double value) => _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, EndRotationGain);
    partial void OnEndRotationGainChanged(double value) => _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, EndRotationGain);

    private int? FindSongId(string audioPath)
    {
        // KaraokeSong doesn't carry the DB SongId, so resolve it back via
        // the library search-by-path lookup the scanning engine indexed.
        using var context = new Lyracist.Data.LyracistDbContext();
        return context.Songs.Where(s => s.FilePath == audioPath).Select(s => (int?)s.SongId).FirstOrDefault();
    }
}
