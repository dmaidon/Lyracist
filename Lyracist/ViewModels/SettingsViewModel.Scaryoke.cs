// Edited on Oct 30, 2026 @ 07:03:00 -> Expose AvailableScaryokePaletteColors and SetScaryokeColorCommand for 12-color Scaryoke palette
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // ─── Scaryoke Configuration ────────────────────────────────────────

    public ObservableCollection<string> ScaryokeCategories { get; } = [];

    public IReadOnlyList<string> AvailableScaryokePaletteColors => ScaryokeViewModel.Palette;

    [ObservableProperty]
    private string? _selectedScaryokeCategory;

    [ObservableProperty]
    private string _newScaryokeCategoryName = string.Empty;

    // Weight/colour of the selected category, shown next to the list
    [ObservableProperty]
    private double _selectedScaryokeWeight = 1;

    [ObservableProperty]
    private string _selectedScaryokeColor = "Transparent";

    [ObservableProperty]
    private bool _isSelectedCategoryCustomColor;

    [ObservableProperty]
    private string _selectedCategoryColorModeText = "Auto (by app)";

    [ObservableProperty]
    private bool _scaryokeShowSingersChoice = AppSettings.ScaryokeShowSingersChoice;

    partial void OnScaryokeShowSingersChoiceChanged(bool value) => AppSettings.ScaryokeShowSingersChoice = value;

    [ObservableProperty]
    private bool _scaryokeShowDjsChoice = AppSettings.ScaryokeShowDjsChoice;

    partial void OnScaryokeShowDjsChoiceChanged(bool value) => AppSettings.ScaryokeShowDjsChoice = value;

    [ObservableProperty]
    private bool _scaryokeAvoidRepeats = AppSettings.ScaryokeAvoidRepeats;

    partial void OnScaryokeAvoidRepeatsChanged(bool value) => AppSettings.ScaryokeAvoidRepeats = value;

    public ObservableCollection<WelcomeScreenChoice> ScaryokeOverlayChoices { get; } = [];

    public string SelectedScaryokeOverlayMonitor
    {
        get => AppSettings.ScaryokeOverlayMonitor;
        set
        {
            string monitor = value ?? string.Empty;
            if (AppSettings.ScaryokeOverlayMonitor != monitor)
            {
                AppSettings.ScaryokeOverlayMonitor = monitor;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<string> ScaryokePresetNames { get; } = [];

    [ObservableProperty]
    private string? _selectedScaryokePreset;

    [ObservableProperty]
    private string _newScaryokePresetName = string.Empty;

    partial void OnSelectedScaryokeCategoryChanged(string? value) => UpdateSelectedScaryokeStyle();

    private void UpdateSelectedScaryokeStyle()
    {
        if (SelectedScaryokeCategory == null)
        {
            SelectedScaryokeWeight = 1;
            SelectedScaryokeColor = "Transparent";
            IsSelectedCategoryCustomColor = false;
            SelectedCategoryColorModeText = "None";
            return;
        }

        var style = AppSettings.GetScaryokeStyle(SelectedScaryokeCategory);
        SelectedScaryokeWeight = style.Weight;
        bool hasCustom = !string.IsNullOrWhiteSpace(style.Color);
        IsSelectedCategoryCustomColor = hasCustom;
        SelectedCategoryColorModeText = hasCustom ? "Custom Color" : "Auto (by app)";
        if (hasCustom)
        {
            SelectedScaryokeColor = style.Color!;
        }
        else
        {
            var usedColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string c in ScaryokeCategories)
            {
                var st = AppSettings.GetScaryokeStyle(c);
                if (!string.IsNullOrWhiteSpace(st.Color))
                {
                    usedColors.Add(st.Color);
                }
            }

            string resolvedColor = ScaryokeViewModel.PaletteColor(0);
            int targetIdx = ScaryokeCategories.IndexOf(SelectedScaryokeCategory);
            for (int i = 0; i <= targetIdx && i < ScaryokeCategories.Count; i++)
            {
                var st = AppSettings.GetScaryokeStyle(ScaryokeCategories[i]);
                if (!string.IsNullOrWhiteSpace(st.Color))
                {
                    resolvedColor = st.Color;
                }
                else
                {
                    resolvedColor = ScaryokeViewModel.PickDistinctPaletteColor(i, usedColors);
                    usedColors.Add(resolvedColor);
                }
            }
            SelectedScaryokeColor = resolvedColor;
        }
    }

    [RelayCommand]
    private void CycleScaryokeWeight()
    {
        if (SelectedScaryokeCategory == null) return;

        var style = AppSettings.GetScaryokeStyle(SelectedScaryokeCategory);
        double weight = style.Weight >= 3 ? 1 : style.Weight + 1;
        AppSettings.SetScaryokeStyle(SelectedScaryokeCategory, weight, style.Color);
        UpdateSelectedScaryokeStyle();
    }

    [RelayCommand]
    private void CycleScaryokeColor()
    {
        if (SelectedScaryokeCategory == null) return;

        var style = AppSettings.GetScaryokeStyle(SelectedScaryokeCategory);
        int index = Array.FindIndex(ScaryokeViewModel.Palette, c => string.Equals(c, SelectedScaryokeColor, StringComparison.OrdinalIgnoreCase));
        string next = ScaryokeViewModel.Palette[(index + 1) % ScaryokeViewModel.Palette.Length];
        AppSettings.SetScaryokeStyle(SelectedScaryokeCategory, style.Weight, next);
        UpdateSelectedScaryokeStyle();
    }

    [RelayCommand]
    private void SetScaryokeColor(string? color)
    {
        if (SelectedScaryokeCategory == null || string.IsNullOrWhiteSpace(color)) return;

        var style = AppSettings.GetScaryokeStyle(SelectedScaryokeCategory);
        AppSettings.SetScaryokeStyle(SelectedScaryokeCategory, style.Weight, color.Trim());
        UpdateSelectedScaryokeStyle();
    }

    [RelayCommand]
    private void ResetScaryokeColor()
    {
        if (SelectedScaryokeCategory == null) return;

        var style = AppSettings.GetScaryokeStyle(SelectedScaryokeCategory);
        AppSettings.SetScaryokeStyle(SelectedScaryokeCategory, style.Weight, null);
        UpdateSelectedScaryokeStyle();
    }

    private void RefreshScaryokePresets()
    {
        ScaryokePresetNames.Clear();
        foreach (string name in AppSettings.ScaryokePresetNames)
        {
            ScaryokePresetNames.Add(name);
        }
    }

    [RelayCommand]
    private void SaveScaryokePreset()
    {
        string name = NewScaryokePresetName?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        AppSettings.SaveScaryokePreset(name);
        NewScaryokePresetName = string.Empty;
        RefreshScaryokePresets();
        SelectedScaryokePreset = name;
    }

    [RelayCommand]
    private void LoadScaryokePreset()
    {
        if (SelectedScaryokePreset == null) return;

        if (AppSettings.LoadScaryokePreset(SelectedScaryokePreset))
        {
            SelectedScaryokeCategory = null;
            RefreshScaryokeCategories();
        }
    }

    [RelayCommand]
    private void DeleteScaryokePreset()
    {
        if (SelectedScaryokePreset == null) return;

        AppSettings.DeleteScaryokePreset(SelectedScaryokePreset);
        SelectedScaryokePreset = null;
        RefreshScaryokePresets();
    }

    private void RefreshScaryokeCategories()
    {
        ScaryokeCategories.Clear();
        foreach (var cat in AppSettings.ScaryokeCategories)
        {
            ScaryokeCategories.Add(cat);
        }
    }

    [RelayCommand]
    private void AddScaryokeCategory()
    {
        string cat = NewScaryokeCategoryName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(cat)) return;

        if (AppSettings.ScaryokeCategories.Count >= 11)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 11 categories is allowed for the Scaryoke wheel.",
                "Max Categories Reached",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.AddScaryokeCategory(cat);
        NewScaryokeCategoryName = string.Empty;
        RefreshScaryokeCategories();
    }

    [RelayCommand]
    private void RemoveScaryokeCategory()
    {
        if (SelectedScaryokeCategory == null) return;

        if (AppSettings.ScaryokeCategories.Count <= 2)
        {
            System.Windows.MessageBox.Show(
                "The Scaryoke wheel must have at least 2 categories to be playable.",
                "Minimum Categories Warning",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        AppSettings.RemoveScaryokeCategory(SelectedScaryokeCategory);
        SelectedScaryokeCategory = null;
        RefreshScaryokeCategories();
    }

    public void RefreshScaryokeOverlayChoices()
    {
        ScaryokeOverlayChoices.Clear();
        ScaryokeOverlayChoices.Add(new WelcomeScreenChoice(string.Empty, "Rotation Billboard Screen (Default)"));
        ScaryokeOverlayChoices.Add(new WelcomeScreenChoice("LYRICS", "Lyrics Projection Screen"));
        ScaryokeOverlayChoices.Add(new WelcomeScreenChoice("DJBANNER", "DJ & Special Event Banner Screen"));

        try
        {
            foreach (var m in MonitorEnumerator.GetMonitors())
            {
                ScaryokeOverlayChoices.Add(new WelcomeScreenChoice(
                    m.DeviceName,
                    $"Monitor {m.Index + 1} ({m.Width}x{m.Height}){(m.IsPrimary ? " [Primary]" : "")}"));
            }
        }
        catch { }
    }

    [RelayCommand]
    private void OpenScaryokeWheel()
    {
        var wheel = App.AppHost.Services.GetRequiredService<Windows.ScaryokeWindow>();
        wheel.RebuildWheel();
        _display.ShowRotationWindow();
        wheel.Show();
        wheel.Activate();
    }
}
