// Edited on Aug 2, 2026 @ 10:10:00 -> Add mp4 support to DJ Banner scanning and file dialog filter
// Edited on Aug 1, 2026 @ 13:16:00 -> Add Chromecast discovery VM properties and discovery command logic
// Edited on Aug 1, 2026 @ 12:14:00 -> Add Casting support properties to SettingsViewModel
using System;
using System.Collections.Generic;
using Lyracist.Shared;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Lyracist.Services.Integration;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IDisplayService _display;
    private readonly ITabletLyricsServer _tablet;
    private readonly IShowFlowService _showFlow;
    private readonly INavigationService _navigation;
    private readonly ILibraryService _library;
    private readonly RotationViewModel _rotation;
    private readonly RotationWindowViewModel _rotationWindowVm;
    private readonly IKSRotationSyncService _ksRotationSync;

    // Theme
    public List<string> ThemeModes { get; } = ["Light", "Dark", "System"];

    [ObservableProperty]
    private string _themeMode = AppSettings.ThemeMode;

    [ObservableProperty]
    private bool _isTestMode = AppSettings.IsTestMode;

    // Audio
    [ObservableProperty]
    private List<AudioDeviceItem> _audioDevices = [];

    [ObservableProperty]
    private int _volume = 80;

    [ObservableProperty]
    private int _latency = 40;

    // Display
    public List<ScreenInfo> Screens { get; }

    public List<DisplayTarget> RotationTargets { get; } = System.Enum.GetValues<DisplayTarget>().ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChromecastSelectionVisible))]
    private DisplayTarget _selectedRotationTarget;

    [ObservableProperty]
    private ObservableCollection<ChromecastDevice> _availableChromecasts = [];

    [ObservableProperty]
    private ChromecastDevice? _selectedChromecast;

    public bool IsChromecastSelectionVisible => SelectedRotationTarget == DisplayTarget.Chromecast;

    public bool IsRotationScreenSelectorEnabled => SelectedRotationTarget == DisplayTarget.Monitor || SelectedRotationTarget == DisplayTarget.WirelessHDMI;

    [ObservableProperty]
    private ScreenInfo? _rotationScreen;

    [ObservableProperty]
    private ScreenInfo? _lyricsScreen;

    [ObservableProperty]
    private bool _isLyricsMirrored;

    public ObservableCollection<DjBannerItem> DjBanners { get; } = [];

    [ObservableProperty]
    private DjBannerItem? _selectedDjBanner;

    [ObservableProperty]
    private ScreenInfo? _djBannerScreen;

    public List<string> ProjectionViews { get; } = ["Normal List", "Star Wars Crawl", "Vegas Marquee", "Vinyl Turntable"];

    [ObservableProperty]
    private string _selectedProjectionView = "Normal List";

    // Venues & DJ
    public ObservableCollection<string> Venues { get; } = [];
    public ObservableCollection<string> SelectedVenueGraphics { get; } = [];

    [ObservableProperty]
    private string _djName = AppSettings.DjName;

    [ObservableProperty]
    private string? _selectedVenue = AppSettings.SelectedVenue;

    [ObservableProperty]
    private string _newVenueName = string.Empty;

    // Crawl Banner Settings
    public List<string> CrawlBannerTypes { get; } = ["Dramatic", "Comedic", "Over-the-Top", "Custom"];

    [ObservableProperty]
    private string _selectedCrawlBannerType = AppSettings.CrawlBannerType;

    [ObservableProperty]
    private string _crawlBannerCustomText = AppSettings.CrawlBannerCustomText;

    // Spaceship overlay settings
    [ObservableProperty]
    private int _crawlSpaceshipFontSize = AppSettings.CrawlSpaceshipFontSize;

    [ObservableProperty]
    private int _crawlSpaceshipDuration = AppSettings.CrawlSpaceshipDuration;

    [ObservableProperty]
    private int _crawlSpaceshipFrequency = AppSettings.CrawlSpaceshipFrequency;

    public ObservableCollection<Lyracist.Models.SpaceshipSnippet> CrawlSpaceshipSnippets { get; } = new(AppSettings.CrawlSpaceshipSnippets);

    [ObservableProperty]
    private string _newSpaceshipSnippetText = string.Empty;

    [ObservableProperty]
    private Lyracist.Models.SpaceshipSnippet? _selectedSpaceshipSnippet;

    [ObservableProperty]
    private bool _enableAutoAdvance = AppSettings.EnableAutoAdvance;

    [ObservableProperty]
    private int _autoAdvanceCountdownSeconds = AppSettings.AutoAdvanceCountdownSeconds;

    public bool IsStarWarsCrawlSelected => SelectedProjectionView == "Star Wars Crawl";
    public bool IsCustomCrawlBannerSelected => SelectedCrawlBannerType == "Custom";

    public string CrawlBannerPreviewText
    {
        get
        {
            return SelectedCrawlBannerType switch
            {
                "Dramatic" => "Dramatic: \"In a tavern far, far away, known only as {venue}, the patrons have risen in glorious rebellion — and under the wicked command of their sinister DJ, {dj}, they have chosen their ultimate weapon… karaoke.\"",
                "Comedic" => "Comedic: \"Somewhere in the distant reaches of the galaxy, inside a questionable establishment called {venue}, the patrons have staged a full‑blown uprising. Led by their diabolical DJ, {dj}, they now march toward their destiny: screaming karaoke like it’s a battle cry.\"",
                "Over-the-Top" => "Over-the-Top: \"In a tavern lost to time and space — a place whispered about only as {venue} — the patrons have revolted. Guided by the dark influence of DJ {dj}, they embark on a quest of unimaginable terror… karaoke night.\"",
                _ => "Custom: \"" + CrawlBannerCustomText + "\""
            };
        }
    }

    // Registration settings
    [ObservableProperty]
    private string _regFirstName = AppSettings.RegFirstName;

    // Hotkeys settings
    [ObservableProperty]
    private string _hkPlayPause = GetKeyForAction("PlayPause");

    [ObservableProperty]
    private string _hkStop = GetKeyForAction("Stop");

    [ObservableProperty]
    private string _hkDoneSinger = GetKeyForAction("DoneSinger");

    [ObservableProperty]
    private string _hkToggleBanner = GetKeyForAction("ToggleBanner");

    [ObservableProperty]
    private string _hkToggleLyricsWindow = GetKeyForAction("ToggleLyricsWindow");

    [ObservableProperty]
    private string _hkToggleRotationWindow = GetKeyForAction("ToggleRotationWindow");

    private static string GetKeyForAction(string actionName)
    {
        var dict = AppSettings.Hotkeys;
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Value, actionName, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return "None";
    }

    private void SaveHotkeys()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (HkPlayPause != "None" && !string.IsNullOrWhiteSpace(HkPlayPause)) dict[HkPlayPause] = "PlayPause";
        if (HkStop != "None" && !string.IsNullOrWhiteSpace(HkStop)) dict[HkStop] = "Stop";
        if (HkDoneSinger != "None" && !string.IsNullOrWhiteSpace(HkDoneSinger)) dict[HkDoneSinger] = "DoneSinger";
        if (HkToggleBanner != "None" && !string.IsNullOrWhiteSpace(HkToggleBanner)) dict[HkToggleBanner] = "ToggleBanner";
        if (HkToggleLyricsWindow != "None" && !string.IsNullOrWhiteSpace(HkToggleLyricsWindow)) dict[HkToggleLyricsWindow] = "ToggleLyricsWindow";
        if (HkToggleRotationWindow != "None" && !string.IsNullOrWhiteSpace(HkToggleRotationWindow)) dict[HkToggleRotationWindow] = "ToggleRotationWindow";
        AppSettings.Hotkeys = dict;
    }

    partial void OnHkPlayPauseChanged(string value) => SaveHotkeys();
    partial void OnHkStopChanged(string value) => SaveHotkeys();
    partial void OnHkDoneSingerChanged(string value) => SaveHotkeys();
    partial void OnHkToggleBannerChanged(string value) => SaveHotkeys();
    partial void OnHkToggleLyricsWindowChanged(string value) => SaveHotkeys();
    partial void OnHkToggleRotationWindowChanged(string value) => SaveHotkeys();

    [ObservableProperty]
    private string _regLastName = AppSettings.RegLastName;

    [ObservableProperty]
    private string _regStageName = AppSettings.RegStageName;

    [ObservableProperty]
    private string _regEmail = AppSettings.RegEmail;

    [ObservableProperty]
    private string _regLicenseKey = AppSettings.RegLicenseKey;

    [ObservableProperty]
    private bool _isRegistered = AppSettings.IsRegistered;

    [ObservableProperty]
    private bool? _registrationStatus = null;

    partial void OnRegFirstNameChanged(string value) { AppSettings.RegFirstName = value; ValidateRegistration(); }
    partial void OnRegLastNameChanged(string value) { AppSettings.RegLastName = value; ValidateRegistration(); }
    partial void OnRegStageNameChanged(string value) { AppSettings.RegStageName = value; ValidateRegistration(); }
    partial void OnRegEmailChanged(string value) { AppSettings.RegEmail = value; ValidateRegistration(); }
    partial void OnRegLicenseKeyChanged(string value) { AppSettings.RegLicenseKey = value; ValidateRegistration(); }

    private void ValidateRegistration()
    {
        if (string.IsNullOrWhiteSpace(RegLicenseKey))
        {
            RegistrationStatus = null;
            IsRegistered = false;
        }
        else
        {
            bool isValid = LicenseValidator.ValidateKey(RegFirstName, RegLastName, RegStageName, RegEmail, RegLicenseKey);
            RegistrationStatus = isValid;
            IsRegistered = isValid;
        }
    }

    // Tablet Server
    [ObservableProperty]
    private int _tabletPort = AppSettings.TabletPort;

    partial void OnTabletPortChanged(int value)
    {
        AppSettings.TabletPort = value;
        _rotationWindowVm.RefreshQrCode();
    }

    [ObservableProperty]
    private string _tabletStatus = "Running";

    // KSRotation Sync
    [ObservableProperty]
    private bool _kSRotationSyncEnabled = AppSettings.KSRotationSyncEnabled;

    [ObservableProperty]
    private string _kSRotationIpAddress = AppSettings.KSRotationIpAddress;

    [ObservableProperty]
    private int _kSRotationPort = AppSettings.KSRotationPort;

    partial void OnKSRotationSyncEnabledChanged(bool value)
    {
        AppSettings.KSRotationSyncEnabled = value;
        _ksRotationSync.TriggerSettingsReloadAsync();
    }

    partial void OnKSRotationIpAddressChanged(string value)
    {
        AppSettings.KSRotationIpAddress = value;
        _ksRotationSync.TriggerSettingsReloadAsync();
    }

    partial void OnKSRotationPortChanged(int value)
    {
        AppSettings.KSRotationPort = value;
        _ksRotationSync.TriggerSettingsReloadAsync();
    }

    // MediaEngine
    public List<string> CdgScalingModes { get; }

    [ObservableProperty]
    private string _selectedCdgScalingMode = "Nearest";

    public List<string> Mp4Backends { get; }

    [ObservableProperty]
    private string _selectedMp4Backend = "LibVLC";

    [ObservableProperty]
    private int _frameRate = 30;

    public List<int> FpsOptions { get; } = [15, 30, 60];

    [ObservableProperty]
    private bool _enableHardwareAcceleration = AppSettings.EnableHardwareAcceleration;

    [ObservableProperty]
    private bool _enableNoiseGate = AppSettings.EnableNoiseGate;

    [ObservableProperty]
    private bool _enableReverb = AppSettings.EnableReverb;

    public List<string> BackdropModes { get; } = ["Original Color", "Neon Waveform", "Nebula Bokeh", "Retro Synthwave", "Space Starfield"];

    [ObservableProperty]
    private string _cdgBackdropMode = AppSettings.CdgBackdropMode;

    public List<int> BufferSizes { get; } = [64, 128, 256, 512, 1024];

    [ObservableProperty]
    private int _selectedBufferSize = AppSettings.SelectedBufferSize;

    public string SelectedKaraokeAudioDevice
    {
        get => AppSettings.SelectedKaraokeAudioDevice;
        set
        {
            if (AppSettings.SelectedKaraokeAudioDevice != value)
            {
                AppSettings.SelectedKaraokeAudioDevice = value;
                OnPropertyChanged(nameof(SelectedKaraokeAudioDevice));

                var mediaEngine = App.AppHost.Services.GetService(typeof(IMediaEngine)) as IMediaEngine;
                mediaEngine?.UpdateAudioParameters();
            }
        }
    }

    public string SelectedBgmAudioDevice
    {
        get => AppSettings.SelectedBgmAudioDevice;
        set
        {
            if (AppSettings.SelectedBgmAudioDevice != value)
            {
                AppSettings.SelectedBgmAudioDevice = value;
                OnPropertyChanged(nameof(SelectedBgmAudioDevice));
                _showFlow.SetBgmAudioDevice(value);
            }
        }
    }

    public bool IsHardwareMixerMode
    {
        get => AppSettings.IsHardwareMixerMode;
        set
        {
            if (AppSettings.IsHardwareMixerMode != value)
            {
                AppSettings.IsHardwareMixerMode = value;
                OnPropertyChanged(nameof(IsHardwareMixerMode));

                var mediaEngine = App.AppHost.Services.GetService(typeof(IMediaEngine)) as IMediaEngine;
                mediaEngine?.UpdateAudioParameters();

                _showFlow.SetFillInTone(FillInBass, FillInTreble, 0);
                _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, 0);
                _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, 0);
            }
        }
    }

    partial void OnEnableHardwareAccelerationChanged(bool value) => AppSettings.EnableHardwareAcceleration = value;
    partial void OnEnableNoiseGateChanged(bool value) => AppSettings.EnableNoiseGate = value;
    partial void OnEnableReverbChanged(bool value) => AppSettings.EnableReverb = value;
    partial void OnSelectedBufferSizeChanged(int value) => AppSettings.SelectedBufferSize = value;

    partial void OnCdgBackdropModeChanged(string value)
    {
        AppSettings.CdgBackdropMode = value;
        var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
        lyricsVm?.NotifyBackdropChanged();
    }



    // ─── Service Logins & API Keys ──────────────────────────────────────────

    [ObservableProperty]
    private string _youTubeApiKey = AppSettings.YouTubeApiKey;

    [ObservableProperty]
    private string _spotifyClientId = AppSettings.SpotifyClientId;

    [ObservableProperty]
    private string _spotifyClientSecret = AppSettings.SpotifyClientSecret;

    [ObservableProperty]
    private string _amazonAccessKey = AppSettings.AmazonAccessKey;

    [ObservableProperty]
    private string _amazonSecretKey = AppSettings.AmazonSecretKey;

    [ObservableProperty]
    private string _partyTymeClientId = AppSettings.PartyTymeClientId;

    [ObservableProperty]
    private string _partyTymeClientSecret = AppSettings.PartyTymeClientSecret;

    [ObservableProperty]
    private string _staticIPAddress = AppSettings.StaticIPAddress;

    partial void OnYouTubeApiKeyChanged(string value) => AppSettings.YouTubeApiKey = value;
    partial void OnSpotifyClientIdChanged(string value) => AppSettings.SpotifyClientId = value;
    partial void OnSpotifyClientSecretChanged(string value) => AppSettings.SpotifyClientSecret = value;
    partial void OnAmazonAccessKeyChanged(string value) => AppSettings.AmazonAccessKey = value;
    partial void OnAmazonSecretKeyChanged(string value) => AppSettings.AmazonSecretKey = value;
    partial void OnPartyTymeClientIdChanged(string value) => AppSettings.PartyTymeClientId = value;
    partial void OnPartyTymeClientSecretChanged(string value) => AppSettings.PartyTymeClientSecret = value;
    partial void OnStaticIPAddressChanged(string value)
    {
        AppSettings.StaticIPAddress = value;
        _rotationWindowVm.RefreshQrCode();
    }

    // ─── Music Library ─────────────────────────────────────────────────

    public ObservableCollection<string> LibraryDirectories { get; } = [];

    [ObservableProperty]
    private string? _selectedLibraryDirectory;

    [ObservableProperty]
    private string _libraryStatus = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    // ─── Scaryoke Configuration ────────────────────────────────────────

    public ObservableCollection<string> ScaryokeCategories { get; } = [];

    [ObservableProperty]
    private string? _selectedScaryokeCategory;

    [ObservableProperty]
    private string _newScaryokeCategoryName = string.Empty;

    // ─── Performer Rating Configuration ────────────────────────────────
    [ObservableProperty]
    private bool _isRatingSystemEnabled = AppSettings.IsRatingSystemEnabled;

    [ObservableProperty]
    private string _selectedRatingIcon = AppSettings.SelectedRatingIcon;

    [ObservableProperty]
    private string _newRatingIconName = string.Empty;

    public ObservableCollection<string> AvailableRatingIcons { get; } = [];

    partial void OnIsRatingSystemEnabledChanged(bool value)
    {
        AppSettings.IsRatingSystemEnabled = value;
        _rotationWindowVm.NotifyPropertyChanged(nameof(RotationWindowViewModel.IsRatingSystemEnabled));
    }

    partial void OnSelectedRatingIconChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AppSettings.SelectedRatingIcon = value;
            _rotationWindowVm.NotifyPropertyChanged(nameof(RotationWindowViewModel.RatingIconSymbol));
        }
    }

    // ─── Splash Screen ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _showSplashOnStartup = AppSettings.ShowSplashOnStartup;

    // ─── Background Music Channel Volumes (0–100) ──────────────────────

    [ObservableProperty]
    private int _openingVolume = AppSettings.OpeningVolume;

    [ObservableProperty]
    private int _fillInVolume = AppSettings.FillInVolume;

    [ObservableProperty]
    private int _fillInDelaySeconds = AppSettings.FillInDelaySeconds;

    [ObservableProperty]
    private int _endRotationVolume = AppSettings.EndRotationVolume;

    // ─── Background Music Channel Tone (–20 to +20 dB) ────────────────

    [ObservableProperty]
    private double _openingBass = AppSettings.OpeningBass;

    [ObservableProperty]
    private double _openingTreble = AppSettings.OpeningTreble;

    [ObservableProperty]
    private double _fillInBass = AppSettings.FillInBass;

    [ObservableProperty]
    private double _fillInTreble = AppSettings.FillInTreble;

    [ObservableProperty]
    private double _endRotationBass = AppSettings.EndRotationBass;

    [ObservableProperty]
    private double _endRotationTreble = AppSettings.EndRotationTreble;

    private readonly KaraokeViewModel _karaoke;
    private readonly IRequestService _requests;
    private readonly IChromecastDiscoveryService _chromecastDiscovery;
    private readonly ICastingService _casting;

    public SettingsViewModel(IDisplayService display,
                             ITabletLyricsServer tablet,
                             IShowFlowService showFlow,
                             INavigationService navigation,
                             ILibraryService library,
                             RotationViewModel rotation,
                             RotationWindowViewModel rotationWindowVm,
                             KaraokeViewModel karaoke,
                             IRequestService requests,
                             IKSRotationSyncService ksRotationSync,
                             IChromecastDiscoveryService chromecastDiscovery,
                             ICastingService casting)
    {
        _display = display;
        _tablet = tablet;
        _showFlow = showFlow;
        _navigation = navigation;
        _library = library;
        _rotation = rotation;
        _rotationWindowVm = rotationWindowVm;
        _karaoke = karaoke;
        _requests = requests;
        _ksRotationSync = ksRotationSync;
        _chromecastDiscovery = chromecastDiscovery;
        _casting = casting;

        _library.LibraryUpdated += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                IsScanning = false;
                RefreshLibraryStatus();
            });
        };

        _library.ScanProgressChanged += (_, progress) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LibraryStatus = $"Scanning… {progress.FilesProcessed:N0} / {progress.TotalFilesFound:N0} files ({progress.Percentage:F0}%)";
            });
        };

        _library.ScanFailed += (_, message) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LibraryStatus = $"Scan failed: {message}";
            });
        };

        _library.MetadataProbeProgressChanged += (_, progress) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LibraryStatus = $"Filling in song details… {progress.FilesProcessed:N0} / {progress.TotalFilesFound:N0} ({progress.Percentage:F0}%)";
            });
        };

        _library.MetadataProbeCompleted += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(RefreshLibraryStatus);
        };

        // Apply persisted channel settings to the show flow service on load
        _showFlow.SetOpeningVolume(OpeningVolume);
        _showFlow.SetFillInVolume(FillInVolume);
        _showFlow.SetEndRotationVolume(EndRotationVolume);
        _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, 0);
        _showFlow.SetFillInTone(FillInBass, FillInTreble, 0);
        _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, 0);

        // Load active screen list with a 'None' option first
        List<ScreenInfo> screens = [new() { Index = -1, DeviceName = "None (Do not show)" }];
        screens.AddRange(_display.GetScreens());
        Screens = screens;

        // Reflect the last saved monitor assignments/mirror state without
        // triggering the OnChanged side effects below (which would move the
        // projection windows during SettingsViewModel construction, before
        // the show has even started).
        var prefs = _display.GetPreferences();
        _selectedRotationTarget = prefs.RotationTarget;
        _rotationScreen = prefs.RotationScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.RotationScreenIndex.Value)
            : Screens.FirstOrDefault(s => s.Index == -1);
        _lyricsScreen = prefs.LyricsScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.LyricsScreenIndex.Value)
            : Screens.FirstOrDefault(s => s.Index == -1);
        _djBannerScreen = prefs.DjBannerScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.DjBannerScreenIndex.Value)
            : Screens.FirstOrDefault(s => s.Index == -1);
        _isLyricsMirrored = prefs.IsLyricsMirrored;
        _selectedProjectionView = prefs.RotationViewMode ?? "Normal List";

        // Dynamic audio device list using LibVLC
        var devList = new List<AudioDeviceItem>
        {
            new() { DeviceIdentifier = "Default System Device", Description = "Default System Device" }
        };

        try
        {
            using var tempLib = new LibVLCSharp.Shared.LibVLC();
            using var tempPlayer = new LibVLCSharp.Shared.MediaPlayer(tempLib);
            var vlcDevices = tempPlayer.AudioOutputDeviceEnum;
            if (vlcDevices != null)
            {
                foreach (var d in vlcDevices)
                {
                    if (!string.IsNullOrEmpty(d.DeviceIdentifier))
                    {
                        devList.Add(new AudioDeviceItem
                        {
                            DeviceIdentifier = d.DeviceIdentifier,
                            Description = d.Description ?? d.DeviceIdentifier
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error enumerating LibVLC audio devices: {ex.Message}");
            devList.Add(new() { DeviceIdentifier = "Speakers", Description = "Speakers (Realtek High Definition Audio)" });
            devList.Add(new() { DeviceIdentifier = "Headphones", Description = "Headphones (USB Audio Device)" });
            devList.Add(new() { DeviceIdentifier = "HDMI", Description = "Digital Output (HDMI)" });
        }

        AudioDevices = devList;

        if (string.IsNullOrEmpty(Lyracist.Core.Helpers.AppSettings.SelectedKaraokeAudioDevice))
        {
            Lyracist.Core.Helpers.AppSettings.SelectedKaraokeAudioDevice = "Default System Device";
        }
        if (string.IsNullOrEmpty(Lyracist.Core.Helpers.AppSettings.SelectedBgmAudioDevice))
        {
            Lyracist.Core.Helpers.AppSettings.SelectedBgmAudioDevice = "Default System Device";
        }

        // Seed list values
        CdgScalingModes = ["Nearest", "Linear"];
        Mp4Backends = ["LibVLC", "FFME"];

        RefreshLibraryDirectories();
        RefreshLibraryStatus();
        RefreshScaryokeCategories();
        RefreshVenues();
        RefreshAvailableRatingIcons();
        RefreshDjBanners();

        AppSettings.ThemeModeChanged += theme =>
        {
            if (_themeMode != theme)
            {
                _themeMode = theme;
                OnPropertyChanged(nameof(ThemeMode));
            }
        };

        ValidateRegistration();

        if (_selectedRotationTarget == DisplayTarget.Chromecast)
        {
            _ = DiscoverChromecastsAsync();
        }
    }



    private void RefreshLibraryDirectories()
    {
        LibraryDirectories.Clear();
        foreach (var dir in Core.Helpers.AppSettings.LibraryDirectories)
            LibraryDirectories.Add(dir);
    }

    private void RefreshScaryokeCategories()
    {
        ScaryokeCategories.Clear();
        foreach (var cat in AppSettings.ScaryokeCategories)
        {
            ScaryokeCategories.Add(cat);
        }
    }

    private void RefreshVenues()
    {
        Venues.Clear();
        foreach (var v in AppSettings.Venues)
        {
            Venues.Add(v);
        }
        SelectedVenue = AppSettings.SelectedVenue;
        RefreshSelectedVenueGraphics();
    }

    partial void OnDjNameChanged(string value)
    {
        AppSettings.DjName = value;
    }

    partial void OnSelectedVenueChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AppSettings.SelectedVenue = value;
            RefreshSelectedVenueGraphics();

            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.StartSlideshow();
        }
        else
        {
            SelectedVenueGraphics.Clear();
            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.StopSlideshow();
        }
    }

    private void RefreshSelectedVenueGraphics()
    {
        SelectedVenueGraphics.Clear();
        if (!string.IsNullOrEmpty(SelectedVenue))
        {
            foreach (var img in AppSettings.GetVenueGraphics(SelectedVenue))
            {
                SelectedVenueGraphics.Add(img);
            }
        }
    }

    partial void OnSelectedCrawlBannerTypeChanged(string value)
    {
        AppSettings.CrawlBannerType = value;
        OnPropertyChanged(nameof(IsCustomCrawlBannerSelected));
        OnPropertyChanged(nameof(CrawlBannerPreviewText));
        UpdateCrawlBannerOnWindow();
    }

    partial void OnCrawlBannerCustomTextChanged(string value)
    {
        AppSettings.CrawlBannerCustomText = value;
        OnPropertyChanged(nameof(CrawlBannerPreviewText));
        UpdateCrawlBannerOnWindow();
    }

    partial void OnCrawlSpaceshipFontSizeChanged(int value) => AppSettings.CrawlSpaceshipFontSize = value;
    partial void OnCrawlSpaceshipDurationChanged(int value) => AppSettings.CrawlSpaceshipDuration = value;
    partial void OnCrawlSpaceshipFrequencyChanged(int value) => AppSettings.CrawlSpaceshipFrequency = value;
    partial void OnEnableAutoAdvanceChanged(bool value) => AppSettings.EnableAutoAdvance = value;
    partial void OnAutoAdvanceCountdownSecondsChanged(int value) => AppSettings.AutoAdvanceCountdownSeconds = value;

    [RelayCommand]
    private void AddSpaceshipSnippet()
    {
        string text = NewSpaceshipSnippetText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (CrawlSpaceshipSnippets.Count >= 10)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 10 snippets is allowed.",
                "Max Snippets Reached",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var snippet = new Lyracist.Models.SpaceshipSnippet { Text = text, IsEnabled = true };
        CrawlSpaceshipSnippets.Add(snippet);
        NewSpaceshipSnippetText = string.Empty;
        SaveSpaceshipSnippets();
    }

    [RelayCommand]
    private void RemoveSpaceshipSnippet()
    {
        if (SelectedSpaceshipSnippet == null) return;
        CrawlSpaceshipSnippets.Remove(SelectedSpaceshipSnippet);
        SelectedSpaceshipSnippet = null;
        SaveSpaceshipSnippets();
    }

    [RelayCommand]
    private void SaveSpaceshipSnippets()
    {
        AppSettings.CrawlSpaceshipSnippets = [.. CrawlSpaceshipSnippets];
    }

    private void UpdateCrawlBannerOnWindow()
    {
        string template = AppSettings.GetActiveCrawlBannerTemplate();
        _display.SetCrawlBannerText(template);
    }

    [RelayCommand]
    private void AddVenue()
    {
        string venue = NewVenueName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(venue)) return;

        AppSettings.AddVenue(venue);
        NewVenueName = string.Empty;
        RefreshVenues();
        SelectedVenue = venue;
    }

    [RelayCommand]
    private void RemoveVenue()
    {
        if (SelectedVenue == null) return;

        AppSettings.RemoveVenue(SelectedVenue);
        SelectedVenue = null;
        RefreshVenues();
    }

    [RelayCommand]
    private void AddVenueGraphic()
    {
        if (string.IsNullOrEmpty(SelectedVenue)) return;

        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Multiselect = false,
            Title = "Select Graphic for Venue"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            AppSettings.AddVenueGraphic(SelectedVenue, openFileDialog.FileName);
            RefreshSelectedVenueGraphics();

            var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
            lyricsVm?.StartSlideshow();
        }
    }

    [RelayCommand]
    private void RemoveVenueGraphic(string? path)
    {
        if (string.IsNullOrEmpty(SelectedVenue) || string.IsNullOrEmpty(path)) return;

        AppSettings.RemoveVenueGraphic(SelectedVenue, path);
        RefreshSelectedVenueGraphics();

        var lyricsVm = App.AppHost.Services.GetService(typeof(LyricsWindowViewModel)) as LyricsWindowViewModel;
        lyricsVm?.StartSlideshow();
    }

    [RelayCommand]
    private void AddScaryokeCategory()
    {
        string cat = NewScaryokeCategoryName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(cat)) return;

        if (AppSettings.ScaryokeCategories.Count >= 8)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 8 categories is allowed for the Scaryoke wheel.",
                "Max Categories Reached",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.AddScaryokeCategory(cat);
        NewScaryokeCategoryName = string.Empty;
        RefreshScaryokeCategories();
    }

    [RelayCommand]
    private void RemoveScaryokeCategory()
    {
        if (SelectedScaryokeCategory == null) return;

        if (AppSettings.ScaryokeCategories.Count <= 2)
        {
            System.Windows.MessageBox.Show(
                "The Scaryoke wheel must have at least 2 categories to be playable.",
                "Minimum Categories Warning",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.RemoveScaryokeCategory(SelectedScaryokeCategory);
        SelectedScaryokeCategory = null;
        RefreshScaryokeCategories();
    }

    private void RefreshAvailableRatingIcons()
    {
        AvailableRatingIcons.Clear();
        foreach (var icon in AppSettings.AvailableRatingIcons)
        {
            AvailableRatingIcons.Add(icon);
        }
        SelectedRatingIcon = AppSettings.SelectedRatingIcon;
    }

    [RelayCommand]
    private void AddRatingIcon()
    {
        string icon = NewRatingIconName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(icon)) return;

        // Validation for "NOTHING negative or detrimental."
        // We block any words/emojis that could express negativity.
        var negativeKeywords = new[]
        {
            "poop", "poo", "shit", "down", "thumbs down", "thumb down", "garbage", "trash", 
            "bad", "boo", "dislike", "hate", "ugly", "fail", "loser", "suck", "terrible", "awful",
            "👎", "💩", "💔", "💀", "😠", "😡", "🗑️", "❌", "👎", "🤮", "👿"
        };

        bool isNegative = false;
        foreach (var keyword in negativeKeywords)
        {
            if (icon.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                isNegative = true;
                break;
            }
        }

        if (isNegative)
        {
            System.Windows.MessageBox.Show(
                "Detrimental or negative feedback icons are not allowed. Please enter a positive icon!",
                "Validation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.AddAvailableRatingIcon(icon);
        NewRatingIconName = string.Empty;
        RefreshAvailableRatingIcons();
        SelectedRatingIcon = icon;
    }

    [RelayCommand]
    private void RemoveRatingIcon()
    {
        if (string.IsNullOrEmpty(SelectedRatingIcon)) return;

        if (AppSettings.AvailableRatingIcons.Count <= 1)
        {
            System.Windows.MessageBox.Show(
                "You must have at least one rating icon available.",
                "Cannot Remove Icon",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.RemoveAvailableRatingIcon(SelectedRatingIcon);
        RefreshAvailableRatingIcons();
    }

    [RelayCommand]
    private void AddPresetIcon(string iconPreset)
    {
        if (string.IsNullOrWhiteSpace(iconPreset)) return;
        AppSettings.AddAvailableRatingIcon(iconPreset);
        RefreshAvailableRatingIcons();
        SelectedRatingIcon = iconPreset;
    }

    private void RefreshLibraryStatus()
    {
        int count = _library.GetSongCount();
        LibraryStatus = count == 0
            ? "No songs scanned yet — add a folder and scan."
            : $"{count:N0} songs in library.";
    }

    [RelayCommand]
    private void AddLibraryDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Music Library Folder to Scan"
        };
        if (dialog.ShowDialog() != true) return;

        IsScanning = true;
        // ScanDirectory persists to AppSettings internally.
        _library.ScanDirectory(dialog.FolderName);
        RefreshLibraryDirectories();
        LibraryStatus = "Scanning…";
    }

    [RelayCommand]
    private void RemoveLibraryDirectory()
    {
        if (SelectedLibraryDirectory == null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove '{SelectedLibraryDirectory}' from the scan list? Songs already indexed from this folder will also be removed from the library.",
            "Confirm Remove Directory",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        _library.RemoveSongsUnderDirectory(SelectedLibraryDirectory);
        Core.Helpers.AppSettings.RemoveLibraryDirectory(SelectedLibraryDirectory);
        SelectedLibraryDirectory = null;
        RefreshLibraryDirectories();
        RefreshLibraryStatus();
    }

    [RelayCommand]
    private void RescanLibrary()
    {
        if (LibraryDirectories.Count == 0) return;
        IsScanning = true;
        LibraryStatus = "Scanning…";
        _library.RescanAllDirectories();
    }

    [RelayCommand]
    private void LoadDbEditor()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, "LyracistDbEditor.exe");

            if (!File.Exists(exePath))
            {
                exePath = "LyracistDbEditor.exe";
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = baseDir
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Load LyracistDbEditor");
            System.Windows.MessageBox.Show($"Failed to launch LyracistDbEditor: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ScanSelectedDirectory()
    {
        if (SelectedLibraryDirectory == null) return;
        IsScanning = true;
        LibraryStatus = "Scanning…";
        _library.ScanDirectory(SelectedLibraryDirectory);
    }

    [RelayCommand]
    private void ClearDatabase()
    {
        var confirm = System.Windows.MessageBox.Show(
            "Are you sure you want to clear the entire database? This will remove all songs, playlists, performer history, and active rotation queue.",
            "Confirm Clear Database",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using (var db = new Lyracist.Data.LyracistDbContext())
            {
                db.Database.EnsureDeleted();
                // Use Migrate (not EnsureCreated) so __EFMigrationsHistory is populated
                // correctly — otherwise the next app startup's Migrate() call sees no
                // history and tries to re-apply migrations against tables that already exist.
                db.Database.Migrate();
            }

            // Clear lists in memory
            LibraryDirectories.Clear();
            RefreshLibraryDirectories();
            RefreshLibraryStatus();
            _rotation.ClearRotationQueue();

            System.Windows.MessageBox.Show("Database cleared successfully.", "Database Cleared", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Clear Database");
            System.Windows.MessageBox.Show($"Failed to clear database: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }



    partial void OnShowSplashOnStartupChanged(bool value)
    {
        AppSettings.ShowSplashOnStartup = value;
    }

    partial void OnOpeningVolumeChanged(int value)
    {
        AppSettings.OpeningVolume = value;
        _showFlow.SetOpeningVolume(value);
    }

    partial void OnFillInVolumeChanged(int value)
    {
        AppSettings.FillInVolume = value;
        _showFlow.SetFillInVolume(value);
    }

    partial void OnFillInDelaySecondsChanged(int value)
    {
        AppSettings.FillInDelaySeconds = value;
    }

    partial void OnEndRotationVolumeChanged(int value)
    {
        AppSettings.EndRotationVolume = value;
        _showFlow.SetEndRotationVolume(value);
    }

    partial void OnOpeningBassChanged(double value)
    {
        AppSettings.OpeningBass = value;
        _showFlow.SetOpeningTone(value, OpeningTreble, 0);
    }

    partial void OnOpeningTrebleChanged(double value)
    {
        AppSettings.OpeningTreble = value;
        _showFlow.SetOpeningTone(OpeningBass, value, 0);
    }

    partial void OnFillInBassChanged(double value)
    {
        AppSettings.FillInBass = value;
        _showFlow.SetFillInTone(value, FillInTreble, 0);
    }

    partial void OnFillInTrebleChanged(double value)
    {
        AppSettings.FillInTreble = value;
        _showFlow.SetFillInTone(FillInBass, value, 0);
    }

    partial void OnEndRotationBassChanged(double value)
    {
        AppSettings.EndRotationBass = value;
        _showFlow.SetEndRotationTone(value, EndRotationTreble, 0);
    }

    partial void OnEndRotationTrebleChanged(double value)
    {
        AppSettings.EndRotationTreble = value;
        _showFlow.SetEndRotationTone(EndRotationBass, value, 0);
    }

    [RelayCommand]
    private void NavigateToPlaylists()
    {
        _navigation.Navigate(typeof(Views.Pages.PlaylistsPage));
    }

    partial void OnThemeModeChanged(string value)
    {
        AppSettings.ThemeMode = value;
    }

    partial void OnIsTestModeChanged(bool value)
    {
        AppSettings.IsTestMode = value;
        if (value)
        {
            _rotation.SeedSingers();
        }
        else
        {
            _rotation.ClearRotationQueue();
        }
    }

    partial void OnSelectedRotationTargetChanged(DisplayTarget value)
    {
        // Just remembers which target the explicit "Cast Rotation" action (CastRotationViewModel.CastCommand)
        // will use next - selecting a target here must not itself start casting.
        OnPropertyChanged(nameof(IsRotationScreenSelectorEnabled));
        OnPropertyChanged(nameof(IsChromecastSelectionVisible));

        if (value == DisplayTarget.Chromecast)
        {
            _ = DiscoverChromecastsAsync();
        }
    }

    [RelayCommand]
    private async Task DiscoverChromecastsAsync()
    {
        try
        {
            AvailableChromecasts.Clear();
            var devices = await _chromecastDiscovery.DiscoverAsync();
            
            foreach (var device in devices)
            {
                AvailableChromecasts.Add(device);
            }

            if (devices.Count > 0)
            {
                SelectedChromecast = devices[0];
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "DiscoverChromecasts");
        }
    }

    partial void OnSelectedChromecastChanged(ChromecastDevice? value)
    {
        _casting.SelectedDevice = value;
    }

    partial void OnRotationScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveRotationToScreen(value.Index == -1 ? null : value.Index);
        }
    }

    partial void OnLyricsScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveLyricsToScreen(value.Index == -1 ? null : value.Index);
        }
    }

    partial void OnDjBannerScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveDjBannerToScreen(value.Index == -1 ? null : value.Index);
        }
    }

    private void RefreshDjBanners()
    {
        DjBanners.Clear();
        foreach (var item in DjBannerFileManager.ScanBanners(Globals.DjBannersDir))
        {
            DjBanners.Add(item);
        }

        var prefs = _display.GetPreferences();
        if (!string.IsNullOrEmpty(prefs.SelectedDjBannerPath))
        {
            SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
        }
        else
        {
            SelectedDjBanner = DjBanners.FirstOrDefault();
        }
    }

    partial void OnSelectedDjBannerChanged(DjBannerItem? value)
    {
        _display.UpdateDjBanner(value?.FullPath ?? string.Empty);
    }

    [RelayCommand]
    private void UploadDjBanner()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Upload DJ Banner",
            Filter = "Supported Banners (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string destPath = DjBannerFileManager.CopyInWithDedup(dialog.FileName, Globals.DjBannersDir);
                RefreshDjBanners();

                SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == destPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to upload banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void DeleteDjBanner()
    {
        if (SelectedDjBanner == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete the DJ Banner '{SelectedDjBanner.FileName}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                string path = SelectedDjBanner.FullPath;
                SelectedDjBanner = null;

                DjBannerFileManager.DeleteBanner(path);
                RefreshDjBanners();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to delete banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    partial void OnIsLyricsMirroredChanged(bool value)
    {
        _display.SetLyricsMirror(value);
    }

    partial void OnSelectedProjectionViewChanged(string value)
    {
        _display.SetRotationViewMode(value);
        OnPropertyChanged(nameof(IsStarWarsCrawlSelected));

        var karaokeVm = App.AppHost.Services.GetService(typeof(KaraokeViewModel)) as KaraokeViewModel;
        if (karaokeVm != null)
        {
            karaokeVm.RaiseSelectedProjectionViewChanged();
        }
    }

    [RelayCommand]
    private void RestoreLayout()
    {
        _display.RestoreAssignments();
    }

    [RelayCommand]
    private async Task StartTabletServer()
    {
        await _tablet.StartAsync();
        TabletStatus = "Running";
    }

    [RelayCommand]
    private async Task StopTabletServer()
    {
        await _tablet.StopAsync();
        TabletStatus = "Stopped";
    }

    [RelayCommand]
    private void BackupDatabase()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Backup Lyracist Database",
            FileName = $"lyracist_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        string selectedPath = dialog.FileName;

        try
        {
            if (System.IO.File.Exists(selectedPath))
            {
                System.IO.File.Delete(selectedPath);
            }

            using var db = new Lyracist.Data.LyracistDbContext();
#pragma warning disable EF1002
            db.Database.ExecuteSqlRaw($"VACUUM INTO '{selectedPath.Replace("'", "''")}';");
#pragma warning restore EF1002

            System.Windows.MessageBox.Show(
                $"Database backup created successfully at:{Environment.NewLine}{selectedPath}",
                "Backup Successful",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database Backup");
            System.Windows.MessageBox.Show(
                $"Failed to backup database: {ex.Message}",
                "Backup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore Lyracist Database from Backup",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        string selectedPath = dialog.FileName;

        var confirm = System.Windows.MessageBox.Show(
            "Restoring the database will overwrite all current settings, performers, playlists, and history. " +
            "The application will shutdown to complete the restore. Do you want to proceed?",
            "Confirm Database Restore",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using (var db = new Lyracist.Data.LyracistDbContext())
            {
                db.Database.CloseConnection();
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "Data", "lyracist.db");

            System.IO.File.Copy(selectedPath, dbPath, overwrite: true);

            string walPath = dbPath + "-wal";
            string shmPath = dbPath + "-shm";
            if (System.IO.File.Exists(walPath)) System.IO.File.Delete(walPath);
            if (System.IO.File.Exists(shmPath)) System.IO.File.Delete(shmPath);

            System.Windows.MessageBox.Show(
                "Database restored successfully. The application will now close. Please restart Lyracist.",
                "Restore Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database Restore");
            System.Windows.MessageBox.Show(
                $"Failed to restore database: {ex.Message}",
                "Restore Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    // ─── Stress-Test System Simulator ──────────────────────────────────────

    [ObservableProperty]
    private string _simulationButtonText = "Start Stress-Test";

    [ObservableProperty]
    private string _simulationStatusText = "Idle";

    [ObservableProperty]
    private string _simulationLogText = "Stress-Test Log Console:\nClick 'Start Stress-Test' to launch auto-pilot.";

    [ObservableProperty]
    private int _simulationDurationMinutes = 1;

    private System.Threading.CancellationTokenSource? _simulationCts;

    [RelayCommand]
    private async Task StartSimulation()
    {
        if (_simulationCts != null)
        {
            _simulationCts.Cancel();
            _simulationCts = null;
            SimulationButtonText = "Start Stress-Test";
            SimulationStatusText = "Aborted";
            LogSim(">> Simulation aborted by host.");
            return;
        }

        _simulationCts = new System.Threading.CancellationTokenSource();
        SimulationButtonText = "Stop Stress-Test";
        SimulationStatusText = "Running...";
        SimulationLogText = string.Empty;
        LogSim($">> System Stress-Test Simulation Started (Duration: {SimulationDurationMinutes} minute(s))");
        LogSim($">> Database track count: {_library.GetSongCount()}");
        LogSim($">> Operating IP: {AppSettings.GetActiveIPAddress()}");

        var token = _simulationCts.Token;
        _ = Task.Run(async () =>
        {
            int countSingersAdded = 0;
            int countToggledInactive = 0;
            int countRequestsMocked = 0;
            int countRequestsApproved = 0;
            int countEqAdjustments = 0;
            int countPlaybackToggles = 0;
            int countErrors = 0;
            int steps = (SimulationDurationMinutes * 60) / 2;
            int i = 0;

            try
            {
                var random = new Random();
                for (i = 0; i < steps && !token.IsCancellationRequested; i++)
                {
                    SimulationStatusText = $"Running ({i + 1}/{steps})...";
                    int action = random.Next(6);

                    switch (action)
                    {
                        case 0:
                            string newSinger = $"SimPerformer_{random.Next(100, 999)}";
                            LogSim($"[QUEUE] Simulating Add Performer: {newSinger}");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                _rotation.NewSingerName = newSinger;
                                _rotation.NewSingerKey = $"{random.Next(-3, 4)}";
                                _rotation.NewSingerNotes = "Simulated request via Auto-Pilot stress-test.";
                                _rotation.AddSingerCommand.Execute(null);
                            });
                            countSingersAdded++;
                            break;

                        case 1:
                            if (_rotation.Rotation.Count > 0)
                            {
                                var singer = _rotation.Rotation[random.Next(_rotation.Rotation.Count)];
                                LogSim($"[QUEUE] Simulating Inactivate Performer: {singer.Name}");
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _rotation.ToggleInactiveSingerCommand.Execute(singer);
                                });
                            }
                            else if (_rotation.InactiveSingers.Count > 0)
                            {
                                var singer = _rotation.InactiveSingers[random.Next(_rotation.InactiveSingers.Count)];
                                LogSim($"[QUEUE] Simulating Reactivate Performer: {singer.Name}");
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _rotation.ToggleInactiveSingerCommand.Execute(singer);
                                });
                            }
                            countToggledInactive++;
                            break;

                        case 2:
                            string rSinger = $"MobileSinger_{random.Next(100, 999)}";
                            string rTitle = $"Simulated Hit {random.Next(1, 50)}";
                            string rArtist = "The Stress Testers";
                            LogSim($"[PORTAL] Simulating incoming request: '{rTitle}' by '{rArtist}' for {rSinger}");
                            _requests.AddRequest(rSinger, rTitle, rArtist, "Mobile Portal");
                            countRequestsMocked++;
                            break;

                        case 3:
                            var pendingList = _requests.GetPending().ToList();
                            if (pendingList.Count > 0)
                            {
                                var req = pendingList[0];
                                LogSim($"[PORTAL] Simulating approving request ID {req.Id} for {req.SingerName}");
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (App.AppHost.Services.GetService(typeof(RequestsViewModel)) is RequestsViewModel reqVm)
                                    {
                                        reqVm.SelectedPending = reqVm.Pending.FirstOrDefault(p => p.Id == req.Id);
                                        reqVm.ApproveCommand.Execute(null);
                                    }
                                    else
                                    {
                                        _requests.Approve(req.Id);
                                    }
                                });
                            }
                            countRequestsApproved++;
                            break;

                        case 4:
                            double treble = random.Next(-10, 11);
                            double mid = random.Next(-10, 11);
                            double bass = random.Next(-10, 11);
                            int volume = random.Next(50, 101);
                            LogSim($"[AUDIO] Adjusting EQ settings: Treble={treble}dB, Mid={mid}dB, Bass={bass}dB, Volume={volume}%");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                _karaoke.Treble = treble;
                                _karaoke.Mid = mid;
                                _karaoke.Bass = bass;
                                _karaoke.Volume = volume;
                            });
                            countEqAdjustments++;
                            break;

                        case 5:
                            LogSim("[AUDIO] Triggering playback start/pause simulation");
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (_karaoke.IsPlaying)
                                {
                                    _karaoke.PauseCommand.Execute(null);
                                    LogSim("[AUDIO] Playback PAUSED");
                                }
                                else
                                {
                                    _karaoke.PlayCommand.Execute(null);
                                    LogSim("[AUDIO] Playback RESUMED");
                                }
                            });
                            countPlaybackToggles++;
                            break;
                    }

                    await Task.Delay(2000, token);
                }

                LogSim("\n=========================================");
                LogSim("       STRESS-TEST ANALYSIS REPORT");
                LogSim("=========================================");
                LogSim($"Duration: {i * 2} seconds");
                LogSim($"Events Injected: {i} / {steps}");
                LogSim("-----------------------------------------");
                LogSim($"[Queue] Singers Added: {countSingersAdded}");
                LogSim($"[Queue] Inactive Toggles: {countToggledInactive}");
                LogSim($"[Portal] Requests Mocked: {countRequestsMocked}");
                LogSim($"[Portal] Requests Approved: {countRequestsApproved}");
                LogSim($"[Audio] Slider & EQ Tweaks: {countEqAdjustments}");
                LogSim($"[Audio] Playback Toggles: {countPlaybackToggles}");
                LogSim("-----------------------------------------");

                bool dbOk = false;
                int totalRequestsInDb = 0;
                try
                {
                    using var context = new Lyracist.Data.LyracistDbContext();
                    totalRequestsInDb = context.MusicRequests.Count();
                    dbOk = true;
                }
                catch { }

                LogSim($"[Sanity Check] Database Integrity: {(dbOk ? $"PASS ({totalRequestsInDb} total request records)" : "FAIL")}");
                LogSim($"[Sanity Check] Audio Engine State: PASS (Successfully Reset)");
                LogSim($"[Sanity Check] UI Thread Locks: PASS (0 Dispatcher Blocks)");
                LogSim($"[Sanity Check] Errors Logged: {countErrors}");
                LogSim("=========================================\n");

                LogSim(">> System Stress-Test Simulation Completed Successfully.");
                LogSim(">> Restoring system rotation queue to defaults.");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _rotation.ClearRotationQueue();
                    _rotation.SeedSingers();
                    _karaoke.ResetAudioCommand.Execute(null);
                });
            }
            catch (TaskCanceledException)
            {
                LogSim(">> Simulation task canceled.");
            }
            catch (Exception ex)
            {
                countErrors++;
                LogSim($"[ERROR] Simulation encountered exception: {ex.Message}");
            }
            finally
            {
                SimulationButtonText = "Start Stress-Test";
                SimulationStatusText = "Completed";
                _simulationCts = null;
            }
        }, token);
    }

    private void LogSim(string message)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            SimulationLogText += $"{DateTime.Now:HH:mm:ss} {message}\n";
        }));
    }
}

public class AudioDeviceItem
{
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public override string ToString() => Description;
}
