// Created on Aug 6, 2026 @ 07:01:27 -> Split Party Tyme integration out of KaraokeViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Models;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    [ObservableProperty]
    private string _partyTymeClientId = string.Empty;

    [ObservableProperty]
    private string _partyTymeClientSecret = string.Empty;

    [ObservableProperty]
    private bool _isPartyTymeConnected;

    [ObservableProperty]
    private bool _isPartyTymeDisconnected = true;

    [ObservableProperty]
    private string _partyTymeConnectionStatus = string.Empty;

    [ObservableProperty]
    private bool _isPartyTymeLoading;

    [ObservableProperty]
    private PartyTymeTrack? _selectedPartyTymeTrack;

    public ObservableCollection<PartyTymeTrack> PartyTymeResults { get; } = [];

    partial void OnSelectedPartyTymeTrackChanged(PartyTymeTrack? value)
    {
        if (value != null)
        {
            SelectedSong = null;
            SelectedExternalTrack = null;
            SelectedHistoryEntry = null;
        }
    }

    partial void OnPartyTymeClientIdChanged(string value) => AppSettings.PartyTymeClientId = value;
    partial void OnPartyTymeClientSecretChanged(string value) => AppSettings.PartyTymeClientSecret = value;

    [RelayCommand]
    private async Task ConnectPartyTyme()
    {
        if (string.IsNullOrWhiteSpace(PartyTymeClientId) || string.IsNullOrWhiteSpace(PartyTymeClientSecret))
        {
            PartyTymeConnectionStatus = "Credentials cannot be empty.";
            return;
        }

        PartyTymeConnectionStatus = "Authenticating...";
        IsPartyTymeLoading = true;

        bool success = await _partyTymeService.AuthenticateAsync(PartyTymeClientId, PartyTymeClientSecret);

        IsPartyTymeLoading = false;
        IsPartyTymeConnected = success;
        IsPartyTymeDisconnected = !success;

        if (success)
        {
            PartyTymeConnectionStatus = "Connected successfully!";
            await SearchPartyTyme();
        }
        else
        {
            PartyTymeConnectionStatus = "Authentication failed. Try again.";
        }
    }

    [RelayCommand]
    private async Task SearchPartyTyme()
    {
        if (!IsPartyTymeConnected) return;

        IsPartyTymeLoading = true;
        try
        {
            var results = await _partyTymeService.SearchCatalogAsync(SearchQuery);
            PartyTymeResults.Clear();
            foreach (var track in results)
            {
                PartyTymeResults.Add(track);
            }
        }
        catch (Exception ex)
        {
            PartyTymeConnectionStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsPartyTymeLoading = false;
        }
    }

    [RelayCommand]
    private async Task PlayPartyTymeTrack(PartyTymeTrack track)
    {
        if (track == null) return;

        IsPlaying = false;
        CurrentSongName = $"{track.Artist} - {track.Title} [Party Tyme]";

        IsExternalPerformanceActive = false;
        ExternalPerformanceSource = string.Empty;
        ExternalPerformanceUrl = string.Empty;

        string streamUrl = await _partyTymeService.GetStreamUrlAsync(track.TrackId);

        SelectedSongPath = streamUrl;
        await _mediaEngine.LoadSong(streamUrl);
        await _mediaEngine.Play();
        IsPlaying = true;

        NotifyAudioPropertiesChanged();
    }

    [RelayCommand]
    private async Task CachePartyTymeTrack(PartyTymeTrack track)
    {
        if (track == null) return;

        PartyTymeConnectionStatus = $"Caching '{track.Title}'...";
        IsPartyTymeLoading = true;

        string cachedPath = await _partyTymeService.DownloadTrackAsync(track.TrackId, track.Title, track.Artist);

        IsPartyTymeLoading = false;

        if (!string.IsNullOrEmpty(cachedPath))
        {
            PartyTymeConnectionStatus = $"Cached '{track.Title}' successfully!";
            await SearchPartyTyme();
        }
        else
        {
            PartyTymeConnectionStatus = "Failed to cache track.";
        }
    }
}
