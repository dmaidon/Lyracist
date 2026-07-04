using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Wpf.Ui.Appearance;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IDisplayService _display;
    private readonly ITabletLyricsServer _tablet;

    // Theme
    [ObservableProperty]
    private bool _isDarkMode = true;

    // Audio
    [ObservableProperty]
    private List<string> _audioDevices = new();

    [ObservableProperty]
    private string _selectedAudioDevice = string.Empty;

    [ObservableProperty]
    private int _volume = 80;

    [ObservableProperty]
    private int _latency = 40;

    // Display
    public List<ScreenInfo> Screens { get; }

    [ObservableProperty]
    private ScreenInfo? _rotationScreen;

    [ObservableProperty]
    private ScreenInfo? _lyricsScreen;

    // Tablet Server
    [ObservableProperty]
    private int _tabletPort = 5005;

    [ObservableProperty]
    private string _tabletStatus = "Running";

    // MediaEngine
    public List<string> CdgScalingModes { get; }

    [ObservableProperty]
    private string _selectedCdgScalingMode = "Nearest";

    public List<string> Mp4Backends { get; }

    [ObservableProperty]
    private string _selectedMp4Backend = "LibVLC";

    [ObservableProperty]
    private int _frameRate = 30;

    public SettingsViewModel(IDisplayService display,
                             ITabletLyricsServer tablet)
    {
        _display = display;
        _tablet = tablet;

        // Load active screen list
        Screens = _display.GetScreens().ToList();

        // Seed available devices
        AudioDevices = new List<string>
        {
            "Default System Device",
            "Speakers (Realtek High Definition Audio)",
            "Headphones (USB Audio Device)",
            "Digital Output (HDMI)"
        };
        SelectedAudioDevice = AudioDevices[0];

        // Seed list values
        CdgScalingModes = new List<string> { "Nearest", "Linear" };
        Mp4Backends = new List<string> { "LibVLC", "FFME" };
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        var applicationTheme = value ? ApplicationTheme.Dark : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(applicationTheme);
        Lyracist.Themes.LyracistThemeManager.Apply(value);
    }

    partial void OnRotationScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveRotationToScreen(value.Index);
        }
    }

    partial void OnLyricsScreenChanged(ScreenInfo? value)
    {
        if (value != null)
        {
            _display.MoveLyricsToScreen(value.Index);
        }
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
}
