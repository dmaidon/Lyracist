// Created on Aug 6, 2026 @ 07:01:27 -> Split performer rating icon settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // ─── Performer Rating Configuration ────────────────────────────────
    [ObservableProperty]
    private bool _isRatingSystemEnabled = AppSettings.IsRatingSystemEnabled;

    [ObservableProperty]
    private string _selectedRatingIcon = AppSettings.SelectedRatingIcon;

    [ObservableProperty]
    private string _newRatingIconName = string.Empty;

    public ObservableCollection<string> AvailableRatingIcons { get; } = [];

    partial void OnIsRatingSystemEnabledChanged(bool value)
    {
        AppSettings.IsRatingSystemEnabled = value;
        _rotationWindowVm.NotifyPropertyChanged(nameof(RotationWindowViewModel.IsRatingSystemEnabled));
    }

    partial void OnSelectedRatingIconChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            AppSettings.SelectedRatingIcon = value;
            _rotationWindowVm.NotifyPropertyChanged(nameof(RotationWindowViewModel.RatingIconSymbol));
        }
    }

    private void RefreshAvailableRatingIcons()
    {
        AvailableRatingIcons.Clear();
        foreach (var icon in AppSettings.AvailableRatingIcons)
        {
            AvailableRatingIcons.Add(icon);
        }
        SelectedRatingIcon = AppSettings.SelectedRatingIcon;
    }

    [RelayCommand]
    private void AddRatingIcon()
    {
        string icon = NewRatingIconName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(icon)) return;

        // Validation for "NOTHING negative or detrimental."
        // We block any words/emojis that could express negativity.
        var negativeKeywords = new[]
        {
            "poop", "poo", "shit", "down", "thumbs down", "thumb down", "garbage", "trash",
            "bad", "boo", "dislike", "hate", "ugly", "fail", "loser", "suck", "terrible", "awful",
            "👎", "💩", "💔", "💀", "😠", "😡", "🗑️", "❌", "👎", "🤮", "👿"
        };

        bool isNegative = false;
        foreach (var keyword in negativeKeywords)
        {
            if (icon.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                isNegative = true;
                break;
            }
        }

        if (isNegative)
        {
            System.Windows.MessageBox.Show(
                "Detrimental or negative feedback icons are not allowed. Please enter a positive icon!",
                "Validation Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.AddAvailableRatingIcon(icon);
        NewRatingIconName = string.Empty;
        RefreshAvailableRatingIcons();
        SelectedRatingIcon = icon;
    }

    [RelayCommand]
    private void RemoveRatingIcon()
    {
        if (string.IsNullOrEmpty(SelectedRatingIcon)) return;

        if (AppSettings.AvailableRatingIcons.Count <= 1)
        {
            System.Windows.MessageBox.Show(
                "You must have at least one rating icon available.",
                "Cannot Remove Icon",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.RemoveAvailableRatingIcon(SelectedRatingIcon);
        RefreshAvailableRatingIcons();
    }

    [RelayCommand]
    private void AddPresetIcon(string iconPreset)
    {
        if (string.IsNullOrWhiteSpace(iconPreset)) return;
        AppSettings.AddAvailableRatingIcon(iconPreset);
        RefreshAvailableRatingIcons();
        SelectedRatingIcon = iconPreset;
    }
}
