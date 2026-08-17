// Edited on Aug 17, 2026 @ 16:08:00 -> Pull Venue and Host from existing AppSettings and remove duplicate settings
using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Shared;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;

namespace Lyracist.ViewModels;

public partial class TriviaSettingsViewModel : BaseViewModel
{
    private readonly TriviaViewModel _triviaViewModel;
    private DispatcherTimer? _statusTimer;

    [ObservableProperty]
    private TriviaSettings _settings;

    public string VenueName => !string.IsNullOrWhiteSpace(AppSettings.SelectedVenue) ? AppSettings.SelectedVenue : "Main Venue";

    public string GameMasterName => !string.IsNullOrWhiteSpace(AppSettings.DjName) ? AppSettings.DjName : "DJ / Host";

    [ObservableProperty]
    private int _defaultQuestionSeconds = 15;

    [ObservableProperty]
    private int _warningCountdownSeconds = 3;

    [ObservableProperty]
    private int _answerEliminationIntervalSeconds = 5;

    [ObservableProperty]
    private int _baseQuestionPoints = 1000;

    [ObservableProperty]
    private bool _speedBonusEnabled = true;

    [ObservableProperty]
    private int _maxSpeedBonusPoints = 500;

    [ObservableProperty]
    private bool _streakBonusEnabled = true;

    [ObservableProperty]
    private double _streakMultiplier = 0.1;

    [ObservableProperty]
    private bool _autoAdvanceQuestions = true;

    [ObservableProperty]
    private bool _autoStartNextGameEnabled = true;

    [ObservableProperty]
    private int _nextGameDelayMinutes = 3;

    [ObservableProperty]
    private int _preGameCountdownMinutes = 5;

    [ObservableProperty]
    private bool _autoStartAfterCountdown = true;

    [ObservableProperty]
    private bool _soundEffectsEnabled = true;

    [ObservableProperty]
    private int _serverPort = 8085;

    [ObservableProperty]
    private string _wifiSsid = "";

    [ObservableProperty]
    private string _wifiPassword = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isStatusVisible;

    public TriviaSettingsViewModel(TriviaViewModel triviaViewModel)
    {
        _triviaViewModel = triviaViewModel;
        _settings = TriviaStorageHelper.LoadSettings();
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        OnPropertyChanged(nameof(VenueName));
        OnPropertyChanged(nameof(GameMasterName));
        DefaultQuestionSeconds = Settings.DefaultQuestionSeconds;
        WarningCountdownSeconds = Settings.WarningCountdownSeconds;
        AnswerEliminationIntervalSeconds = Settings.AnswerEliminationIntervalSeconds;
        BaseQuestionPoints = Settings.BasePointsPerQuestion;
        SpeedBonusEnabled = Settings.SpeedBonusEnabled;
        MaxSpeedBonusPoints = Settings.MaxSpeedBonus;
        StreakBonusEnabled = Settings.StreakBonusMultiplier > 0;
        StreakMultiplier = Settings.StreakBonusMultiplier;
        AutoAdvanceQuestions = Settings.AutoAdvanceQuestions;
        AutoStartNextGameEnabled = Settings.AutoStartNextGameEnabled;
        NextGameDelayMinutes = Settings.NextGameDelayMinutes;
        PreGameCountdownMinutes = Settings.PreGameCountdownMinutes;
        AutoStartAfterCountdown = Settings.AutoStartAfterCountdown;
        SoundEffectsEnabled = Settings.SoundEffectsEnabled;
        ServerPort = Settings.Port;
        WifiSsid = Settings.WifiSsid;
        WifiPassword = Settings.WifiPassword;
    }

    private void PushToSettings()
    {
        Settings.VenueName = VenueName;
        Settings.HostName = GameMasterName;
        Settings.DefaultQuestionSeconds = DefaultQuestionSeconds;
        Settings.WarningCountdownSeconds = WarningCountdownSeconds;
        Settings.AnswerEliminationIntervalSeconds = AnswerEliminationIntervalSeconds;
        Settings.BasePointsPerQuestion = BaseQuestionPoints;
        Settings.SpeedBonusEnabled = SpeedBonusEnabled;
        Settings.MaxSpeedBonus = MaxSpeedBonusPoints;
        Settings.StreakBonusMultiplier = StreakBonusEnabled ? StreakMultiplier : 0;
        Settings.AutoAdvanceQuestions = AutoAdvanceQuestions;
        Settings.AutoStartNextGameEnabled = AutoStartNextGameEnabled;
        Settings.NextGameDelayMinutes = NextGameDelayMinutes;
        Settings.PreGameCountdownMinutes = PreGameCountdownMinutes;
        Settings.AutoStartAfterCountdown = AutoStartAfterCountdown;
        Settings.SoundEffectsEnabled = SoundEffectsEnabled;
        Settings.Port = ServerPort;
        Settings.WifiSsid = WifiSsid;
        Settings.WifiPassword = WifiPassword;
    }

    [RelayCommand]
    private void AutoDetectWifi()
    {
        try
        {
            string? detectedSsid = WifiHelper.GetConnectedSsid();
            if (!string.IsNullOrWhiteSpace(detectedSsid))
            {
                WifiSsid = detectedSsid;
                string storedPassword = WifiPasswordStore.GetPasswordForSsid(detectedSsid);
                if (!string.IsNullOrWhiteSpace(storedPassword))
                {
                    WifiPassword = storedPassword;
                }
                ShowStatus($"Auto-detected Wi-Fi Network: \"{detectedSsid}\"");
            }
            else
            {
                ShowStatus("No active Wi-Fi SSID detected.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to detect Wi-Fi: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        PushToSettings();
        TriviaStorageHelper.SaveSettings(Settings);

        if (!string.IsNullOrWhiteSpace(WifiSsid) && !string.IsNullOrWhiteSpace(WifiPassword))
        {
            WifiPasswordStore.SetPasswordForSsid(WifiSsid, WifiPassword);
        }

        _triviaViewModel.ApplySettings(Settings);
        ShowStatus("Trivia settings saved successfully.");
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        Settings = new TriviaSettings();
        LoadFromSettings();
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
        ShowStatus("Trivia settings reset to defaults.");
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
        IsStatusVisible = true;

        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (s, e) =>
        {
            IsStatusVisible = false;
            _statusTimer.Stop();
        };
        _statusTimer.Start();
    }
}
