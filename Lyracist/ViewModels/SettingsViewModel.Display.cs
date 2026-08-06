// Created on Aug 6, 2026 @ 07:01:27 -> Split display/monitor/Chromecast settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
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
}
