// Created on Aug 6, 2026 @ 07:01:27 -> Split Star Wars Crawl banner + spaceship overlay settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // Crawl Banner Settings
    public List<string> CrawlBannerTypes { get; } = ["Dramatic", "Comedic", "Over-the-Top", "Custom"];

    [ObservableProperty]
    private string _selectedCrawlBannerType = AppSettings.CrawlBannerType;

    [ObservableProperty]
    private string _crawlBannerCustomText = AppSettings.CrawlBannerCustomText;

    // Spaceship overlay settings
    [ObservableProperty]
    private int _crawlSpaceshipFontSize = AppSettings.CrawlSpaceshipFontSize;

    [ObservableProperty]
    private int _crawlSpaceshipDuration = AppSettings.CrawlSpaceshipDuration;

    [ObservableProperty]
    private int _crawlSpaceshipFrequency = AppSettings.CrawlSpaceshipFrequency;

    public ObservableCollection<Lyracist.Models.SpaceshipSnippet> CrawlSpaceshipSnippets { get; } = new(AppSettings.CrawlSpaceshipSnippets);

    [ObservableProperty]
    private string _newSpaceshipSnippetText = string.Empty;

    [ObservableProperty]
    private Lyracist.Models.SpaceshipSnippet? _selectedSpaceshipSnippet;

    public bool IsStarWarsCrawlSelected => SelectedProjectionView == "Star Wars Crawl";
    public bool IsCustomCrawlBannerSelected => SelectedCrawlBannerType == "Custom";

    public string CrawlBannerPreviewText
    {
        get
        {
            return SelectedCrawlBannerType switch
            {
                "Dramatic" => "Dramatic: \"In a tavern far, far away, known only as {venue}, the patrons have risen in glorious rebellion — and under the wicked command of their sinister DJ, {dj}, they have chosen their ultimate weapon… karaoke.\"",
                "Comedic" => "Comedic: \"Somewhere in the distant reaches of the galaxy, inside a questionable establishment called {venue}, the patrons have staged a full‑blown uprising. Led by their diabolical DJ, {dj}, they now march toward their destiny: screaming karaoke like it’s a battle cry.\"",
                "Over-the-Top" => "Over-the-Top: \"In a tavern lost to time and space — a place whispered about only as {venue} — the patrons have revolted. Guided by the dark influence of DJ {dj}, they embark on a quest of unimaginable terror… karaoke night.\"",
                _ => "Custom: \"" + CrawlBannerCustomText + "\""
            };
        }
    }

    partial void OnSelectedCrawlBannerTypeChanged(string value)
    {
        AppSettings.CrawlBannerType = value;
        OnPropertyChanged(nameof(IsCustomCrawlBannerSelected));
        OnPropertyChanged(nameof(CrawlBannerPreviewText));
        UpdateCrawlBannerOnWindow();
    }

    partial void OnCrawlBannerCustomTextChanged(string value)
    {
        AppSettings.CrawlBannerCustomText = value;
        OnPropertyChanged(nameof(CrawlBannerPreviewText));
        UpdateCrawlBannerOnWindow();
    }

    partial void OnCrawlSpaceshipFontSizeChanged(int value) => AppSettings.CrawlSpaceshipFontSize = value;
    partial void OnCrawlSpaceshipDurationChanged(int value) => AppSettings.CrawlSpaceshipDuration = value;
    partial void OnCrawlSpaceshipFrequencyChanged(int value) => AppSettings.CrawlSpaceshipFrequency = value;

    [RelayCommand]
    private void AddSpaceshipSnippet()
    {
        string text = NewSpaceshipSnippetText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (CrawlSpaceshipSnippets.Count >= 10)
        {
            System.Windows.MessageBox.Show(
                "A maximum of 10 snippets is allowed.",
                "Max Snippets Reached",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var snippet = new Lyracist.Models.SpaceshipSnippet { Text = text, IsEnabled = true };
        CrawlSpaceshipSnippets.Add(snippet);
        NewSpaceshipSnippetText = string.Empty;
        SaveSpaceshipSnippets();
    }

    [RelayCommand]
    private void RemoveSpaceshipSnippet()
    {
        if (SelectedSpaceshipSnippet == null) return;
        CrawlSpaceshipSnippets.Remove(SelectedSpaceshipSnippet);
        SelectedSpaceshipSnippet = null;
        SaveSpaceshipSnippets();
    }

    [RelayCommand]
    private void SaveSpaceshipSnippets()
    {
        AppSettings.CrawlSpaceshipSnippets = [.. CrawlSpaceshipSnippets];
    }

    private void UpdateCrawlBannerOnWindow()
    {
        string template = AppSettings.GetActiveCrawlBannerTemplate();
        _display.SetCrawlBannerText(template);
    }
}
