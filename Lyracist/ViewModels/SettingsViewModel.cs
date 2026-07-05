using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Services.Tablet;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IDisplayService _display;
    private readonly ITabletLyricsServer _tablet;
    private readonly IOccasionService _occasions;
    private readonly IShowFlowService _showFlow;
    private readonly INavigationService _navigation;

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

    [ObservableProperty]
    private bool _isLyricsMirrored;

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

    // Special Occasion editor
    public ObservableCollection<OccasionNode> OccasionCategories { get; } = new();
    public ObservableCollection<OccasionNode> OccasionItems { get; } = new();

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

    // ─── Splash Screen ─────────────────────────────────────────────────

    [ObservableProperty]
    private bool _showSplashOnStartup = AppSettings.ShowSplashOnStartup;

    // ─── Background Music Channel Volumes (0–100) ──────────────────────

    [ObservableProperty]
    private int _openingVolume = AppSettings.OpeningVolume;

    [ObservableProperty]
    private int _fillInVolume = AppSettings.FillInVolume;

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

    public SettingsViewModel(IDisplayService display,
                             ITabletLyricsServer tablet,
                             IOccasionService occasions,
                             IShowFlowService showFlow,
                             INavigationService navigation)
    {
        _display = display;
        _tablet = tablet;
        _occasions = occasions;
        _showFlow = showFlow;
        _navigation = navigation;

        // Apply persisted channel settings to the show flow service on load
        _showFlow.SetOpeningVolume(OpeningVolume);
        _showFlow.SetFillInVolume(FillInVolume);
        _showFlow.SetEndRotationVolume(EndRotationVolume);
        _showFlow.SetOpeningTone(OpeningBass, OpeningTreble, 0);
        _showFlow.SetFillInTone(FillInBass, FillInTreble, 0);
        _showFlow.SetEndRotationTone(EndRotationBass, EndRotationTreble, 0);

        // Load active screen list
        Screens = _display.GetScreens().ToList();

        // Reflect the last saved monitor assignments/mirror state without
        // triggering the OnChanged side effects below (which would move the
        // projection windows during SettingsViewModel construction, before
        // the show has even started).
        var prefs = _display.GetPreferences();
        _rotationScreen = prefs.RotationScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.RotationScreenIndex.Value)
            : null;
        _lyricsScreen = prefs.LyricsScreenIndex.HasValue
            ? Screens.FirstOrDefault(s => s.Index == prefs.LyricsScreenIndex.Value)
            : null;
        _isLyricsMirrored = prefs.IsLyricsMirrored;

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

        RefreshOccasionCategories();
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

    partial void OnIsLyricsMirroredChanged(bool value)
    {
        _display.SetLyricsMirror(value);
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
}
