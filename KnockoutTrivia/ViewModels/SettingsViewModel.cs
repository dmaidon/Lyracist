// Edited on Aug 28, 2026 @ 11:14:30 -> Added persistent DeviceName monitor selection and display event payload
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Lyracist.Shared;

namespace KnockoutTrivia.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IDisplayService _displayService;
    private readonly ITriviaDataService _triviaDataService;
    private readonly IGameStateService _gameStateService;

    [ObservableProperty]
    private KnockoutSettings _settings;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _totalSelectedQuestions;

    [ObservableProperty]
    private DisplayMonitorOption? _selectedHostMonitor;

    [ObservableProperty]
    private DisplayMonitorOption? _selectedAudienceMonitor;

    [ObservableProperty]
    private bool _isAudienceDisplayActive;

    public ObservableCollection<DisplayMonitorOption> AvailableDisplayOptions { get; } = [];
    public ObservableCollection<SelectableTriviaSource> AvailableSources { get; } = [];

    public event EventHandler<(int Index, string? Device)>? OpenAudienceDisplayRequested;
    public event EventHandler? CloseAudienceDisplayRequested;
    public event EventHandler<(int Index, string? Device)>? MoveHostWindowRequested;

    public SettingsViewModel(
        IConfigService configService,
        IDisplayService displayService,
        ITriviaDataService triviaDataService,
        IGameStateService gameStateService)
    {
        _configService = configService;
        _displayService = displayService;
        _triviaDataService = triviaDataService;
        _gameStateService = gameStateService;

        _settings = _configService.LoadSettings();

        // Auto-detect Wi-Fi if not configured
        if (string.IsNullOrWhiteSpace(_settings.WifiSsid))
        {
            string? detected = WifiHelper.GetConnectedSsid();
            if (!string.IsNullOrWhiteSpace(detected))
            {
                _settings.WifiSsid = detected;
                _settings.WifiPassword = WifiPasswordStore.GetPasswordForSsid(detected);
            }
        }

        RefreshMonitors();
        _ = LoadSourcesAsync();
    }

    [RelayCommand]
    public void DetectWifi()
    {
        string? ssid = WifiHelper.GetConnectedSsid();
        if (!string.IsNullOrWhiteSpace(ssid))
        {
            Settings.WifiSsid = ssid;
            Settings.WifiPassword = WifiPasswordStore.GetPasswordForSsid(ssid);
            OnPropertyChanged(nameof(Settings));
            StatusMessage = $"Detected Wi-Fi network: {ssid}";
        }
        else
        {
            StatusMessage = "No active Wi-Fi interface detected.";
        }
    }

    [RelayCommand]
    public async Task LoadSourcesAsync()
    {
        AvailableSources.Clear();
        var sources = await _triviaDataService.GetAvailableSourcesAsync();

        var savedPaths = new HashSet<string>(Settings.SelectedSourcePaths ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var src in sources)
        {
            if (savedPaths.Count > 0)
            {
                src.IsSelected = savedPaths.Contains(src.FilePath);
            }
            AvailableSources.Add(src);
        }

        UpdateSelectedQuestionsCount();
    }

    [RelayCommand]
    public void SelectAllSources()
    {
        foreach (var s in AvailableSources)
        {
            s.IsSelected = true;
        }
        UpdateSelectedQuestionsCount();
    }

    [RelayCommand]
    public void DeselectAllSources()
    {
        foreach (var s in AvailableSources)
        {
            s.IsSelected = false;
        }
        UpdateSelectedQuestionsCount();
    }

    [RelayCommand]
    public void UpdateSelectedQuestionsCount()
    {
        TotalSelectedQuestions = AvailableSources.Where(s => s.IsSelected).Sum(s => s.QuestionCount);
    }

    [RelayCommand]
    public void RefreshMonitors()
    {
        AvailableDisplayOptions.Clear();
        var options = _displayService.GetDisplayOptions();
        foreach (var opt in options)
        {
            AvailableDisplayOptions.Add(opt);
        }

        // Resolve Host monitor selection (device name first, then index, then primary)
        SelectedHostMonitor = AvailableDisplayOptions.FirstOrDefault(m => !string.IsNullOrEmpty(Settings.SelectedGameMonitorDevice) && string.Equals(m.DeviceName, Settings.SelectedGameMonitorDevice, StringComparison.OrdinalIgnoreCase))
                              ?? AvailableDisplayOptions.FirstOrDefault(m => m.Index == Settings.SelectedGameMonitorIndex)
                              ?? AvailableDisplayOptions.FirstOrDefault(m => m.IsPrimary)
                              ?? AvailableDisplayOptions.FirstOrDefault();

        // Resolve Audience monitor selection (device name first, then index, then secondary display)
        SelectedAudienceMonitor = AvailableDisplayOptions.FirstOrDefault(m => !string.IsNullOrEmpty(Settings.SelectedBannerMonitorDevice) && string.Equals(m.DeviceName, Settings.SelectedBannerMonitorDevice, StringComparison.OrdinalIgnoreCase))
                                  ?? AvailableDisplayOptions.FirstOrDefault(m => m.Index == Settings.SelectedBannerMonitorIndex)
                                  ?? AvailableDisplayOptions.FirstOrDefault(m => !m.IsPrimary)
                                  ?? AvailableDisplayOptions.LastOrDefault();
    }

    [RelayCommand]
    public void ToggleAudienceDisplay()
    {
        if (IsAudienceDisplayActive)
        {
            CloseAudienceDisplayRequested?.Invoke(this, EventArgs.Empty);
            IsAudienceDisplayActive = false;
            StatusMessage = "Audience Display closed.";
        }
        else
        {
            int targetIndex = SelectedAudienceMonitor?.Index ?? 1;
            string? targetDevice = SelectedAudienceMonitor?.DeviceName;
            OpenAudienceDisplayRequested?.Invoke(this, (targetIndex, targetDevice));
            IsAudienceDisplayActive = true;
            StatusMessage = $"Audience Display opened on {SelectedAudienceMonitor?.ShortLabel ?? "Screen"}.";
        }
    }

    [RelayCommand]
    public void MoveHostWindow()
    {
        if (SelectedHostMonitor != null)
        {
            MoveHostWindowRequested?.Invoke(this, (SelectedHostMonitor.Index, SelectedHostMonitor.DeviceName));
            StatusMessage = $"Host window moved to {SelectedHostMonitor.ShortLabel}.";
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (SelectedHostMonitor != null)
        {
            Settings.SelectedGameMonitorIndex = SelectedHostMonitor.Index;
            Settings.SelectedGameMonitorDevice = SelectedHostMonitor.DeviceName;
        }

        if (SelectedAudienceMonitor != null)
        {
            Settings.SelectedBannerMonitorIndex = SelectedAudienceMonitor.Index;
            Settings.SelectedBannerMonitorDevice = SelectedAudienceMonitor.DeviceName;
        }

        if (!string.IsNullOrWhiteSpace(Settings.WifiSsid))
        {
            WifiPasswordStore.SetPasswordForSsid(Settings.WifiSsid, Settings.WifiPassword);
        }

        // Only reload/reshuffle the question deck when the actual source selection changed -
        // saving unrelated settings (Wi-Fi, monitors) mid-game must not discard game progress.
        var previousSources = new HashSet<string>(Settings.SelectedSourcePaths ?? [], StringComparer.OrdinalIgnoreCase);
        var newSources = AvailableSources.Where(s => s.IsSelected).Select(s => s.FilePath).ToList();
        bool sourcesChanged = !previousSources.SetEquals(newSources);

        Settings.SelectedSourcePaths = newSources;
        bool saved = _configService.SaveSettings(Settings);

        if (sourcesChanged)
        {
            await _gameStateService.LoadQuestionsFromSourcesAsync(newSources);
            StatusMessage = saved
                ? $"Settings saved! {TotalSelectedQuestions} questions loaded across {AvailableSources.Count(s => s.IsSelected)} sources."
                : "Question sources updated, but settings failed to save to disk.";
        }
        else
        {
            StatusMessage = saved ? "Settings saved!" : "Settings failed to save to disk.";
        }
    }

    [RelayCommand]
    public async Task ResetDefaultsAsync()
    {
        Settings = new KnockoutSettings();
        SelectAllSources();
        RefreshMonitors();
        await SaveSettingsAsync();
        StatusMessage = "Restored default settings, monitors, and all databases.";
    }
}


