// Edited on Aug 18, 2026 @ 13:24:00 -> Add FloatCurrentSingerToTop setting property
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

    [ObservableProperty]
    private bool _floatCurrentSingerToTop = AppSettings.FloatCurrentSingerToTop;

    partial void OnFloatCurrentSingerToTopChanged(bool value)
    {
        AppSettings.FloatCurrentSingerToTop = value;
        _rotation.FloatCurrentSingerToTop = value;
    }

    // Audio
    [ObservableProperty]
    private List<AudioDeviceItem> _audioDevices = [];

    [ObservableProperty]
    private int _volume = 80;

    [ObservableProperty]
    private int _latency = 40;

    [ObservableProperty]
    private bool _enableAutoAdvance = AppSettings.EnableAutoAdvance;

    [ObservableProperty]
    private int _autoAdvanceCountdownSeconds = AppSettings.AutoAdvanceCountdownSeconds;

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

        _display.ScreenAssignmentsChanged += OnScreenAssignmentsChanged;

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
            Lyracist.Shared.Globals.LogError("Lyracist", "Error enumerating LibVLC audio devices", ex);
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
        RefreshSpecialEventsList();
        RefreshAvailableEventBannerFiles();

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



    partial void OnEnableAutoAdvanceChanged(bool value) => AppSettings.EnableAutoAdvance = value;
    partial void OnAutoAdvanceCountdownSecondsChanged(int value) => AppSettings.AutoAdvanceCountdownSeconds = value;


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

}

public class AudioDeviceItem
{
    public string DeviceIdentifier { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public override string ToString() => Description;
}
