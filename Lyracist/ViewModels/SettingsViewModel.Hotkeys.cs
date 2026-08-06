// Created on Aug 6, 2026 @ 07:01:27 -> Split hotkey settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // Hotkeys settings
    [ObservableProperty]
    private string _hkPlayPause = GetKeyForAction("PlayPause");

    [ObservableProperty]
    private string _hkStop = GetKeyForAction("Stop");

    [ObservableProperty]
    private string _hkDoneSinger = GetKeyForAction("DoneSinger");

    [ObservableProperty]
    private string _hkToggleBanner = GetKeyForAction("ToggleBanner");

    [ObservableProperty]
    private string _hkToggleLyricsWindow = GetKeyForAction("ToggleLyricsWindow");

    [ObservableProperty]
    private string _hkToggleRotationWindow = GetKeyForAction("ToggleRotationWindow");

    private static string GetKeyForAction(string actionName)
    {
        var dict = AppSettings.Hotkeys;
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Value, actionName, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return "None";
    }

    private void SaveHotkeys()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (HkPlayPause != "None" && !string.IsNullOrWhiteSpace(HkPlayPause)) dict[HkPlayPause] = "PlayPause";
        if (HkStop != "None" && !string.IsNullOrWhiteSpace(HkStop)) dict[HkStop] = "Stop";
        if (HkDoneSinger != "None" && !string.IsNullOrWhiteSpace(HkDoneSinger)) dict[HkDoneSinger] = "DoneSinger";
        if (HkToggleBanner != "None" && !string.IsNullOrWhiteSpace(HkToggleBanner)) dict[HkToggleBanner] = "ToggleBanner";
        if (HkToggleLyricsWindow != "None" && !string.IsNullOrWhiteSpace(HkToggleLyricsWindow)) dict[HkToggleLyricsWindow] = "ToggleLyricsWindow";
        if (HkToggleRotationWindow != "None" && !string.IsNullOrWhiteSpace(HkToggleRotationWindow)) dict[HkToggleRotationWindow] = "ToggleRotationWindow";
        AppSettings.Hotkeys = dict;
    }

    partial void OnHkPlayPauseChanged(string value) => SaveHotkeys();
    partial void OnHkStopChanged(string value) => SaveHotkeys();
    partial void OnHkDoneSingerChanged(string value) => SaveHotkeys();
    partial void OnHkToggleBannerChanged(string value) => SaveHotkeys();
    partial void OnHkToggleLyricsWindowChanged(string value) => SaveHotkeys();
    partial void OnHkToggleRotationWindowChanged(string value) => SaveHotkeys();
}
