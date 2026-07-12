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
    private readonly IOccasionService _occasions;
    private readonly DispatcherTimer _statusTimer;

    public ObservableCollection<PlaylistTrack> OpeningTracks { get; } = [];
    public ObservableCollection<PlaylistTrack> FillInTracks { get; } = [];
    public ObservableCollection<PlaylistTrack> EndRotationTracks { get; } = [];
    public ObservableCollection<KaraokeSong> LibrarySongs { get; } = [];

    // Special Occasion editor
    public ObservableCollection<OccasionNode> OccasionCategories { get; } = [];
    public ObservableCollection<OccasionNode> OccasionItems { get; } = [];

    [ObservableProperty]
    private OccasionNode? _selectedOccasionCategory;

    [ObservableProperty]
    private OccasionNode? _selectedOccasionItem;

    [ObservableProperty]
    private string _newOccasionCategoryName = string.Empty;

    [ObservableProperty]
    private bool _addAsSubcategory;

    [ObservableProperty]
    private string _newOccasionItemName = string.Empty;

    [ObservableProperty]
    private double _occasionItemBass;

    [ObservableProperty]
    private double _occasionItemTreble;

    [ObservableProperty]
    private double _occasionItemGain;

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

    private System.Collections.Generic.List<KaraokeSong> _allLibrarySongs = [];

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    partial void OnSearchQueryChanged(string value)
    {
        FilterLibrarySongs();
    }

    public PlaylistsViewModel(IPlaylistService playlistService, IShowFlowService showFlow, ILibraryService libraryService, IOccasionService occasions)
    {
        _playlistService = playlistService;
        _showFlow = showFlow;
        _libraryService = libraryService;
        _occasions = occasions;

        RefreshAll();
        RefreshOccasionCategories();

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
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var opening = _playlistService.GetOpeningPlaylist().ToList();
                var fillIn = _playlistService.GetFillInPlaylist().ToList();
                var endRot = _playlistService.GetEndRotationPlaylist().ToList();
                var songs = _libraryService.GetBackgroundMusicSongs().ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OpeningTracks.Clear();
                    foreach (var track in opening)
                        OpeningTracks.Add(track);

                    FillInTracks.Clear();
                    foreach (var track in fillIn)
                        FillInTracks.Add(track);

                    EndRotationTracks.Clear();
                    foreach (var track in endRot)
                        EndRotationTracks.Add(track);

                    _allLibrarySongs = songs;
                    FilterLibrarySongs();

                    _showFlow.RefreshPlaylists();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during RefreshAll background load: {ex.Message}");
            }
        });
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

        filtered = filtered.OrderBy(s => s.Title);

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
    private void StartOpeningMusic() => _showFlow.StartOpeningMusic(SelectedOpeningTrack?.AudioPath);

    [RelayCommand]
    private void StopOpeningMusic() => _showFlow.StopOpeningMusic();

    [RelayCommand]
    private void PlayFillIn() => _showFlow.PlayFillIn(SelectedFillInTrack?.AudioPath);

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
    private void StartEndRotationMusic() => _showFlow.StartEndRotationMusic(SelectedEndRotationTrack?.AudioPath);

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

    private void RefreshOccasionCategories()
    {
        int? keepId = SelectedOccasionCategory?.Id;
        OccasionCategories.Clear();
        foreach (var category in _occasions.GetCategoriesFlat())
        {
            OccasionCategories.Add(category);
        }
        SelectedOccasionCategory = OccasionCategories.FirstOrDefault(c => c.Id == keepId)
                                   ?? OccasionCategories.FirstOrDefault();
    }

    private void RefreshOccasionItems()
    {
        OccasionItems.Clear();
        if (SelectedOccasionCategory == null) return;

        foreach (var item in _occasions.GetItems(SelectedOccasionCategory.Id))
        {
            OccasionItems.Add(item);
        }
    }

    partial void OnSelectedOccasionCategoryChanged(OccasionNode? value)
    {
        RefreshOccasionItems();
    }

    partial void OnSelectedOccasionItemChanged(OccasionNode? value)
    {
        if (value == null) return;
        OccasionItemBass = value.Bass;
        OccasionItemTreble = value.Treble;
        OccasionItemGain = value.Gain;
    }

    [RelayCommand]
    private void AddOccasionCategory()
    {
        if (string.IsNullOrWhiteSpace(NewOccasionCategoryName)) return;

        int? parentId = AddAsSubcategory ? SelectedOccasionCategory?.Id : null;
        _occasions.AddCategory(NewOccasionCategoryName, parentId);
        NewOccasionCategoryName = string.Empty;
        RefreshOccasionCategories();
    }

    [RelayCommand]
    private void RemoveOccasionCategory()
    {
        if (SelectedOccasionCategory == null) return;
        _occasions.RemoveCategory(SelectedOccasionCategory.Id);
        SelectedOccasionCategory = null;
        RefreshOccasionCategories();
    }

    [RelayCommand]
    private void AddOccasionItem()
    {
        if (SelectedOccasionCategory == null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Occasion Music File",
            Filter = "Audio files (*.mp3;*.wav;*.m4a;*.flac)|*.mp3;*.wav;*.m4a;*.flac|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        string name = string.IsNullOrWhiteSpace(NewOccasionItemName)
            ? System.IO.Path.GetFileNameWithoutExtension(dialog.FileName)
            : NewOccasionItemName;

        _occasions.AddItem(SelectedOccasionCategory.Id, name, dialog.FileName);
        NewOccasionItemName = string.Empty;
        RefreshOccasionItems();
    }

    [RelayCommand]
    private void SearchOccasionItem()
    {
        if (SelectedOccasionCategory == null)
        {
            System.Windows.MessageBox.Show("Please select an Occasion Category first.", "Select Category", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var partyTyme = (IPartyTymeService)App.AppHost.Services.GetService(typeof(IPartyTymeService))!;
        var window = new Lyracist.Windows.OccasionSearchWindow(
            _occasions,
            _libraryService,
            partyTyme,
            SelectedOccasionCategory.Id,
            () => RefreshOccasionItems()
        )
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void RemoveOccasionItem()
    {
        if (SelectedOccasionItem == null) return;
        _occasions.RemoveItem(SelectedOccasionItem.Id);
        SelectedOccasionItem = null;
        RefreshOccasionItems();
    }

    [RelayCommand]
    private void SaveOccasionItemAudio()
    {
        if (SelectedOccasionItem == null) return;
        _occasions.UpdateItemAudio(SelectedOccasionItem.Id, OccasionItemBass, OccasionItemTreble, OccasionItemGain);
        RefreshOccasionItems();
    }
}
