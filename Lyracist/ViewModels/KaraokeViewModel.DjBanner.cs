// Edited on Oct 5, 2026 @ 22:46:50 -> Fix CS8611 nullable parameter warning in OnActiveSpecialEventChanged
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Shared;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    public Action<string>? OnLocalSpecialEventChanged { get; set; }
    public ObservableCollection<Lyracist.Shared.DjBannerItem> DjBanners { get; } = [];

    [ObservableProperty]
    private Lyracist.Shared.DjBannerItem? _selectedDjBanner;

    private void RefreshDjBanners()
    {
        DjBanners.Clear();
        foreach (var item in Lyracist.Shared.DjBannerFileManager.ScanBanners(Lyracist.Shared.Globals.DjBannersDir))
        {
            DjBanners.Add(item);
        }

        var prefs = _displayService.GetPreferences();
        if (!string.IsNullOrEmpty(prefs.SelectedDjBannerPath))
        {
            SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
        }
        else
        {
            SelectedDjBanner = DjBanners.FirstOrDefault();
        }
    }

    partial void OnSelectedDjBannerChanged(Lyracist.Shared.DjBannerItem? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.FullPath))
        {
            if (ActiveSpecialEvent != "None")
            {
                ActiveSpecialEvent = "None";
                RebuildSpecialEventOptions();
            }
        }
        _displayService.UpdateDjBanner(value?.FullPath ?? string.Empty);
    }

    [RelayCommand]
    private void UploadDjBanner()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Upload DJ Banner",
            Filter = "Supported Banners (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string destPath = Lyracist.Shared.DjBannerFileManager.CopyInWithDedup(dialog.FileName, Lyracist.Shared.Globals.DjBannersDir);
                RefreshDjBanners();

                SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == destPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to upload banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void DeleteDjBanner()
    {
        if (SelectedDjBanner == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete the DJ Banner '{SelectedDjBanner.FileName}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                string path = SelectedDjBanner.FullPath;
                SelectedDjBanner = null;

                Lyracist.Shared.DjBannerFileManager.DeleteBanner(path);
                RefreshDjBanners();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to delete banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void ShowDjBanner()
    {
        _displayService.ShowDjBannerWindow();
    }

    [ObservableProperty]
    private string _activeSpecialEvent = "None";

    public ObservableCollection<SpecialEventOptionViewModel> SpecialEventOptions { get; } = [];

    public void InitializeSpecialEvents()
    {
        var prefs = _displayService.GetPreferences();
        // Announcements are one-night: clear leftovers from the last run and don't resume on a deleted one.
        DjBannerFileManager.DeleteGeneratedAnnouncements(Globals.EventBannersDir);
        if (string.Equals(prefs.SelectedSpecialEvent, "Announcement", StringComparison.OrdinalIgnoreCase))
        {
            _displayService.UpdateSpecialEvent("None");
        }
        ActiveSpecialEvent = string.IsNullOrWhiteSpace(prefs.SelectedSpecialEvent) ? "None" : prefs.SelectedSpecialEvent;

        var currentEvents = Lyracist.Core.Helpers.AppSettings.SpecialEvents;
        bool addedAny = false;
        foreach (var stdName in DjBannerFileManager.StandardEventNames)
        {
            if (!currentEvents.Any(e => e.EventName.Equals(stdName, StringComparison.OrdinalIgnoreCase)))
            {
                currentEvents.Add(new Lyracist.Shared.SpecialEventConfig
                {
                    EventName = stdName,
                    BannerFileName = DjBannerFileManager.GetStandardBannerFileName(stdName)
                });
                addedAny = true;
            }
        }
        if (addedAny)
        {
            Lyracist.Core.Helpers.AppSettings.SpecialEvents = currentEvents;
        }

        RebuildSpecialEventOptions();
        RefreshConnectInstructionsBanner();
    }

    public void RefreshConnectInstructionsBanner()
    {
        try
        {
            DjBannerFileManager.CreateConnectInstructionsBannerPng(
                System.IO.Path.Combine(Globals.EventBannersDir, "ConnectInstructions.png"),
                WifiHelper.GetConnectedSsid() ?? string.Empty,
                Lyracist.Core.Helpers.AppSettings.WifiPassword,
                JoinUrl);
        }
        catch (Exception ex)
        {
            Lyracist.Core.Helpers.AppLogger.LogError(ex, "KaraokeViewModel.RefreshConnectInstructionsBanner");
        }
    }

    public void RebuildSpecialEventOptions()
    {
        if (string.IsNullOrWhiteSpace(ActiveSpecialEvent))
        {
            ActiveSpecialEvent = "None";
        }
        SpecialEventOptions.Clear();
        SpecialEventOptions.Add(new SpecialEventOptionViewModel("None", "None", ActiveSpecialEvent.Equals("None", StringComparison.OrdinalIgnoreCase), OnSpecialEventChanged));
        foreach (var ev in Lyracist.Core.Helpers.AppSettings.SpecialEvents)
        {
            SpecialEventOptions.Add(new SpecialEventOptionViewModel(ev.EventName, ev.EventName, ActiveSpecialEvent.Equals(ev.EventName, StringComparison.OrdinalIgnoreCase), OnSpecialEventChanged));
        }
    }

    public static Func<string, string?>? AnnouncementPromptHandler { get; set; }
    public static Func<string, string?>? BirthdayPromptHandler { get; set; }
    private string? _lastAnnouncementText;

    private void OnSpecialEventChanged(string value)
    {
        if (value.Equals("Birthday", StringComparison.OrdinalIgnoreCase))
        {
            string defaultName = Rotation?.SelectedSinger?.Name ?? Rotation?.Rotation.FirstOrDefault(s => s.IsCurrent && !s.IsMusic)?.Name ?? Rotation?.Rotation.FirstOrDefault()?.Name ?? "";
            string? performerName = ShowPersonalizedBirthdayPrompt(defaultName);
            if (performerName != null)
            {
                try
                {
                    string birthdayFilePath = System.IO.Path.Combine(Globals.EventBannersDir, "Birthday.png");
                    DjBannerFileManager.CreatePersonalizedBirthdayBannerPng(birthdayFilePath, performerName);
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "Failed creating birthday banner", ex);
                }
            }
        }
        else if (value.Equals("Announcement", StringComparison.OrdinalIgnoreCase))
        {
            string defaultText = !string.IsNullOrWhiteSpace(_lastAnnouncementText)
                ? _lastAnnouncementText
                : "Welcome to Jill & Robert, 1st timers tonight";
            string? announcementText = ShowDynamicAnnouncementPrompt(defaultText);
            if (announcementText != null)
            {
                _lastAnnouncementText = announcementText;
                try
                {
                    DjBannerFileManager.CreateAnnouncementBanner(Globals.EventBannersDir, announcementText);
                }
                catch (Exception ex)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", "Failed creating dynamic announcement banner", ex);
                }
            }
            else
            {
                // Cancelled by DJ - revert selection to previous ActiveSpecialEvent
                SyncSpecialEventOptionSelections(ActiveSpecialEvent);
                return;
            }
        }
        ActiveSpecialEvent = value;
        _displayService.UpdateSpecialEvent(value);
        OnLocalSpecialEventChanged?.Invoke(value);
    }

    public static string? ShowDynamicAnnouncementPrompt(string defaultText = "")
    {
        if (AnnouncementPromptHandler != null) return AnnouncementPromptHandler(defaultText);
        if (System.Windows.Application.Current == null) return defaultText;
        return TextPromptDialog.ShowAnnouncement(defaultText);
    }

    public static string? ShowPersonalizedBirthdayPrompt(string defaultName = "")
    {
        if (BirthdayPromptHandler != null) return BirthdayPromptHandler(defaultName);
        if (System.Windows.Application.Current == null) return defaultName;
        return TextPromptDialog.ShowBirthday(defaultName);
    }

    // Closing an announcement deletes its banner. Deferred so the banner window has moved off the file first.
    partial void OnActiveSpecialEventChanged(string? oldValue, string newValue)
    {
        if (oldValue != null &&
            oldValue.Equals("Announcement", StringComparison.OrdinalIgnoreCase) &&
            !newValue.Equals("Announcement", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => DjBannerFileManager.DeleteGeneratedAnnouncements(Globals.EventBannersDir));
        }
    }

    partial void OnActiveSpecialEventChanged(string value)
    {
        SyncSpecialEventOptionSelections(value);
    }

    public void SyncSpecialEventOptionSelections(string eventName)
    {
        bool foundMatch = false;
        foreach (var option in SpecialEventOptions)
        {
            bool shouldBeSelected = string.Equals(option.Value, eventName, StringComparison.OrdinalIgnoreCase);
            if (shouldBeSelected) foundMatch = true;
            if (option.IsSelected != shouldBeSelected)
            {
                option.SetSelectedQuietly(shouldBeSelected);
            }
        }

        // If an unrecognized custom event came in from KSRotation, dynamically add it so it is visible and selected
        if (!foundMatch && !string.IsNullOrWhiteSpace(eventName) && !eventName.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            var customOption = new SpecialEventOptionViewModel(eventName, eventName, true, OnSpecialEventChanged);
            SpecialEventOptions.Add(customOption);
            foundMatch = true;
        }

        // If still no match (e.g. invalid event or "None"), guarantee "None" is selected so all options are never deselected
        if (!foundMatch)
        {
            var noneOption = SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("None", StringComparison.OrdinalIgnoreCase));
            if (noneOption != null && !noneOption.IsSelected)
            {
                noneOption.SetSelectedQuietly(true);
            }
        }
    }

    public void UpdateActiveSpecialEventFromSync(string eventName)
    {
        if (string.Equals(ActiveSpecialEvent, eventName, StringComparison.OrdinalIgnoreCase)) return;

        ActiveSpecialEvent = eventName;
        _displayService.UpdateSpecialEvent(eventName);
    }
}

public class SpecialEventOptionViewModel : ObservableObject
{
    private readonly Action<string> _onSelected;
    public string DisplayName { get; }
    public string Value { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value)
            {
                _onSelected(Value);
            }
        }
    }

    public void SetSelectedQuietly(bool value)
    {
        SetProperty(ref _isSelected, value, nameof(IsSelected));
    }

    public SpecialEventOptionViewModel(string displayName, string value, bool isSelected, Action<string> onSelected)
    {
        DisplayName = displayName;
        Value = value;
        _isSelected = isSelected;
        _onSelected = onSelected;
    }
}
