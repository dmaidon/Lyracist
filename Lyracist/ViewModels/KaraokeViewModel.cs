using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Display;
using Lyracist.Services.Integration;
using Lyracist.Models;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel : BaseViewModel
{
    private readonly IMediaEngine _mediaEngine;
    private readonly IDisplayService _displayService;
    private readonly ILibraryService _libraryService;
    private readonly IShowFlowService _showFlow;
    private readonly IOccasionService _occasions;
    private readonly IPartyTymeService _partyTymeService;
    private readonly ExternalLinkService _externalLinkService;

    public RotationViewModel Rotation { get; }

    /// <summary>Nested Special Occasion menu (categories > subcategories > playable items).</summary>
    public ObservableCollection<OccasionNode> OccasionMenu { get; } = new();

    [ObservableProperty]
    private bool _isScaryokeMode;

    [ObservableProperty]
    private string _currentSongName = "No Song Loaded";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _seekPosition;

    [ObservableProperty]
    private string _selectedSongPath = string.Empty;

    [ObservableProperty]
    private ImageSource? _currentFrame;

    [ObservableProperty]
    private string _partyTymeClientId = string.Empty;

    [ObservableProperty]
    private string _partyTymeClientSecret = string.Empty;

    [ObservableProperty]
    private bool _isPartyTymeConnected;

    [ObservableProperty]
    private bool _isPartyTymeDisconnected = true;

    [ObservableProperty]
    private string _partyTymeConnectionStatus = string.Empty;

    [ObservableProperty]
    private bool _isPartyTymeLoading;

    [ObservableProperty]
    private PartyTymeTrack? _selectedPartyTymeTrack;

    public ObservableCollection<PartyTymeTrack> PartyTymeResults { get; } = new();

    [ObservableProperty]
    private string _customExternalUrl = string.Empty;

    [ObservableProperty]
    private string _customExternalTitle = string.Empty;

    [ObservableProperty]
    private string _customExternalArtist = string.Empty;

    [ObservableProperty]
    private string _selectedExternalService = "All";

    [ObservableProperty]
    private ExternalTrack? _selectedExternalTrack;

    [ObservableProperty]
    private bool _isExternalLoading;

    public ObservableCollection<ExternalTrack> ExternalResults { get; } = new();

    [ObservableProperty]
    private bool _showLocalFilter = true;

    [ObservableProperty]
    private bool _showPartyTymeFilter = true;

    [ObservableProperty]
    private bool _showSpotifyFilter = true;

    [ObservableProperty]
    private bool _showYouTubeFilter = true;

    [ObservableProperty]
    private bool _showAmazonFilter = true;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<KaraokeSong> _filteredSongs = new();

    [ObservableProperty]
    private bool _isPreviewExpanded;

    // Added properties for Singer Assignment & Rotation binding
    [ObservableProperty]
    private KaraokeSong? _selectedSong;

    public ObservableCollection<string> SingerNames { get; } = new();

    public void LoadSingerNames()
    {
        try
        {
            using var context = new Lyracist.Data.LyracistDbContext();
            var list = context.Singers.Select(s => s.Name).Distinct().ToList();
            SingerNames.Clear();
            foreach (var name in list)
            {
                SingerNames.Add(name);
            }
        }
        catch { }
    }

    [ObservableProperty]
    private string _newSingerName = string.Empty;

    partial void OnNewSingerNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            NewSingerNotes = string.Empty;
            return;
        }

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                var history = context.RotationEntries
                    .Include(r => r.Singer)
                    .Include(r => r.Song)
                    .Where(r => r.Singer!.Name == value && r.Status == "Finished")
                    .OrderByDescending(r => r.TimestampAdded)
                    .Select(r => r.Song!.Title + " - " + r.Song.Artist)
                    .Distinct()
                    .Take(5)
                    .ToList();

                if (history.Count > 0)
                {
                    string historyText = "Previously: " + string.Join(", ", history);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        NewSingerNotes = historyText;
                    });
                }
            }
            catch { }
        });
    }

    [ObservableProperty]
    private string _newSingerNotes = string.Empty;

    [ObservableProperty]
    private string _newSingerKey = "0";

    // Added properties for Now/Next prominent display banners
    [ObservableProperty]
    private string _nowSingingName = "None";

    [ObservableProperty]
    private string _nowSingingSong = "No Song";

    [ObservableProperty]
    private string _nextUpName = "None";

    [ObservableProperty]
    private string _nextUpSong = "No Song";

    public double Treble
    {
        get => _mediaEngine.Treble;
        set
        {
            if (Math.Abs(_mediaEngine.Treble - value) > 0.01)
            {
                _mediaEngine.Treble = value;
                OnPropertyChanged(nameof(Treble));
            }
        }
    }

    public double Mid
    {
        get => _mediaEngine.Mid;
        set
        {
            if (Math.Abs(_mediaEngine.Mid - value) > 0.01)
            {
                _mediaEngine.Mid = value;
                OnPropertyChanged(nameof(Mid));
            }
        }
    }

    public double Bass
    {
        get => _mediaEngine.Bass;
        set
        {
            if (Math.Abs(_mediaEngine.Bass - value) > 0.01)
            {
                _mediaEngine.Bass = value;
                OnPropertyChanged(nameof(Bass));
            }
        }
    }

    public double Compressor
    {
        get => _mediaEngine.Compressor;
        set
        {
            if (Math.Abs(_mediaEngine.Compressor - value) > 0.01)
            {
                _mediaEngine.Compressor = value;
                OnPropertyChanged(nameof(Compressor));
            }
        }
    }

    public double Limiter
    {
        get => _mediaEngine.Limiter;
        set
        {
            if (Math.Abs(_mediaEngine.Limiter - value) > 0.01)
            {
                _mediaEngine.Limiter = value;
                OnPropertyChanged(nameof(Limiter));
            }
        }
    }

    // Added properties for Multi-Monitor display lists
    public ObservableCollection<ScreenInfo> AvailableScreens { get; } = new();

    [ObservableProperty]
    private int _selectedLyricsScreenIndex = 0;

    [ObservableProperty]
    private int _selectedRotationScreenIndex = 0;

    [ObservableProperty]
    private string _rotationBannerText = "Welcome to Karaoke Night!";

    [ObservableProperty]
    private bool _showRotationBanner;

    partial void OnRotationBannerTextChanged(string value)
    {
        _displayService.SetRotationAnnouncement(value, ShowRotationBanner);
    }

    partial void OnShowRotationBannerChanged(bool value)
    {
        _displayService.SetRotationAnnouncement(RotationBannerText, value);
    }

    public double Volume
    {
        get => _mediaEngine.Volume;
        set
        {
            if (Math.Abs(_mediaEngine.Volume - value) > 0.01)
            {
                _mediaEngine.Volume = value;
                OnPropertyChanged(nameof(Volume));
            }
        }
    }

    public double Speed
    {
        get => _mediaEngine.Speed;
        set
        {
            if (Math.Abs(_mediaEngine.Speed - value) > 0.01)
            {
                _mediaEngine.Speed = value;
                OnPropertyChanged(nameof(Speed));
            }
        }
    }

    public int Pitch
    {
        get => _mediaEngine.Pitch;
        set
        {
            if (_mediaEngine.Pitch != value)
            {
                _mediaEngine.Pitch = value;
                OnPropertyChanged(nameof(Pitch));
            }
        }
    }

    public KaraokeViewModel(
        IMediaEngine mediaEngine,
        IDisplayService displayService,
        ILibraryService libraryService,
        IShowFlowService showFlow,
        IOccasionService occasions,
        RotationViewModel rotationViewModel,
        IPartyTymeService partyTymeService)
    {
        _mediaEngine = mediaEngine;
        _displayService = displayService;
        _libraryService = libraryService;
        _showFlow = showFlow;
        _occasions = occasions;
        Rotation = rotationViewModel;
        _partyTymeService = partyTymeService;
        _externalLinkService = new ExternalLinkService();

        _mediaEngine.FrameReady += OnFrameReady;
        _libraryService.LibraryUpdated += OnLibraryUpdated;
        LoadSingerNames();

        RebuildOccasionMenu();
        _occasions.OccasionsChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(RebuildOccasionMenu);

        // Hook rotation updates to sync Now/Next banners
        Rotation.Rotation.CollectionChanged += (s, e) => UpdateNowNext();

        // Apply starting defaults
        _mediaEngine.Volume = 100.0;
        _mediaEngine.Speed = 1.0;
        _mediaEngine.Pitch = 0;

        // Fetch available monitors for display targeting
        foreach (var screen in _displayService.GetScreens())
        {
            AvailableScreens.Add(screen);
        }

        RefreshFilteredList();
        UpdateNowNext();
    }

    private void OnFrameReady(ImageSource frame)
    {
        CurrentFrame = frame;
    }

    private void OnLibraryUpdated(object? sender, EventArgs e)
    {
        RefreshFilteredList();
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshFilteredList();
        if (IsPartyTymeConnected)
        {
            SearchPartyTymeCommand.Execute(null);
        }
        SearchExternalCommand.Execute(null);
    }

    private void RefreshFilteredList()
    {
        string query = SearchQuery;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var results = _libraryService.Search(query).Where(s => s.IsKaraoke).ToList();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    FilteredSongs.Clear();
                    foreach (var song in results)
                    {
                        FilteredSongs.Add(song);
                    }
                });
            }
            catch { }
        });
    }

    private void UpdateNowNext()
    {
        string oldSinger = NowSingingName;

        if (Rotation.Rotation.Count > 0)
        {
            var now = Rotation.Rotation[0];
            NowSingingName = now.Name;
            NowSingingSong = string.IsNullOrEmpty(now.SongTitle) ? "No Song" : $"{now.Artist} - {now.SongTitle}";
        }
        else
        {
            NowSingingName = "None";
            NowSingingSong = "No Song";
        }

        if (Rotation.Rotation.Count > 1)
        {
            var next = Rotation.Rotation[1];
            NextUpName = next.Name;
            NextUpSong = string.IsNullOrEmpty(next.SongTitle) ? "No Song" : $"{next.Artist} - {next.SongTitle}";
        }
        else
        {
            NextUpName = "None";
            NextUpSong = "No Song";
        }

        if (NowSingingName != oldSinger)
        {
            _mediaEngine.ActiveSingerName = NowSingingName;
            NotifyAudioPropertiesChanged();
        }
    }

    private void NotifyAudioPropertiesChanged()
    {
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(Pitch));
        OnPropertyChanged(nameof(Treble));
        OnPropertyChanged(nameof(Mid));
        OnPropertyChanged(nameof(Bass));
        OnPropertyChanged(nameof(Compressor));
        OnPropertyChanged(nameof(Limiter));
    }

    partial void OnSelectedLyricsScreenIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableScreens.Count)
        {
            _displayService.MoveLyricsToScreen(value);
        }
    }

    partial void OnSelectedRotationScreenIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableScreens.Count)
        {
            _displayService.MoveRotationToScreen(value);
        }
    }

    [RelayCommand]
    private void Play()
    {
        _mediaEngine.Play();
        IsPlaying = true;
    }

    [RelayCommand]
    private void Pause()
    {
        _mediaEngine.Pause();
        IsPlaying = false;
    }

    [RelayCommand]
    private void Stop()
    {
        _mediaEngine.Stop();
        IsPlaying = false;
        SeekPosition = 0;
    }

    [RelayCommand]
    private void LoadSong()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio & Video files (*.mp3;*.wav;*.cdg;*.mp4)|*.mp3;*.wav;*.cdg;*.mp4|All files (*.*)|*.*"
        };
        
        if (openFileDialog.ShowDialog() == true)
        {
            SelectedSongPath = openFileDialog.FileName;
            CurrentSongName = System.IO.Path.GetFileName(openFileDialog.FileName);
            _mediaEngine.LoadSong(SelectedSongPath);
            
            // Sync slider states to the view
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(Pitch));
            OnPropertyChanged(nameof(Treble));
            OnPropertyChanged(nameof(Mid));
            OnPropertyChanged(nameof(Bass));
            OnPropertyChanged(nameof(Compressor));
            OnPropertyChanged(nameof(Limiter));
        }
    }

    [RelayCommand]
    private void ScanFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Music Library Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            var folderPath = dialog.FolderName;
            _libraryService.ScanDirectory(folderPath);
        }
    }

    [RelayCommand]
    private void PlaySong(KaraokeSong song)
    {
        if (song == null) return;

        SelectedSongPath = song.AudioPath;
        CurrentSongName = $"{song.Artist} - {song.Title}";

        _mediaEngine.LoadSong(song.AudioPath);
        _mediaEngine.Play();
        IsPlaying = true;
        
        // Sync states to the view
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(Pitch));
        OnPropertyChanged(nameof(Treble));
        OnPropertyChanged(nameof(Mid));
        OnPropertyChanged(nameof(Bass));
        OnPropertyChanged(nameof(Compressor));
        OnPropertyChanged(nameof(Limiter));
    }

    [RelayCommand]
    private void AddSongToRotation(KaraokeSong song)
    {
        if (song == null) return;

        if (!string.IsNullOrWhiteSpace(NewSingerName))
        {
            Rotation.AddSinger(NewSingerName, song.Title, song.Artist, NewSingerKey, NewSingerNotes, "Local");
            NewSingerName = string.Empty;
            NewSingerNotes = string.Empty;
            NewSingerKey = "0";
            LoadSingerNames();
        }
        else if (Rotation.SelectedSinger != null)
        {
            Rotation.AddSinger(Rotation.SelectedSinger.Name, song.Title, song.Artist, "0", string.Empty, "Local");
        }
        else
        {
            Rotation.AddSinger("Singer", song.Title, song.Artist, "0", string.Empty, "Local");
        }
    }

    [RelayCommand]
    private void AddToRotation()
    {
        if (string.IsNullOrWhiteSpace(NewSingerName))
            return;

        if (SelectedSong != null)
        {
            Rotation.AddSinger(NewSingerName, SelectedSong.Title, SelectedSong.Artist, NewSingerKey, NewSingerNotes, "Local");
        }
        else if (SelectedPartyTymeTrack != null)
        {
            Rotation.AddSinger(NewSingerName, SelectedPartyTymeTrack.Title, SelectedPartyTymeTrack.Artist, NewSingerKey, 
                $"[Party Tyme ID: {SelectedPartyTymeTrack.TrackId}] {NewSingerNotes}", "PartyTyme");
        }
        else if (SelectedExternalTrack != null)
        {
            Rotation.AddSinger(NewSingerName, SelectedExternalTrack.Title, SelectedExternalTrack.Artist, NewSingerKey, 
                NewSingerNotes, SelectedExternalTrack.Source, SelectedExternalTrack.Url);
        }
        else if (!string.IsNullOrWhiteSpace(CustomExternalUrl))
        {
            var parsed = _externalLinkService.ParseUrl(CustomExternalUrl);
            if (parsed != null)
            {
                string title = string.IsNullOrWhiteSpace(CustomExternalTitle) ? parsed.Title : CustomExternalTitle;
                string artist = string.IsNullOrWhiteSpace(CustomExternalArtist) ? parsed.Artist : CustomExternalArtist;
                Rotation.AddSinger(NewSingerName, title, artist, NewSingerKey, NewSingerNotes, parsed.Source, CustomExternalUrl);
            }
        }
        else
        {
            return;
        }

        // Reset inputs
        NewSingerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";
        CustomExternalUrl = string.Empty;
        CustomExternalTitle = string.Empty;
        CustomExternalArtist = string.Empty;

        LoadSingerNames();
    }

    [RelayCommand]
    private void ResetAudio()
    {
        Volume = 100.0;
        Speed = 1.0;
        Pitch = 0;
        Treble = 0.0;
        Mid = 0.0;
        Bass = 0.0;
        Compressor = 0.0;
        Limiter = 0.0;
    }

    [RelayCommand]
    private void ShowRotation()
    {
        _displayService.ShowRotationWindow();
    }

    [RelayCommand]
    private void ShowLyrics()
    {
        _displayService.ShowLyricsWindow();
    }

    private void RebuildOccasionMenu()
    {
        OccasionMenu.Clear();
        foreach (var node in _occasions.GetMenuTree())
        {
            OccasionMenu.Add(node);
        }
    }

    [RelayCommand]
    private void PlayOccasion(object? parameter)
    {
        if (parameter is OccasionNode node)
        {
            // Category headers open their submenu; only playable items fire.
            if (!node.IsItem) return;
            _showFlow.PlayOccasion(node.Name, node.FilePath, node.Bass, node.Treble, node.Gain);
        }
    }

    [RelayCommand]
    private void StopOccasion()
    {
        _showFlow.StopOccasion();
    }

    [RelayCommand]
    private void OpenScaryokeWheel()
    {
        var wheel = App.AppHost.Services.GetRequiredService<Windows.ScaryokeWindow>();
        wheel.Show();
        wheel.Activate();
    }

    [RelayCommand]
    private void TogglePreviewExpand()
    {
        IsPreviewExpanded = !IsPreviewExpanded;
    }

    [RelayCommand]
    private void PitchUp()
    {
        Pitch = Math.Min(Pitch + 1, 6);
    }

    [RelayCommand]
    private void PitchDown()
    {
        Pitch = Math.Max(Pitch - 1, -6);
    }

    [RelayCommand]
    private void EditSongSettings(KaraokeSong song)
    {
        if (song == null) return;
        var window = App.AppHost.Services.GetRequiredService<Windows.SongSettingsWindow>();
        window.ViewModel.Load(song);
        window.Owner = System.Windows.Application.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            _mediaEngine.UpdateAudioParameters();
            NotifyAudioPropertiesChanged();
        }
    }

    [RelayCommand]
    private void EditSingerSettings(Singer singer)
    {
        if (singer == null) return;
        var window = App.AppHost.Services.GetRequiredService<Windows.SingerSettingsWindow>();
        window.ViewModel.Load(singer);
        window.Owner = System.Windows.Application.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            _mediaEngine.UpdateAudioParameters();
            NotifyAudioPropertiesChanged();
        }
    }

    [RelayCommand]
    private async Task ConnectPartyTyme()
    {
        if (string.IsNullOrWhiteSpace(PartyTymeClientId) || string.IsNullOrWhiteSpace(PartyTymeClientSecret))
        {
            PartyTymeConnectionStatus = "Credentials cannot be empty.";
            return;
        }

        PartyTymeConnectionStatus = "Authenticating...";
        IsPartyTymeLoading = true;
        
        bool success = await _partyTymeService.AuthenticateAsync(PartyTymeClientId, PartyTymeClientSecret);
        
        IsPartyTymeLoading = false;
        IsPartyTymeConnected = success;
        IsPartyTymeDisconnected = !success;
        
        if (success)
        {
            PartyTymeConnectionStatus = "Connected successfully!";
            await SearchPartyTyme();
        }
        else
        {
            PartyTymeConnectionStatus = "Authentication failed. Try again.";
        }
    }

    [RelayCommand]
    private async Task SearchPartyTyme()
    {
        if (!IsPartyTymeConnected) return;

        IsPartyTymeLoading = true;
        try
        {
            var results = await _partyTymeService.SearchCatalogAsync(SearchQuery);
            PartyTymeResults.Clear();
            foreach (var track in results)
            {
                PartyTymeResults.Add(track);
            }
        }
        catch (Exception ex)
        {
            PartyTymeConnectionStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsPartyTymeLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayPartyTymeTrack(PartyTymeTrack track)
    {
        if (track == null) return;

        IsPlaying = false;
        CurrentSongName = $"{track.Artist} - {track.Title} [Party Tyme]";

        string streamUrl = await _partyTymeService.GetStreamUrlAsync(track.TrackId);
        
        SelectedSongPath = streamUrl;
        _mediaEngine.LoadSong(streamUrl);
        _mediaEngine.Play();
        IsPlaying = true;
        
        NotifyAudioPropertiesChanged();
    }

    [RelayCommand]
    private async Task CachePartyTymeTrack(PartyTymeTrack track)
    {
        if (track == null) return;

        PartyTymeConnectionStatus = $"Caching '{track.Title}'...";
        IsPartyTymeLoading = true;
        
        string cachedPath = await _partyTymeService.DownloadTrackAsync(track.TrackId, track.Title, track.Artist);
        
        IsPartyTymeLoading = false;
        
        if (!string.IsNullOrEmpty(cachedPath))
        {
            PartyTymeConnectionStatus = $"Cached '{track.Title}' successfully!";
            await SearchPartyTyme();
        }
        else
        {
            PartyTymeConnectionStatus = "Failed to cache track.";
        }
    }

    [RelayCommand]
    private async Task PlayPerformerRequest(Singer singer)
    {
        if (singer == null) return;

        // Check if it's an external link
        if (singer.Source == "Spotify" || singer.Source == "YouTube" || singer.Source == "Amazon")
        {
            IsPlaying = false;
            CurrentSongName = $"{singer.Artist} - {singer.SongTitle} [{singer.Source}]";
            _mediaEngine.ActiveSingerName = singer.Name;
            _mediaEngine.Stop();

            try
            {
                if (!string.IsNullOrWhiteSpace(singer.ExternalLink))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(singer.ExternalLink) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open external link: {ex.Message}", "Playback Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            return;
        }

        if (singer.Notes.Contains("[Party Tyme ID:"))
        {
            int startIdx = singer.Notes.IndexOf("[Party Tyme ID:") + 15;
            int endIdx = singer.Notes.IndexOf("]", startIdx);
            if (startIdx >= 15 && endIdx > startIdx)
            {
                string trackId = singer.Notes.Substring(startIdx, endIdx - startIdx).Trim();
                
                IsPlaying = false;
                CurrentSongName = $"{singer.Artist} - {singer.SongTitle} [Party Tyme]";
                _mediaEngine.ActiveSingerName = singer.Name;

                string streamUrl = await _partyTymeService.GetStreamUrlAsync(trackId);
                
                SelectedSongPath = streamUrl;
                _mediaEngine.LoadSong(streamUrl);
                _mediaEngine.Play();
                IsPlaying = true;
                
                NotifyAudioPropertiesChanged();
                return;
            }
        }

        var localMatch = _libraryService.Search($"{singer.SongTitle} {singer.Artist}").FirstOrDefault();
        if (localMatch != null)
        {
            IsPlaying = false;
            CurrentSongName = $"{localMatch.Artist} - {localMatch.Title}";
            _mediaEngine.ActiveSingerName = singer.Name;
            
            SelectedSongPath = localMatch.AudioPath;
            _mediaEngine.LoadSong(localMatch.AudioPath);
            _mediaEngine.Play();
            IsPlaying = true;

            NotifyAudioPropertiesChanged();
        }
        else
        {
            System.Windows.MessageBox.Show(
                $"Could not locate file matching '{singer.SongTitle}' by '{singer.Artist}'. Please load it manually.",
                "Song Not Found",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task SearchExternal()
    {
        IsExternalLoading = true;
        try
        {
            var results = await _externalLinkService.SearchAsync(SearchQuery, SelectedExternalService);
            ExternalResults.Clear();
            foreach (var track in results)
            {
                ExternalResults.Add(track);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"External search error: {ex.Message}");
        }
        finally
        {
            IsExternalLoading = false;
        }
    }

    [RelayCommand]
    private void PlayExternalTrack(ExternalTrack track)
    {
        if (track == null) return;
        
        IsPlaying = false;
        CurrentSongName = $"{track.Artist} - {track.Title} [{track.Source}]";
        _mediaEngine.Stop();

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(track.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open link: {ex.Message}", "Browser Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenExternalLinkInBrowser(Singer singer)
    {
        if (singer == null || string.IsNullOrWhiteSpace(singer.ExternalLink)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(singer.ExternalLink) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open link: {ex.Message}", "Browser Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
