// Edited on Oct 31, 2026 @ 10:52:00 -> Add ImportPackCommand and AvailableCategories filter synchronization
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Lyracist.Shared;
using Microsoft.Win32;

namespace KnockoutTrivia.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IConfigService _configService;
    private readonly IDisplayService _displayService;
    private readonly ITriviaDataService _triviaDataService;
    private readonly IGameStateService _gameStateService;
    private readonly IKnockoutWebServer? _webServer;
    private int _previousWebServerPort;
    private bool _disposed;

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
    public ObservableCollection<string> AvailableCategories { get; } = ["All Categories"];

    public event EventHandler<(int Index, string? Device)>? OpenAudienceDisplayRequested;
    public event EventHandler? CloseAudienceDisplayRequested;
    public event EventHandler<(int Index, string? Device)>? MoveHostWindowRequested;
    public event EventHandler? SettingsUpdated;

    public SettingsViewModel(
        IConfigService configService,
        IDisplayService displayService,
        ITriviaDataService triviaDataService,
        IGameStateService gameStateService,
        IKnockoutWebServer? webServer = null)
    {
        _configService = configService;
        _displayService = displayService;
        _triviaDataService = triviaDataService;
        _gameStateService = gameStateService;
        _webServer = webServer;

        _settings = _gameStateService.Settings ?? _configService.LoadSettings();
        _previousWebServerPort = _settings.WebServerPort;

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
        SyncCategoriesFromGameState();
        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged += OnGameStatePropertyChanged;
        }
        MonitorEnumerator.MonitorsChanged += OnMonitorsChanged;
        _ = LoadSourcesAsync();
    }

    private void OnGameStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGameStateService.AvailableCategories))
        {
            SyncCategoriesFromGameState();
        }
    }

    private void SyncCategoriesFromGameState()
    {
        string current = Settings.SelectedCategory;
        AvailableCategories.Clear();
        foreach (var cat in _gameStateService.AvailableCategories)
        {
            AvailableCategories.Add(cat);
        }

        if (AvailableCategories.Count == 0)
        {
            AvailableCategories.Add("All Categories");
        }

        if (string.IsNullOrWhiteSpace(current) || !AvailableCategories.Contains(current))
        {
            Settings.SelectedCategory = AvailableCategories[0];
        }
        else
        {
            Settings.SelectedCategory = current;
        }
        OnPropertyChanged(nameof(Settings));
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            _ = Application.Current.Dispatcher.InvokeAsync(RefreshMonitors);
        }
        else
        {
            RefreshMonitors();
        }
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
    public async Task ImportPackAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Trivia Pack or Database",
            Filter = "Trivia Packs & Databases (*.json;*.db)|*.json;*.db|JSON Question Packs (*.json)|*.json|SQLite Databases (*.db)|*.db",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            int importedCount = 0;
            foreach (string file in dialog.FileNames)
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    string ext = Path.GetExtension(file);
                    string targetDir = string.Equals(ext, ".db", StringComparison.OrdinalIgnoreCase)
                        ? Globals.DataDir
                        : Globals.PacksDir;

                    Directory.CreateDirectory(targetDir);
                    string destPath = Path.Combine(targetDir, Path.GetFileName(file));
                    if (!string.Equals(Path.GetFullPath(file), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(file, destPath, overwrite: true);
                    }
                    importedCount++;
                }
                catch (Exception ex)
                {
                    Globals.LogError("KnockoutTrivia", "SettingsViewModel.ImportPackAsync", ex);
                }
            }

            if (importedCount > 0)
            {
                await LoadSourcesAsync();
                StatusMessage = $"Imported {importedCount} trivia file(s) into library.";
            }
        }
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
        _gameStateService.UpdateSettings(Settings);

        if (_webServer != null && Settings.WebServerPort > 0 && Settings.WebServerPort != _previousWebServerPort)
        {
            _webServer.Start(Settings.WebServerPort);
            _previousWebServerPort = Settings.WebServerPort;
        }

        SettingsUpdated?.Invoke(this, EventArgs.Empty);

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
        _gameStateService.UpdateSettings(Settings);
        SelectAllSources();
        RefreshMonitors();
        await SaveSettingsAsync();
        StatusMessage = "Restored default settings, monitors, and all databases.";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        MonitorEnumerator.MonitorsChanged -= OnMonitorsChanged;
        if (_gameStateService is ObservableObject obs)
        {
            obs.PropertyChanged -= OnGameStatePropertyChanged;
        }
    }
}


