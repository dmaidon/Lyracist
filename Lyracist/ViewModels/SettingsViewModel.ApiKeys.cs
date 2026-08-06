// Created on Aug 6, 2026 @ 07:01:27 -> Split service login/API key settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Core.Helpers;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // ─── Service Logins & API Keys ──────────────────────────────────────────

    [ObservableProperty]
    private string _youTubeApiKey = AppSettings.YouTubeApiKey;

    [ObservableProperty]
    private string _spotifyClientId = AppSettings.SpotifyClientId;

    [ObservableProperty]
    private string _spotifyClientSecret = AppSettings.SpotifyClientSecret;

    [ObservableProperty]
    private string _amazonAccessKey = AppSettings.AmazonAccessKey;

    [ObservableProperty]
    private string _amazonSecretKey = AppSettings.AmazonSecretKey;

    [ObservableProperty]
    private string _partyTymeClientId = AppSettings.PartyTymeClientId;

    [ObservableProperty]
    private string _partyTymeClientSecret = AppSettings.PartyTymeClientSecret;

    [ObservableProperty]
    private string _staticIPAddress = AppSettings.StaticIPAddress;

    partial void OnYouTubeApiKeyChanged(string value) => AppSettings.YouTubeApiKey = value;
    partial void OnSpotifyClientIdChanged(string value) => AppSettings.SpotifyClientId = value;
    partial void OnSpotifyClientSecretChanged(string value) => AppSettings.SpotifyClientSecret = value;
    partial void OnAmazonAccessKeyChanged(string value) => AppSettings.AmazonAccessKey = value;
    partial void OnAmazonSecretKeyChanged(string value) => AppSettings.AmazonSecretKey = value;
    partial void OnPartyTymeClientIdChanged(string value) => AppSettings.PartyTymeClientId = value;
    partial void OnPartyTymeClientSecretChanged(string value) => AppSettings.PartyTymeClientSecret = value;
    partial void OnStaticIPAddressChanged(string value)
    {
        AppSettings.StaticIPAddress = value;
        _rotationWindowVm.RefreshQrCode();
    }
}
