// Edited on Aug 28, 2026 @ 09:26:00 -> Added Wi-Fi auto-detection and companion configuration support
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

    public event EventHandler<int>? OpenAudienceDisplayRequested;
    public event EventHandler? CloseAudienceDisplayRequested;
    public event EventHandler<int>? MoveHostWindowRequested;

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

        // Resolve Host monitor selection
        SelectedHostMonitor = AvailableDisplayOptions.FirstOrDefault(m => m.Index == Settings.SelectedGameMonitorIndex)
                              ?? AvailableDisplayOptions.FirstOrDefault(m => m.IsPrimary)
                              ?? AvailableDisplayOptions.FirstOrDefault();

        // Resolve Audience monitor selection (prefer secondary display if available)
        SelectedAudienceMonitor = AvailableDisplayOptions.FirstOrDefault(m => m.Index == Settings.SelectedBannerMonitorIndex)
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
            OpenAudienceDisplayRequested?.Invoke(this, targetIndex);
            IsAudienceDisplayActive = true;
            StatusMessage = $"Audience Display opened on {SelectedAudienceMonitor?.ShortLabel ?? "Screen"}.";
        }
    }

    [RelayCommand]
    public void MoveHostWindow()
    {
        if (SelectedHostMonitor != null)
        {
            MoveHostWindowRequested?.Invoke(this, SelectedHostMonitor.Index);
            StatusMessage = $"Host window moved to {SelectedHostMonitor.ShortLabel}.";
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (SelectedHostMonitor != null)
        {
            Settings.SelectedGameMonitorIndex = SelectedHostMonitor.Index;
        }

        if (SelectedAudienceMonitor != null)
        {
            Settings.SelectedBannerMonitorIndex = SelectedAudienceMonitor.Index;
        }

        if (!string.IsNullOrWhiteSpace(Settings.WifiSsid))
        {
            WifiPasswordStore.SetPasswordForSsid(Settings.WifiSsid, Settings.WifiPassword);
        }

        Settings.SelectedSourcePaths = AvailableSources.Where(s => s.IsSelected).Select(s => s.FilePath).ToList();
        _configService.SaveSettings(Settings);

        // Reload active game questions with chosen databases and packs
        await _gameStateService.LoadQuestionsFromSourcesAsync(Settings.SelectedSourcePaths);

        StatusMessage = $"Settings saved! {TotalSelectedQuestions} questions loaded across {AvailableSources.Count(s => s.IsSelected)} sources.";
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

