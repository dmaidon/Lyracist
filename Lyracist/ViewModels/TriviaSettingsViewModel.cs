// Edited on Aug 21, 2026 @ 08:18:00 -> Add live settings synchronization and persistence for all Trivia settings
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    private string _instructionBannerText = TriviaSettings.DefaultInstructionBannerText;

    partial void OnInstructionBannerTextChanged(string value)
    {
        Settings.InstructionBannerText = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _defaultQuestionSeconds = 15;

    partial void OnDefaultQuestionSecondsChanged(int value)
    {
        Settings.DefaultQuestionSeconds = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _warningCountdownSeconds = 3;

    partial void OnWarningCountdownSecondsChanged(int value)
    {
        Settings.WarningCountdownSeconds = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _answerEliminationIntervalSeconds = 5;

    partial void OnAnswerEliminationIntervalSecondsChanged(int value)
    {
        Settings.AnswerEliminationIntervalSeconds = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _postRevealDelaySeconds = 5;

    partial void OnPostRevealDelaySecondsChanged(int value)
    {
        Settings.PostRevealDelaySeconds = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _baseQuestionPoints = 1000;

    partial void OnBaseQuestionPointsChanged(int value)
    {
        Settings.BasePointsPerQuestion = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _wrongAnswerDeductionPoints = 0;

    partial void OnWrongAnswerDeductionPointsChanged(int value)
    {
        Settings.WrongAnswerDeductionPoints = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private bool _tieredScoringEnabled = true;

    partial void OnTieredScoringEnabledChanged(bool value)
    {
        Settings.TieredScoringEnabled = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _points4OptionsPercent = 100;

    partial void OnPoints4OptionsPercentChanged(int value)
    {
        Settings.Points4OptionsPercent = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _points3OptionsPercent = 70;

    partial void OnPoints3OptionsPercentChanged(int value)
    {
        Settings.Points3OptionsPercent = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _points2OptionsPercent = 40;

    partial void OnPoints2OptionsPercentChanged(int value)
    {
        Settings.Points2OptionsPercent = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private bool _speedBonusEnabled = true;

    partial void OnSpeedBonusEnabledChanged(bool value)
    {
        Settings.SpeedBonusEnabled = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _maxSpeedBonusPoints = 500;

    partial void OnMaxSpeedBonusPointsChanged(int value)
    {
        Settings.MaxSpeedBonus = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private bool _streakBonusEnabled = true;

    partial void OnStreakBonusEnabledChanged(bool value)
    {
        Settings.StreakBonusMultiplier = value ? StreakMultiplier : 0;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private double _streakMultiplier = 0.1;

    partial void OnStreakMultiplierChanged(double value)
    {
        if (StreakBonusEnabled)
        {
            Settings.StreakBonusMultiplier = value;
            TriviaStorageHelper.SaveSettings(Settings);
            _triviaViewModel.ApplySettings(Settings);
        }
    }

    [ObservableProperty]
    private bool _autoAdvanceQuestions = true;

    partial void OnAutoAdvanceQuestionsChanged(bool value)
    {
        Settings.AutoAdvanceQuestions = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private bool _autoStartNextGameEnabled = true;

    partial void OnAutoStartNextGameEnabledChanged(bool value)
    {
        Settings.AutoStartNextGameEnabled = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _nextGameDelayMinutes = 3;

    partial void OnNextGameDelayMinutesChanged(int value)
    {
        Settings.NextGameDelayMinutes = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _preGameCountdownMinutes = 5;

    partial void OnPreGameCountdownMinutesChanged(int value)
    {
        Settings.PreGameCountdownMinutes = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private bool _autoStartAfterCountdown = true;

    partial void OnAutoStartAfterCountdownChanged(bool value)
    {
        Settings.AutoStartAfterCountdown = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private bool _soundEffectsEnabled = true;

    partial void OnSoundEffectsEnabledChanged(bool value)
    {
        Settings.SoundEffectsEnabled = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private int _serverPort = 8085;

    partial void OnServerPortChanged(int value)
    {
        Settings.Port = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private string _wifiSsid = "";

    partial void OnWifiSsidChanged(string value)
    {
        Settings.WifiSsid = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
    }

    [ObservableProperty]
    private string _wifiPassword = "";

    partial void OnWifiPasswordChanged(string value)
    {
        Settings.WifiPassword = value;
        TriviaStorageHelper.SaveSettings(Settings);
        _triviaViewModel.ApplySettings(Settings);
        if (!string.IsNullOrWhiteSpace(WifiSsid) && !string.IsNullOrWhiteSpace(value))
        {
            WifiPasswordStore.SetPasswordForSsid(WifiSsid, value);
        }
    }

    [ObservableProperty]
    private MonitorInfo? _selectedMonitor;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isStatusVisible;

    public ObservableCollection<MonitorInfo> AvailableMonitors { get; } = [];

    public TriviaSettingsViewModel(TriviaViewModel triviaViewModel)
    {
        _triviaViewModel = triviaViewModel;
        _settings = TriviaStorageHelper.LoadSettings();
        LoadFromSettings();
        RefreshMonitors();
    }

    public void RefreshMonitors()
    {
        AvailableMonitors.Clear();
        var monitors = MonitorEnumerator.GetMonitors();
        foreach (var m in monitors)
        {
            AvailableMonitors.Add(m);
        }

        if (AvailableMonitors.Count > 0)
        {
            var match = AvailableMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, Settings.SelectedMonitorDevice, StringComparison.OrdinalIgnoreCase));
            SelectedMonitor = match ?? (AvailableMonitors.Count > 1 ? AvailableMonitors[1] : AvailableMonitors[0]);
        }
    }

    private void LoadFromSettings()
    {
        OnPropertyChanged(nameof(VenueName));
        OnPropertyChanged(nameof(GameMasterName));
        InstructionBannerText = string.IsNullOrWhiteSpace(Settings.InstructionBannerText) ? TriviaSettings.DefaultInstructionBannerText : Settings.InstructionBannerText;
        DefaultQuestionSeconds = Settings.DefaultQuestionSeconds;
        WarningCountdownSeconds = Settings.WarningCountdownSeconds;
        AnswerEliminationIntervalSeconds = Settings.AnswerEliminationIntervalSeconds;
        PostRevealDelaySeconds = Settings.PostRevealDelaySeconds > 0 ? Settings.PostRevealDelaySeconds : 5;
        BaseQuestionPoints = Settings.BasePointsPerQuestion;
        WrongAnswerDeductionPoints = Settings.WrongAnswerDeductionPoints;
        TieredScoringEnabled = Settings.TieredScoringEnabled;
        Points4OptionsPercent = Settings.Points4OptionsPercent;
        Points3OptionsPercent = Settings.Points3OptionsPercent;
        Points2OptionsPercent = Settings.Points2OptionsPercent;
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
        Settings.InstructionBannerText = InstructionBannerText;
        Settings.DefaultQuestionSeconds = DefaultQuestionSeconds;
        Settings.WarningCountdownSeconds = WarningCountdownSeconds;
        Settings.AnswerEliminationIntervalSeconds = AnswerEliminationIntervalSeconds;
        Settings.PostRevealDelaySeconds = PostRevealDelaySeconds;
        Settings.BasePointsPerQuestion = BaseQuestionPoints;
        Settings.WrongAnswerDeductionPoints = WrongAnswerDeductionPoints;
        Settings.TieredScoringEnabled = TieredScoringEnabled;
        Settings.Points4OptionsPercent = Points4OptionsPercent;
        Settings.Points3OptionsPercent = Points3OptionsPercent;
        Settings.Points2OptionsPercent = Points2OptionsPercent;
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
        if (SelectedMonitor != null)
        {
            Settings.SelectedMonitorDevice = SelectedMonitor.DeviceName;
        }
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
