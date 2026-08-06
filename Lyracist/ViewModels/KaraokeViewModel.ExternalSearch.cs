// Created on Aug 6, 2026 @ 07:01:27 -> Split external link (Spotify/YouTube/Amazon) search out of KaraokeViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Models;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    [ObservableProperty]
    private string _customExternalUrl = string.Empty;

    [ObservableProperty]
    private string _customExternalTitle = string.Empty;

    [ObservableProperty]
    private string _customExternalArtist = string.Empty;

    [ObservableProperty]
    private string _selectedExternalService = "All";

    [ObservableProperty]
    private ExternalTrack? _selectedExternalTrack;

    [ObservableProperty]
    private bool _isExternalLoading;

    public ObservableCollection<ExternalTrack> ExternalResults { get; } = [];

    [ObservableProperty]
    private bool _showLocalFilter = true;

    [ObservableProperty]
    private bool _showPartyTymeFilter = false;

    [ObservableProperty]
    private bool _showSpotifyFilter = false;

    [ObservableProperty]
    private bool _showYouTubeFilter = true;

    [ObservableProperty]
    private bool _showAmazonFilter = false;

    [ObservableProperty]
    private bool _isSpotifyAvailable = false;

    [ObservableProperty]
    private bool _isYouTubeAvailable = true;

    [ObservableProperty]
    private bool _isAmazonAvailable = false;

    [ObservableProperty]
    private bool _isExternalPerformanceActive;

    [ObservableProperty]
    private string _externalPerformanceSource = string.Empty;

    [ObservableProperty]
    private string _externalPerformanceUrl = string.Empty;

    partial void OnSelectedExternalTrackChanged(ExternalTrack? value)
    {
        if (value != null)
        {
            SelectedSong = null;
            SelectedPartyTymeTrack = null;
            SelectedHistoryEntry = null;
        }
    }

    [RelayCommand]
    private async Task SearchExternal()
    {
        IsExternalLoading = true;
        try
        {
            var results = await _externalLinkService.SearchAsync(SearchQuery, SelectedExternalService);
            ExternalResults.Clear();
            foreach (var track in results)
            {
                ExternalResults.Add(track);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"External search error: {ex.Message}");
        }
        finally
        {
            IsExternalLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayExternalTrack(ExternalTrack track)
    {
        if (track == null) return;

        IsPlaying = false;
        CurrentSongName = $"{track.Artist} - {track.Title} [{track.Source}]";
        await _mediaEngine.Stop();

        // Stop background music as performance is launching externally
        _showFlow.OnKaraokeTrackStarted();

        IsExternalPerformanceActive = true;
        ExternalPerformanceSource = track.Source;
        ExternalPerformanceUrl = track.Url;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(track.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open link: {ex.Message}", "Browser Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ReopenExternalPerformanceLink()
    {
        if (string.IsNullOrWhiteSpace(ExternalPerformanceUrl)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ExternalPerformanceUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open link: {ex.Message}", "Browser Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void OpenExternalLinkInBrowser(Singer singer)
    {
        if (singer == null || string.IsNullOrWhiteSpace(singer.ExternalLink)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(singer.ExternalLink) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open link: {ex.Message}", "Browser Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
