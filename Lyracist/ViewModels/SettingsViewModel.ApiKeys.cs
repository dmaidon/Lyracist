// Edited on Aug 6, 2026 @ 08:40:07 -> Remove PartyTyme setting fields
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
    private string _staticIPAddress = AppSettings.StaticIPAddress;

    partial void OnYouTubeApiKeyChanged(string value) => AppSettings.YouTubeApiKey = value;
    partial void OnSpotifyClientIdChanged(string value) => AppSettings.SpotifyClientId = value;
    partial void OnSpotifyClientSecretChanged(string value) => AppSettings.SpotifyClientSecret = value;
    partial void OnAmazonAccessKeyChanged(string value) => AppSettings.AmazonAccessKey = value;
    partial void OnAmazonSecretKeyChanged(string value) => AppSettings.AmazonSecretKey = value;
    partial void OnStaticIPAddressChanged(string value)
    {
        AppSettings.StaticIPAddress = value;
        _rotationWindowVm.RefreshQrCode();
    }
}
