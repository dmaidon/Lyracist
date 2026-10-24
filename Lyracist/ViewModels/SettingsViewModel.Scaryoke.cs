// Created on Aug 6, 2026 @ 07:01:27 -> Split Scaryoke wheel category settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // ─── Scaryoke Configuration ────────────────────────────────────────

    public ObservableCollection<string> ScaryokeCategories { get; } = [];

    [ObservableProperty]
    private string? _selectedScaryokeCategory;

    [ObservableProperty]
    private string _newScaryokeCategoryName = string.Empty;

    // Weight/colour of the selected category, shown next to the list
    [ObservableProperty]
    private double _selectedScaryokeWeight = 1;

    [ObservableProperty]
    private string _selectedScaryokeColor = "Transparent";

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
            return;
        }

        var style = AppSettings.GetScaryokeStyle(SelectedScaryokeCategory);
        SelectedScaryokeWeight = style.Weight;
        SelectedScaryokeColor = style.Color ?? ScaryokeViewModel.PaletteColor(ScaryokeCategories.IndexOf(SelectedScaryokeCategory));
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

        if (AppSettings.ScaryokeCategories.Count >= 8)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 8 categories is allowed for the Scaryoke wheel.",
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
}
