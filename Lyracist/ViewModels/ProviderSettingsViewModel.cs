// Created on Sep 6, 2026 @ 12:36:00 -> ProviderSettingsViewModel for Store tab provider preferences, defaults, and target routing
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class ProviderSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _preferredProvider = "KV";

    [ObservableProperty]
    private string _preferredFileType = "MP3+G";

    [ObservableProperty]
    private bool _defaultNormalizeAudio = true;

    [ObservableProperty]
    private bool _defaultTrimSilence = true;

    [ObservableProperty]
    private bool _defaultGenerateWaveform = true;

    [ObservableProperty]
    private string _preferredTargetFolder = "Karaoke";

    [ObservableProperty]
    private string _preferredLyricsFormat = "LRC";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    public ObservableCollection<string> AvailableProviders { get; } =
    [
        "KV",
        "PT",
        "Sunfly",
        "Karaoke.com"
    ];

    public ObservableCollection<string> AvailableFileTypes { get; } =
    [
        "MP3+G",
        "MP4",
        "Audio-only"
    ];

    public ObservableCollection<string> AvailableTargetFolders { get; } =
    [
        "Karaoke",
        "Music"
    ];

    public ObservableCollection<string> AvailableLyricsFormats { get; } =
    [
        "LRC",
        "TXT"
    ];

    public event EventHandler? SettingsSaved;

    public ProviderSettingsViewModel()
    {
        LoadSettings();
    }

    public void LoadSettings()
    {
        PreferredProvider = AppSettings.PreferredProvider;
        PreferredFileType = AppSettings.PreferredFileType;
        DefaultNormalizeAudio = AppSettings.DefaultNormalizeAudio;
        DefaultTrimSilence = AppSettings.DefaultTrimSilence;
        DefaultGenerateWaveform = AppSettings.DefaultGenerateWaveform;
        PreferredTargetFolder = AppSettings.PreferredTargetFolder;
        PreferredLyricsFormat = AppSettings.PreferredLyricsFormat;
        HasStatusMessage = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        AppSettings.PreferredProvider = PreferredProvider;
        AppSettings.PreferredFileType = PreferredFileType;
        AppSettings.DefaultNormalizeAudio = DefaultNormalizeAudio;
        AppSettings.DefaultTrimSilence = DefaultTrimSilence;
        AppSettings.DefaultGenerateWaveform = DefaultGenerateWaveform;
        AppSettings.PreferredTargetFolder = PreferredTargetFolder;
        AppSettings.PreferredLyricsFormat = PreferredLyricsFormat;

        // Keep folder watcher pipeline defaults in sync
        AppSettings.StoreNormalizeAudioOnImport = DefaultNormalizeAudio;
        AppSettings.StoreTrimSilenceOnImport = DefaultTrimSilence;
        AppSettings.StoreGenerateWaveformOnImport = DefaultGenerateWaveform;

        StatusMessage = "Provider settings saved successfully!";
        HasStatusMessage = true;

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ResetToDefaults()
    {
        PreferredProvider = "KV";
        PreferredFileType = "MP3+G";
        DefaultNormalizeAudio = true;
        DefaultTrimSilence = true;
        DefaultGenerateWaveform = true;
        PreferredTargetFolder = "Karaoke";
        PreferredLyricsFormat = "LRC";

        SaveSettings();
        StatusMessage = "Settings reset to default values.";
        HasStatusMessage = true;
    }
}
