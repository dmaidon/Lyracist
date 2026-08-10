// Edited on Aug 8, 2026 @ 19:20:10 -> Add Special Event Banners list and available files properties
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
                _display.IsDjBannerActive = value;
                OnPropertyChanged(nameof(IsDjBannerActive));
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

    public List<string> ProjectionViews { get; } = ["Normal List", "Star Wars Crawl", "Vegas Marquee", "Vinyl Turntable"];

    [ObservableProperty]
    private string _selectedProjectionView = "Normal List";

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
            }
        }
    }

// Edited on Aug 10, 2026 @ 13:19:00 -> Suppress Wi-Fi password auto-population when running under Visual Studio Debugger, but retain in the field
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
        catch
        {
            // Ignore background rendering exceptions
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

            if (screenSelection.Equals("All Screens", StringComparison.OrdinalIgnoreCase) ||
                screenSelection.Equals("All Screens / Monitors", StringComparison.OrdinalIgnoreCase))
            {
                int maxW = 1920, maxH = 1080;
                foreach (var s in System.Windows.Forms.Screen.AllScreens)
                {
                    if (s.Bounds.Width > maxW) maxW = s.Bounds.Width;
                    if (s.Bounds.Height > maxH) maxH = s.Bounds.Height;
                }
                return (maxW, maxH);
            }

            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                if (s.DeviceName.Equals(screenSelection, StringComparison.OrdinalIgnoreCase) ||
                    screenSelection.Contains(s.DeviceName) ||
                    s.Bounds.ToString().Contains(screenSelection))
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
}
