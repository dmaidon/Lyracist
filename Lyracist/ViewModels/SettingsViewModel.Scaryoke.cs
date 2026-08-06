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
