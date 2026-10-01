// Edited on Sep 22, 2026 @ 08:49:00 -> Auto-toggle between DJ Banner and Rotation when on the same screen
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Services.Display;
using Lyracist.Services.Integration;
using Lyracist.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    public bool IsLyricsActive
    {
        get => _display.IsLyricsActive;
        set
        {
            if (_display.IsLyricsActive != value)
            {
                _display.IsLyricsActive = value;
                OnPropertyChanged(nameof(IsLyricsActive));
            }
        }
    }

    public bool IsRotationActive
    {
        get => _display.IsRotationActive;
        set
        {
            if (_display.IsRotationActive != value)
            {
                if (value && _display.IsDjBannerActive &&
                    _display.GetPreferences().RotationScreenIndex.HasValue &&
                    _display.GetPreferences().RotationScreenIndex == _display.GetPreferences().DjBannerScreenIndex)
                {
                    _display.IsDjBannerActive = false;
                    OnPropertyChanged(nameof(IsDjBannerActive));
                }
                _display.IsRotationActive = value;
                OnPropertyChanged(nameof(IsRotationActive));
            }
        }
    }

    public bool IsDjBannerActive
    {
        get => _display.IsDjBannerActive;
        set
        {
            if (_display.IsDjBannerActive != value)
            {
                if (value && _display.IsRotationActive &&
                    _display.GetPreferences().DjBannerScreenIndex.HasValue &&
                    _display.GetPreferences().DjBannerScreenIndex == _display.GetPreferences().RotationScreenIndex)
                {
                    _display.IsRotationActive = false;
                    OnPropertyChanged(nameof(IsRotationActive));
                }
                _display.IsDjBannerActive = value;
                OnPropertyChanged(nameof(IsDjBannerActive));
            }
        }
    }

    public bool IsDjBannerQrCodeEnabled
    {
        get => AppSettings.IsDjBannerQrCodeEnabled;
        set
        {
            if (AppSettings.IsDjBannerQrCodeEnabled != value)
            {
                AppSettings.IsDjBannerQrCodeEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool WelcomeScreenEnabled
    {
        get => AppSettings.WelcomeScreenEnabled;
        set
        {
            if (AppSettings.WelcomeScreenEnabled != value)
            {
                AppSettings.WelcomeScreenEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>How long the "Welcome to our new performer" screen stays up, in seconds (3-120).</summary>
    public int WelcomeScreenSeconds
    {
        get => WelcomeScreenService.ClampSeconds(AppSettings.WelcomeScreenSeconds);
        set
        {
            int clamped = WelcomeScreenService.ClampSeconds(value);
            if (AppSettings.WelcomeScreenSeconds != clamped)
            {
                AppSettings.WelcomeScreenSeconds = clamped;
            }
            // Always notify so a TextBox holding an out-of-range entry snaps back to the clamped value.
            OnPropertyChanged();
        }
    }

    /// <summary>Screens the welcome can be shown on: the default (rotation & DJ banner screens) or one monitor.</summary>
    public List<WelcomeScreenChoice> WelcomeScreenChoices { get; } = WelcomeScreenService.GetScreenChoices();

    /// <summary>Device name of the monitor for the welcome; empty means the rotation & DJ banner screens.</summary>
    public string WelcomeScreenMonitor
    {
        get => AppSettings.WelcomeScreenMonitor;
        set
        {
            string monitor = value ?? string.Empty;
            if (AppSettings.WelcomeScreenMonitor != monitor)
            {
                AppSettings.WelcomeScreenMonitor = monitor;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void PreviewWelcomeScreen()
    {
        WelcomeScreenService.Instance.Seconds = WelcomeScreenSeconds;
        WelcomeScreenService.Instance.TargetMonitorDevice = WelcomeScreenMonitor;
        WelcomeScreenService.Instance.Preview();
    }

    public bool ShowQrCodeOnRotationScreen
    {
        get => AppSettings.ShowQrCodeOnRotationScreen;
        set
        {
            if (AppSettings.ShowQrCodeOnRotationScreen != value)
            {
                AppSettings.ShowQrCodeOnRotationScreen = value;
                OnPropertyChanged();
                _rotationWindowVm.NotifyPropertyChanged(nameof(RotationWindowViewModel.ShowQrCodeOnRotationScreen));
            }
        }
    }

    public bool ShowQrCodeOnLyricsScreen
    {
        get => AppSettings.ShowQrCodeOnLyricsScreen;
        set
        {
            if (AppSettings.ShowQrCodeOnLyricsScreen != value)
            {
                AppSettings.ShowQrCodeOnLyricsScreen = value;
                OnPropertyChanged();
                _lyricsWindowVm?.NotifyPropertyChanged(nameof(LyricsWindowViewModel.ShowQrCodeOnLyricsScreen));
            }
        }
    }

    private void OnScreenAssignmentsChanged()
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            var prefs = _display.GetPreferences();

            var currentLyricsScreen = prefs.LyricsScreenIndex.HasValue
                ? Screens.FirstOrDefault(s => s.Index == prefs.LyricsScreenIndex.Value)
                : Screens.FirstOrDefault(s => s.Index == -1);

            var currentRotationScreen = prefs.RotationScreenIndex.HasValue
                ? Screens.FirstOrDefault(s => s.Index == prefs.RotationScreenIndex.Value)
                : Screens.FirstOrDefault(s => s.Index == -1);

            var currentDjBannerScreen = prefs.DjBannerScreenIndex.HasValue
                ? Screens.FirstOrDefault(s => s.Index == prefs.DjBannerScreenIndex.Value)
                : Screens.FirstOrDefault(s => s.Index == -1);

#pragma warning disable MVVMTK0034
            if (_lyricsScreen != currentLyricsScreen)
            {
                _lyricsScreen = currentLyricsScreen;
                OnPropertyChanged(nameof(LyricsScreen));
            }
            if (_rotationScreen != currentRotationScreen)
            {
                _rotationScreen = currentRotationScreen;
                OnPropertyChanged(nameof(RotationScreen));
            }
            if (_djBannerScreen != currentDjBannerScreen)
            {
                _djBannerScreen = currentDjBannerScreen;
                OnPropertyChanged(nameof(DjBannerScreen));
            }
#pragma warning restore MVVMTK0034

            OnPropertyChanged(nameof(IsLyricsActive));
            OnPropertyChanged(nameof(IsRotationActive));
            OnPropertyChanged(nameof(IsDjBannerActive));

            if (SelectedDjBanner?.FullPath != prefs.SelectedDjBannerPath)
            {
                SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
            }
        });
    }
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

    [ObservableProperty]
    private ScreenInfo? _djBannerScreen;

    public static readonly IReadOnlyList<string> AllProjectionViews =
    [
        "Normal List",
        "Star Wars Crawl",
        "Vegas Marquee",
        "Vinyl Turntable",
        "Disco Ball",
        "Synthwave Grid",
        "Concert Festival Lineup",
        "Casino Slot Reels",
        "Jukebox",
        "Stadium Jumbotron",
        "Movie Theater 'Now Showing'"
    ];

    public List<string> ProjectionViews { get; } = [.. AllProjectionViews];

    [ObservableProperty]
    private string _selectedProjectionView = "Normal List";

    /// <summary>When true, the rotation display automatically cycles through the enabled entries in
    /// <see cref="ProjectionRotationSchedule"/> instead of staying on one fixed screen.</summary>
    [ObservableProperty]
    private bool _autoRotateProjectionViews;

    partial void OnAutoRotateProjectionViewsChanged(bool value)
    {
        _display.SetAutoRotateProjectionViews(value);
    }

    /// <summary>"Reduced projection effects" for weaker venue PCs: fewer particles and no
    /// per-element blur effects on the rotation display's themed views.</summary>
    [ObservableProperty]
    private bool _reducedProjectionEffects;

    partial void OnReducedProjectionEffectsChanged(bool value)
    {
        _display.SetReducedProjectionEffects(value);
    }

    /// <summary>Single duration (in seconds) that each randomly chosen screen stays visible before automatically changing.</summary>
    [ObservableProperty]
    private int _autoRotateDurationSeconds = 180;

    partial void OnAutoRotateDurationSecondsChanged(int value)
    {
        if (value < 5) AutoRotateDurationSeconds = 5;
        _display.SetAutoRotateDurationSeconds(AutoRotateDurationSeconds);
    }

    [RelayCommand]
    private void SelectAllProjectionScreens()
    {
        foreach (var entry in ProjectionRotationSchedule)
        {
            entry.IsEnabled = true;
        }
        _display.SetProjectionRotationSchedule([.. ProjectionRotationSchedule]);
    }

    [RelayCommand]
    private void ClearAllProjectionScreens()
    {
        foreach (var entry in ProjectionRotationSchedule)
        {
            entry.IsEnabled = false;
        }
        _display.SetProjectionRotationSchedule([.. ProjectionRotationSchedule]);
    }

    /// <summary>DJ-configured schedule of which projection views participate in the automatic rotation
    /// and how long each stays up. Always has exactly one row per <see cref="ProjectionViews"/> entry.</summary>
    public ObservableCollection<ProjectionRotationEntry> ProjectionRotationSchedule { get; } = [];

    /// <summary>Rebuilds <see cref="ProjectionRotationSchedule"/> from the saved schedule, carrying over
    /// the DJ's enabled/duration choices by view name so a newly-added view (e.g. "Disco Ball") picks
    /// up a sane default instead of being dropped.</summary>
    private void LoadProjectionRotationSchedule(List<ProjectionRotationEntry>? saved)
    {
        saved ??= [];

        foreach (var existingEntry in ProjectionRotationSchedule)
        {
            existingEntry.PropertyChanged -= ProjectionRotationEntry_PropertyChanged;
        }
        ProjectionRotationSchedule.Clear();

        foreach (string viewName in ProjectionViews)
        {
            var match = saved.FirstOrDefault(e => e.ViewName == viewName);
            var entry = new ProjectionRotationEntry
            {
                ViewName = viewName,
                IsEnabled = match?.IsEnabled ?? false,
                DurationSeconds = match?.DurationSeconds > 0 ? match.DurationSeconds : 30
            };
            entry.PropertyChanged += ProjectionRotationEntry_PropertyChanged;
            ProjectionRotationSchedule.Add(entry);
        }
    }

    private void ProjectionRotationEntry_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _display.SetProjectionRotationSchedule([.. ProjectionRotationSchedule]);
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

    public ObservableCollection<string> ConnectInstructionScreens { get; } = [];

    public string ConnectInstructionsScreen
    {
        get => AppSettings.ConnectInstructionsScreen;
        set
        {
            if (AppSettings.ConnectInstructionsScreen != value)
            {
                AppSettings.ConnectInstructionsScreen = value;
                OnPropertyChanged();
                RefreshConnectInstructionsBanner();
                _display.RefreshActiveBanner();
            }
        }
    }

// Edited on Aug 11, 2026 -> Suppress Wi-Fi password auto-population when Debugger.IsAttached in VS IDE, but retain in the field
    public string WifiPassword
    {
        get
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return string.Empty;
            }
            string? ssid = WifiHelper.GetConnectedSsid();
            string savedPwd = !string.IsNullOrWhiteSpace(ssid) ? WifiPasswordStore.GetPasswordForSsid(ssid) : string.Empty;
            return !string.IsNullOrEmpty(savedPwd) ? savedPwd : AppSettings.WifiPassword;
        }
        set
        {
            if (AppSettings.WifiPassword != value)
            {
                AppSettings.WifiPassword = value;
                string? ssid = WifiHelper.GetConnectedSsid();
                if (!string.IsNullOrWhiteSpace(ssid))
                {
                    WifiPasswordStore.SetPasswordForSsid(ssid, value);
                }
                OnPropertyChanged();
                RefreshConnectInstructionsBanner();
                _display.RefreshActiveBanner();
            }
        }
    }

    public void RefreshConnectInstructionScreens()
    {
        ConnectInstructionScreens.Clear();
        ConnectInstructionScreens.Add("None");
        ConnectInstructionScreens.Add("All Screens / Monitors");
        foreach (var screen in Screens)
        {
            ConnectInstructionScreens.Add(screen.DisplayName);
        }
    }

    private void RefreshConnectInstructionsBanner()
    {
        try
        {
            string localIp = AppSettings.GetLocalIPAddress();
            string requestUrl = $"http://{localIp}:8080/request";
            var (w, h) = GetTargetScreenResolution(ConnectInstructionsScreen);
            DjBannerFileManager.CreateConnectInstructionsBannerPng(
                System.IO.Path.Combine(Globals.EventBannersDir, "ConnectInstructions.png"),
                WifiHelper.GetConnectedSsid() ?? string.Empty,
                AppSettings.WifiPassword,
                requestUrl,
                w, h);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "SettingsViewModel.RefreshConnectInstructionsBanner");
        }
    }

    private static (int Width, int Height) GetTargetScreenResolution(string screenSelection)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(screenSelection) || screenSelection.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return (1920, 1080);
            }

            if (screenSelection.Equals("All Screens / Monitors", StringComparison.OrdinalIgnoreCase))
            {
                int maxW = 1920, maxH = 1080;
                foreach (var s in System.Windows.Forms.Screen.AllScreens)
                {
                    if (s.Bounds.Width > maxW) maxW = s.Bounds.Width;
                    if (s.Bounds.Height > maxH) maxH = s.Bounds.Height;
                }
                return (maxW, maxH);
            }

            // screenSelection is a ConnectInstructionScreens entry, formatted as screen.DisplayName
            // ("Screen {n} - {DeviceName}") — match by checking it embeds the monitor's DeviceName.
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                if (screenSelection.Contains(s.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return (s.Bounds.Width, s.Bounds.Height);
                }
            }
        }
        catch
        {
            // Ignore screen enumeration errors
        }
        return (1920, 1080);
    }

    public ObservableCollection<Lyracist.Shared.SpecialEventConfig> SpecialEventsList { get; } = [];
    public ObservableCollection<string> AvailableEventBannerFiles { get; } = [];

    public void RefreshSpecialEventsList()
    {
        RefreshConnectInstructionScreens();
        SpecialEventsList.Clear();
        foreach (var ev in AppSettings.SpecialEvents)
        {
            SpecialEventsList.Add(ev);
        }
    }

    public void RefreshAvailableEventBannerFiles()
    {
        AvailableEventBannerFiles.Clear();
        AvailableEventBannerFiles.Add("None");
        foreach (var item in DjBannerFileManager.ScanBanners(Globals.EventBannersDir))
        {
            AvailableEventBannerFiles.Add(item.FileName);
        }
    }

    [RelayCommand]
    private void SaveSpecialEvents()
    {
        AppSettings.SpecialEvents = SpecialEventsList.ToList();
        // Notify display service
        _display.UpdateSpecialEvent(_display.GetPreferences().SelectedSpecialEvent);
    }

    [RelayCommand]
    private void LaunchTrivia()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = System.IO.Path.Combine(baseDir, "Lyracist.Trivia.exe");

            if (!System.IO.File.Exists(exePath))
            {
                string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "Lyracist.Trivia", "bin", "Debug", "net9.0-windows", "Lyracist.Trivia.exe"));
                if (System.IO.File.Exists(devPath))
                {
                    exePath = devPath;
                }
                else
                {
                    exePath = "Lyracist.Trivia.exe";
                }
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(exePath) ?? baseDir
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "SettingsViewModel.LaunchTrivia");
        }
    }

    [RelayCommand]
    private void PreviewConnectInstructions()
    {
        try
        {
            string bannerPath = System.IO.Path.Combine(Globals.EventBannersDir, "ConnectInstructions.png");
            if (System.IO.File.Exists(bannerPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(bannerPath) { UseShellExecute = true });
            }
            else
            {
                RefreshConnectInstructionsBanner();
                if (System.IO.File.Exists(bannerPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(bannerPath) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "SettingsViewModel.PreviewConnectInstructions");
        }
    }
}
