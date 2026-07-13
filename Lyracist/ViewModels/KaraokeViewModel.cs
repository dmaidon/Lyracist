using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Services.Display;
using Lyracist.Services.Integration;
using Lyracist.Services.Database;
using Lyracist.Models;
using Microsoft.EntityFrameworkCore;
using Wpf.Ui;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel : BaseViewModel
{
    private readonly IMediaEngine _mediaEngine;
    private readonly IDisplayService _displayService;
    private readonly ILibraryService _libraryService;
    private readonly IShowFlowService _showFlow;
    private readonly IOccasionService _occasions;
    private readonly IPartyTymeService _partyTymeService;
    private readonly IRequestService _requests;
    private readonly INavigationService _navigation;
    private readonly ExternalLinkService _externalLinkService;
    private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;
    private int _searchRequestToken;

    public RotationViewModel Rotation { get; }

    /// <summary>Nested Special Occasion menu (categories > subcategories > playable items).</summary>
    public ObservableCollection<OccasionNode> OccasionMenu { get; } = [];

    [ObservableProperty]
    private bool _isScaryokeMode;

    [ObservableProperty]
    private bool _hasPendingKaraokeRequest;

    [ObservableProperty]
    private bool _hasPendingMusicRequest;

    [ObservableProperty]
    private string _currentSongName = "No Song Loaded";

    [ObservableProperty]
    private bool _isPlaying;

    public bool EnableKillVocal
    {
        get => _mediaEngine.EnableKillVocal;
        set
        {
            if (_mediaEngine.EnableKillVocal != value)
            {
                _mediaEngine.EnableKillVocal = value;
                OnPropertyChanged(nameof(EnableKillVocal));
            }
        }
    }

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

    public ObservableCollection<PartyTymeTrack> PartyTymeResults { get; } = [];

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

    public ObservableCollection<ExternalTrack> ExternalResults { get; } = [];
    public ObservableCollection<SingerHistoryEntry> SingerHistoryResults { get; } = [];

    [ObservableProperty]
    private bool _showLocalFilter = true;

    [ObservableProperty]
    private bool _showPartyTymeFilter = false;

    [ObservableProperty]
    private bool _showSpotifyFilter = false;

    [ObservableProperty]
    private bool _showYouTubeFilter = true;

    [ObservableProperty]
    private bool _showAmazonFilter = false;

    [ObservableProperty]
    private bool _isSpotifyAvailable = false;

    [ObservableProperty]
    private bool _isYouTubeAvailable = true;

    [ObservableProperty]
    private bool _isAmazonAvailable = false;

    [ObservableProperty]
    private bool _isExternalPerformanceActive;

    [ObservableProperty]
    private string _externalPerformanceSource = string.Empty;

    [ObservableProperty]
    private string _externalPerformanceUrl = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<KaraokeSong> _filteredSongs = [];

    [ObservableProperty]
    private bool _isPreviewExpanded;

    // Added properties for Singer Assignment & Rotation binding
    [ObservableProperty]
    private KaraokeSong? _selectedSong;

    [ObservableProperty]
    private SingerHistoryEntry? _selectedHistoryEntry;

    // Theme selection properties for header
    public List<string> ThemeModes { get; } = ["Light", "Dark", "System"];

    [ObservableProperty]
    private string _themeMode = AppSettings.ThemeMode;

    partial void OnThemeModeChanged(string value)
    {
        AppSettings.ThemeMode = value;
    }

    partial void OnSelectedSongChanged(KaraokeSong? value)
    {
        if (value != null)
        {
            SelectedPartyTymeTrack = null;
            SelectedExternalTrack = null;
            SelectedHistoryEntry = null;
        }
    }

    partial void OnSelectedPartyTymeTrackChanged(PartyTymeTrack? value)
    {
        if (value != null)
        {
            SelectedSong = null;
            SelectedExternalTrack = null;
            SelectedHistoryEntry = null;
        }
    }

    partial void OnSelectedExternalTrackChanged(ExternalTrack? value)
    {
        if (value != null)
        {
            SelectedSong = null;
            SelectedPartyTymeTrack = null;
            SelectedHistoryEntry = null;
        }
    }

    partial void OnSelectedHistoryEntryChanged(SingerHistoryEntry? value)
    {
        if (value != null)
        {
            SelectedSong = null;
            SelectedPartyTymeTrack = null;
            SelectedExternalTrack = null;
        }
    }

    public ObservableCollection<string> SingerNames { get; } = [];

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
            SingerHistoryResults.Clear();
            return;
        }

        // Query the new SingerHistory table entries
        System.Threading.Tasks.Task.Run(() =>
        {
            var historyList = Lyracist.Services.Database.SingerHistoryService.GetHistory(value);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SingerHistoryResults.Clear();
                foreach (var entry in historyList)
                {
                    SingerHistoryResults.Add(entry);
                }
            });
        });

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

    [ObservableProperty]
    private string _newDuetPartnerName = string.Empty;

    [ObservableProperty]
    private int _autoAdvanceRemainingSeconds;

    [ObservableProperty]
    private bool _isAutoAdvanceActive;

    public int AutoAdvanceMaxSeconds => Lyracist.Core.Helpers.AppSettings.AutoAdvanceCountdownSeconds;

    // Added properties for Now/Next prominent display banners
    [ObservableProperty]
    private string _nowSingingName = "None";

    [ObservableProperty]
    private string _nowSingingSong = "No Song";

    [ObservableProperty]
    private string _nextUpName = "None";

    [ObservableProperty]
    private string _nextUpSong = "No Song";

    [ObservableProperty]
    private System.Windows.Media.Imaging.BitmapImage? _qrCodeImage;

    [ObservableProperty]
    private string _joinUrl = string.Empty;

    public void RefreshQrCode()
    {
        try
        {
            string ip = AppSettings.GetActiveIPAddress();
            JoinUrl = $"http://{ip}:{AppSettings.TabletPort}";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(JoinUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
            QrCodeImage = LoadImage(qrCodeAsPngByteArr);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to generate QR Code: {ex.Message}");
        }
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadImage(byte[] imageData)
    {
        if (imageData == null || imageData.Length == 0) return null;
        var image = new System.Windows.Media.Imaging.BitmapImage();
        using (var mem = new System.IO.MemoryStream(imageData))
        {
            mem.Position = 0;
            image.BeginInit();
            image.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = null;
            image.StreamSource = mem;
            image.EndInit();
        }
        image.Freeze();
        return image;
    }

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
    public ObservableCollection<ScreenInfo> AvailableScreens { get; } = [];

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
        IPartyTymeService partyTymeService,
        IRequestService requests,
        INavigationService navigation)
    {
        _mediaEngine = mediaEngine;
        _displayService = displayService;
        _libraryService = libraryService;
        _showFlow = showFlow;
        _occasions = occasions;
        Rotation = rotationViewModel;
        _partyTymeService = partyTymeService;
        _requests = requests;
        _navigation = navigation;
        _externalLinkService = new ExternalLinkService();

        RefreshPendingRequestIndicators();
        _requests.RequestsChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshPendingRequestIndicators);

        // Debounce search-as-you-type so we don't fire a DB query per keystroke;
        // RefreshFilteredList also discards stale results via _searchRequestToken
        // in case an older query's results resolve after a newer one's.
        _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            RefreshFilteredList();
        };

        _mediaEngine.FrameReady += OnFrameReady;
        _libraryService.LibraryUpdated += OnLibraryUpdated;
        LoadSingerNames();
        RefreshQrCode();

        RebuildOccasionMenu();
        _occasions.OccasionsChanged += (_, _) =>
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(RebuildOccasionMenu);

        // Hook rotation updates to sync Now/Next banners
        Rotation.Rotation.CollectionChanged += (s, e) => UpdateNowNext();
        Rotation.RotationStateChanged += UpdateNowNext;

        _showFlow.AutoAdvanceCountdownTick += (seconds, active) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AutoAdvanceRemainingSeconds = seconds;
                IsAutoAdvanceActive = active;
                OnPropertyChanged(nameof(AutoAdvanceMaxSeconds));
            });
        };

        AppSettings.ThemeModeChanged += theme =>
        {
            if (_themeMode != theme)
            {
                _themeMode = theme;
                OnPropertyChanged(nameof(ThemeMode));
            }
        };

        // Apply starting defaults
        _mediaEngine.Volume = 100.0;
        _mediaEngine.Speed = 1.0;
        _mediaEngine.Pitch = 0;

        // Fetch available monitors for display targeting, prefixing with 'None'
        AvailableScreens.Add(new ScreenInfo { Index = -1, DeviceName = "None (Do not show)" });
        foreach (var screen in _displayService.GetScreens())
        {
            AvailableScreens.Add(screen);
        }

        var prefs = _displayService.GetPreferences();
        var currentLyricsScreen = prefs.LyricsScreenIndex.HasValue
            ? AvailableScreens.FirstOrDefault(s => s.Index == prefs.LyricsScreenIndex.Value)
            : AvailableScreens.FirstOrDefault(s => s.Index == -1);
        _selectedLyricsScreenIndex = currentLyricsScreen != null ? AvailableScreens.IndexOf(currentLyricsScreen) : 0;

        var currentRotationScreen = prefs.RotationScreenIndex.HasValue
            ? AvailableScreens.FirstOrDefault(s => s.Index == prefs.RotationScreenIndex.Value)
            : AvailableScreens.FirstOrDefault(s => s.Index == -1);
        _selectedRotationScreenIndex = currentRotationScreen != null ? AvailableScreens.IndexOf(currentRotationScreen) : 0;

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

    public void OnNavigatedTo()
    {
        // Refresh credentials and availability from settings
        PartyTymeClientId = AppSettings.PartyTymeClientId;
        PartyTymeClientSecret = AppSettings.PartyTymeClientSecret;
        IsSpotifyAvailable = !string.IsNullOrWhiteSpace(AppSettings.SpotifyClientId);
        IsAmazonAvailable = !string.IsNullOrWhiteSpace(AppSettings.AmazonAccessKey);

        LoadSingerNames();
    }

    partial void OnPartyTymeClientIdChanged(string value) => AppSettings.PartyTymeClientId = value;
    partial void OnPartyTymeClientSecretChanged(string value) => AppSettings.PartyTymeClientSecret = value;

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();

        if (IsPartyTymeConnected)
        {
            SearchPartyTymeCommand.Execute(null);
        }
        SearchExternalCommand.Execute(null);
    }

    private async void RefreshFilteredList()
    {
        string query = SearchQuery;
        int myToken = ++_searchRequestToken;

        try
        {
            var results = (await _libraryService.SearchAsync(query))
                .Where(s => s.IsKaraoke)
                .ToList();

            // Discard results if a newer search has since been issued.
            if (myToken != _searchRequestToken) return;

            FilteredSongs.Clear();
            foreach (var song in results)
            {
                FilteredSongs.Add(song);
            }
        }
        catch { }
    }

    private void RefreshPendingRequestIndicators()
    {
        var pending = _requests.GetPending();
        HasPendingKaraokeRequest = pending.Any(r => r.RequestType == "Karaoke");
        HasPendingMusicRequest = pending.Any(r => r.RequestType == "Music");
    }

    [RelayCommand]
    private void NavigateToRequests()
    {
        _navigation.Navigate(typeof(Views.Pages.RequestsPage));
    }

    private void UpdateNowNext()
    {
        string oldSinger = NowSingingName;

        var rotationList = Rotation.Rotation.ToList();
        var activeSingers = rotationList.Where(s => !s.IsPaused && !s.IsInactive).ToList();

        if (activeSingers.Count == 0)
        {
            foreach (var s in rotationList)
            {
                s.IsCurrent = false;
                s.IsNext = false;
            }
            NowSingingName = "None";
            NowSingingSong = "No Song";
            NextUpName = "None";
            NextUpSong = "No Song";

            if (NowSingingName != oldSinger)
            {
                _mediaEngine.ActiveSingerName = NowSingingName;
                _mediaEngine.ActiveDuetPartnerName = "None";
                NotifyAudioPropertiesChanged();
            }
            return;
        }

        // Find the designated current singer
        Singer? current = rotationList.FirstOrDefault(s => s.IsCurrent);

        // Fallback if current is not set or is no longer active
        if (current == null || current.IsPaused || current.IsInactive || !rotationList.Contains(current))
        {
            current = activeSingers.FirstOrDefault();
        }

        foreach (var s in rotationList)
        {
            s.IsCurrent = false;
            s.IsNext = false;
        }

        if (current != null)
        {
            current.IsCurrent = true;
            NowSingingName = current.Name;
            NowSingingSong = string.IsNullOrEmpty(current.SongTitle) ? "No Song" : $"{current.Artist} - {current.SongTitle}";

            // Next is always whoever sequentially follows the current singer in the
            // rotation, wrapping around — matching KSRotation. Never reuse a stale
            // IsNext flag: it may point at someone left over from before Current moved.
            Singer? next = null;
            int currentIndex = rotationList.IndexOf(current);
            for (int i = 1; i <= rotationList.Count; i++)
            {
                int nextIndex = (currentIndex + i) % rotationList.Count;
                var candidate = rotationList[nextIndex];
                if (candidate != current && !candidate.IsPaused && !candidate.IsInactive)
                {
                    next = candidate;
                    break;
                }
            }

            if (next != null)
            {
                next.IsNext = true;
                NextUpName = next.Name;
                NextUpSong = string.IsNullOrEmpty(next.SongTitle) ? "No Song" : $"{next.Artist} - {next.SongTitle}";
            }
            else
            {
                NextUpName = "None";
                NextUpSong = "No Song";
            }
        }
        else
        {
            NowSingingName = "None";
            NowSingingSong = "No Song";
            NextUpName = "None";
            NextUpSong = "No Song";
        }

        string partnerName = current?.DuetPartnerName ?? string.Empty;
        if (NowSingingName != oldSinger || _mediaEngine.ActiveDuetPartnerName != partnerName)
        {
            _mediaEngine.ActiveSingerName = NowSingingName;
            _mediaEngine.ActiveDuetPartnerName = partnerName;
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
            var screen = AvailableScreens[value];
            _displayService.MoveLyricsToScreen(screen.Index == -1 ? null : screen.Index);
        }
    }

    partial void OnSelectedRotationScreenIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableScreens.Count)
        {
            var screen = AvailableScreens[value];
            _displayService.MoveRotationToScreen(screen.Index == -1 ? null : screen.Index);
        }
    }

    [RelayCommand]
    private async Task Play()
    {
        if (!string.IsNullOrEmpty(SelectedSongPath))
        {
            await _mediaEngine.Play();
            IsPlaying = true;
        }
        else
        {
            _showFlow.ResumeBackgroundMusic();
        }
    }

    [RelayCommand]
    private async Task Pause()
    {
        if (!string.IsNullOrEmpty(SelectedSongPath))
        {
            await _mediaEngine.Pause();
            IsPlaying = false;
        }
        else
        {
            _showFlow.PauseBackgroundMusic();
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (!string.IsNullOrEmpty(SelectedSongPath))
        {
            await _mediaEngine.Stop();
            IsPlaying = false;
            SeekPosition = 0;
        }
        else
        {
            _showFlow.StopOpeningMusic();
            _showFlow.StopFillIn();
            _showFlow.StopEndRotationMusic();
            _showFlow.StopOccasion();
        }
    }

    [RelayCommand]
    private async Task LoadSong()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Audio & Video files (*.mp3;*.wav;*.cdg;*.mp4)|*.mp3;*.wav;*.cdg;*.mp4|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SelectedSongPath = openFileDialog.FileName;
            CurrentSongName = System.IO.Path.GetFileName(openFileDialog.FileName);

            IsExternalPerformanceActive = false;
            ExternalPerformanceSource = string.Empty;
            ExternalPerformanceUrl = string.Empty;

            await _mediaEngine.LoadSong(SelectedSongPath);

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
    private async Task PlaySong(KaraokeSong song)
    {
        if (song == null) return;

        SelectedSongPath = song.AudioPath;
        CurrentSongName = $"{song.Artist} - {song.Title}";

        IsExternalPerformanceActive = false;
        ExternalPerformanceSource = string.Empty;
        ExternalPerformanceUrl = string.Empty;

        await _mediaEngine.LoadSong(song.AudioPath);
        await _mediaEngine.Play();
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

        string targetSingerName = NewSingerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetSingerName))
        {
            if (Rotation.SelectedSinger != null)
            {
                targetSingerName = Rotation.SelectedSinger.Name;
            }
            else
            {
                targetSingerName = "Singer";
            }
        }

        Rotation.AddSinger(targetSingerName, song.Title, song.Artist, NewSingerKey, NewSingerNotes, "Local", song.AudioPath, NewDuetPartnerName);

        // Reset inputs
        NewSingerName = string.Empty;
        NewDuetPartnerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";
        LoadSingerNames();
    }

    [RelayCommand]
    private void AddExternalSongToRotation(ExternalTrack track)
    {
        if (track == null) return;

        string targetSingerName = NewSingerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetSingerName))
        {
            if (Rotation.SelectedSinger != null)
            {
                targetSingerName = Rotation.SelectedSinger.Name;
            }
            else
            {
                targetSingerName = "Singer";
            }
        }

        Rotation.AddSinger(targetSingerName, track.Title, track.Artist, NewSingerKey, NewSingerNotes, track.Source, track.Url, NewDuetPartnerName);

        // Reset inputs
        NewSingerName = string.Empty;
        NewDuetPartnerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";
        LoadSingerNames();
    }

    [RelayCommand]
    private void CancelAutoAdvance()
    {
        _showFlow.CancelAutoAdvance();
    }

    [RelayCommand]
    private void TriggerAutoAdvanceNow()
    {
        _showFlow.TriggerAutoAdvanceNow();
    }

    [RelayCommand]
    private void AddHistorySongToRotation(SingerHistoryEntry entry)
    {
        if (entry == null) return;

        string targetSingerName = NewSingerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetSingerName))
        {
            if (Rotation.SelectedSinger != null)
            {
                targetSingerName = Rotation.SelectedSinger.Name;
            }
            else
            {
                targetSingerName = "Singer";
            }
        }

        Rotation.AddSinger(targetSingerName, entry.SongTitle, entry.Artist, "0", string.Empty, entry.Source, entry.Link, NewDuetPartnerName);
    }

    [RelayCommand]
    private void AddToRotation()
    {
        string targetSingerName = NameFormatting.ProperCase(NewSingerName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(targetSingerName))
        {
            if (Rotation.SelectedSinger != null)
            {
                targetSingerName = Rotation.SelectedSinger.Name;
            }
            else
            {
                return;
            }
        }

        if (SelectedSong != null)
        {
            Rotation.AddSinger(targetSingerName, SelectedSong.Title, SelectedSong.Artist, NewSingerKey, NewSingerNotes, "Local", SelectedSong.AudioPath, NewDuetPartnerName);
        }
        else if (SelectedPartyTymeTrack != null)
        {
            Rotation.AddSinger(targetSingerName, SelectedPartyTymeTrack.Title, SelectedPartyTymeTrack.Artist, NewSingerKey,
                $"[Party Tyme ID: {SelectedPartyTymeTrack.TrackId}] {NewSingerNotes}", "PartyTyme", string.Empty, NewDuetPartnerName);
        }
        else if (SelectedExternalTrack != null)
        {
            Rotation.AddSinger(targetSingerName, SelectedExternalTrack.Title, SelectedExternalTrack.Artist, NewSingerKey,
                NewSingerNotes, SelectedExternalTrack.Source, SelectedExternalTrack.Url, NewDuetPartnerName);
        }
        else if (SelectedHistoryEntry != null)
        {
            Rotation.AddSinger(targetSingerName, SelectedHistoryEntry.SongTitle, SelectedHistoryEntry.Artist, NewSingerKey,
                NewSingerNotes, SelectedHistoryEntry.Source, SelectedHistoryEntry.Link, NewDuetPartnerName);
        }
        else if (!string.IsNullOrWhiteSpace(CustomExternalUrl))
        {
            var parsed = _externalLinkService.ParseUrl(CustomExternalUrl);
            if (parsed != null)
            {
                string title = NameFormatting.ProperCase(string.IsNullOrWhiteSpace(CustomExternalTitle) ? parsed.Title : CustomExternalTitle);
                string artist = NameFormatting.ProperCase(string.IsNullOrWhiteSpace(CustomExternalArtist) ? parsed.Artist : CustomExternalArtist);
                Rotation.AddSinger(targetSingerName, title, artist, NewSingerKey, NewSingerNotes, parsed.Source, CustomExternalUrl, NewDuetPartnerName);
            }
        }
        else
        {
            Rotation.AddSinger(targetSingerName, string.Empty, string.Empty, NewSingerKey, NewSingerNotes, "Local", string.Empty, NewDuetPartnerName);
        }

        // Reset inputs
        NewSingerName = string.Empty;
        NewDuetPartnerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";
        CustomExternalUrl = string.Empty;
        CustomExternalTitle = string.Empty;
        CustomExternalArtist = string.Empty;
        SelectedHistoryEntry = null;

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
        wheel.RebuildWheel();
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

        IsExternalPerformanceActive = false;
        ExternalPerformanceSource = string.Empty;
        ExternalPerformanceUrl = string.Empty;

        string streamUrl = await _partyTymeService.GetStreamUrlAsync(track.TrackId);

        SelectedSongPath = streamUrl;
        await _mediaEngine.LoadSong(streamUrl);
        await _mediaEngine.Play();
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

        // Mark this singer as current in the static list
        foreach (var s in Rotation.Rotation)
        {
            s.IsCurrent = s.Name.Equals(singer.Name, StringComparison.OrdinalIgnoreCase);
        }
        UpdateNowNext();

        // Check if it's an external link
        if (singer.Source == "Spotify" || singer.Source == "YouTube" || singer.Source == "Amazon")
        {
            IsPlaying = false;
            CurrentSongName = $"{singer.Artist} - {singer.SongTitle} [{singer.Source}]";
            _mediaEngine.ActiveSingerName = singer.Name;
            _mediaEngine.ActiveDuetPartnerName = singer.DuetPartnerName;
            await _mediaEngine.Stop();

            // Stop background music as performance is launching externally
            _showFlow.OnKaraokeTrackStarted();

            IsExternalPerformanceActive = true;
            ExternalPerformanceSource = singer.Source;
            ExternalPerformanceUrl = singer.ExternalLink;

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
                string trackId = singer.Notes[startIdx..endIdx].Trim();

                IsPlaying = false;
                CurrentSongName = $"{singer.Artist} - {singer.SongTitle} [Party Tyme]";
                _mediaEngine.ActiveSingerName = singer.Name;
                _mediaEngine.ActiveDuetPartnerName = singer.DuetPartnerName;

                IsExternalPerformanceActive = false;
                ExternalPerformanceSource = string.Empty;
                ExternalPerformanceUrl = string.Empty;

                string streamUrl = await _partyTymeService.GetStreamUrlAsync(trackId);

                SelectedSongPath = streamUrl;
                await _mediaEngine.LoadSong(streamUrl);
                await _mediaEngine.Play();
                IsPlaying = true;

                IsPlaying = true;
                NotifyAudioPropertiesChanged();
                return;
            }
        }

        string localPath = string.Empty;
        if (singer.Source == "Local" && !string.IsNullOrWhiteSpace(singer.ExternalLink) && System.IO.File.Exists(singer.ExternalLink))
        {
            localPath = singer.ExternalLink;
        }
        else
        {
            var localMatch = _libraryService.Search($"{singer.SongTitle} {singer.Artist}").FirstOrDefault();
            if (localMatch == null && !string.IsNullOrWhiteSpace(singer.SongTitle))
            {
                localMatch = _libraryService.Search(singer.SongTitle).FirstOrDefault();
            }

            if (localMatch != null)
            {
                localPath = localMatch.AudioPath;
            }
        }

        if (!string.IsNullOrEmpty(localPath))
        {
            IsPlaying = false;
            CurrentSongName = $"{singer.Artist} - {singer.SongTitle}";
            _mediaEngine.ActiveSingerName = singer.Name;
            _mediaEngine.ActiveDuetPartnerName = singer.DuetPartnerName;

            IsExternalPerformanceActive = false;
            ExternalPerformanceSource = string.Empty;
            ExternalPerformanceUrl = string.Empty;

            SelectedSongPath = localPath;
            await _mediaEngine.LoadSong(localPath);
            await _mediaEngine.Play();
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
    private async Task PlayExternalTrack(ExternalTrack track)
    {
        if (track == null) return;

        IsPlaying = false;
        CurrentSongName = $"{track.Artist} - {track.Title} [{track.Source}]";
        await _mediaEngine.Stop();

        // Stop background music as performance is launching externally
        _showFlow.OnKaraokeTrackStarted();

        IsExternalPerformanceActive = true;
        ExternalPerformanceSource = track.Source;
        ExternalPerformanceUrl = track.Url;

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
    private async Task LoadAndPlaySelectedPerformer()
    {
        Singer? targetSinger = Rotation.SelectedSinger;

        if (targetSinger == null && !string.IsNullOrEmpty(NowSingingName) && !NowSingingName.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            targetSinger = Rotation.Rotation.FirstOrDefault(s => s.Name.Equals(NowSingingName, StringComparison.OrdinalIgnoreCase));
        }

        if (targetSinger == null) return;

        _displayService.ShowRotationWindow();
        _displayService.HighlightSinger(targetSinger);

        await PlayPerformerRequest(targetSinger);
    }

    [RelayCommand]
    private void ReopenExternalPerformanceLink()
    {
        if (string.IsNullOrWhiteSpace(ExternalPerformanceUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ExternalPerformanceUrl) { UseShellExecute = true });
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

    public System.Collections.Generic.List<string> ProjectionViews { get; } = ["Normal List", "Star Wars Crawl", "Vegas Marquee", "Vinyl Turntable"];

    public string SelectedProjectionView
    {
        get => _displayService.GetPreferences().RotationViewMode ?? "Normal List";
        set
        {
            if (SelectedProjectionView != value)
            {
                _displayService.SetRotationViewMode(value);
                OnPropertyChanged(nameof(SelectedProjectionView));

                var settingsVm = App.AppHost.Services.GetService<SettingsViewModel>();
                if (settingsVm != null && settingsVm.SelectedProjectionView != value)
                {
                    settingsVm.SelectedProjectionView = value;
                }
            }
        }
    }

    public void RaiseSelectedProjectionViewChanged()
    {
        OnPropertyChanged(nameof(SelectedProjectionView));
    }

}
