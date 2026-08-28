// Edited on Aug 28, 2026 @ 10:58:00 -> Update QR code colors to dark purple for Wi-Fi and dark green for Game Arena
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnockoutTrivia.Models;
using KnockoutTrivia.Services;
using Lyracist.Shared;

namespace KnockoutTrivia.ViewModels;

public partial class ConnectViewModel : ViewModelBase
{
    private readonly IGameStateService _gameStateService;
    private readonly IKnockoutWebServer _webServer;

    [ObservableProperty]
    private string _wifiSsid = string.Empty;

    [ObservableProperty]
    private string _wifiPassword = string.Empty;

    [ObservableProperty]
    private string _wifiPasswordDisplay = "No Password Required";

    [ObservableProperty]
    private string _connectUrl = "http://127.0.0.1:8088";

    [ObservableProperty]
    private BitmapSource? _wifiQrCodeImage;

    [ObservableProperty]
    private BitmapSource? _qrCodeImage;

    [ObservableProperty]
    private string _venueName = "Knockout Arena";

    [ObservableProperty]
    private int _connectedPlayersCount;

    [ObservableProperty]
    private string _statusMessage = "Ready for players to connect";

    public ObservableCollection<KnockoutPlayer> Players => _gameStateService.Players;

    public event EventHandler? PushToAudienceRequested;
    public event EventHandler? GameStartRequested;

    public ConnectViewModel(
        IGameStateService gameStateService,
        IKnockoutWebServer webServer)
    {
        _gameStateService = gameStateService;
        _webServer = webServer;

        _gameStateService.Players.CollectionChanged += (s, e) =>
        {
            UpdatePlayerCount();
        };

        InitializeNetwork();
    }

    public void InitializeNetwork()
    {
        VenueName = _gameStateService.Settings.VenueName;

        // Detect or load saved SSID
        string? detectedSsid = WifiHelper.GetConnectedSsid();
        string effectiveSsid = !string.IsNullOrWhiteSpace(_gameStateService.Settings.WifiSsid)
            ? _gameStateService.Settings.WifiSsid
            : (detectedSsid ?? "Venue Wi-Fi");

        WifiSsid = effectiveSsid;

        // Load password from settings or store
        string savedPwd = !string.IsNullOrWhiteSpace(_gameStateService.Settings.WifiPassword)
            ? _gameStateService.Settings.WifiPassword
            : WifiPasswordStore.GetPasswordForSsid(effectiveSsid);

        WifiPassword = savedPwd;
        WifiPasswordDisplay = string.IsNullOrWhiteSpace(savedPwd) ? "No Password Required" : savedPwd;

        ConnectUrl = _webServer.ConnectUrl;

        GenerateQrCodes();
        UpdatePlayerCount();
    }

    private void UpdatePlayerCount()
    {
        ConnectedPlayersCount = _gameStateService.Players.Count;
    }

    public void UpdateWifiCredentials(string ssid, string password)
    {
        WifiSsid = string.IsNullOrWhiteSpace(ssid) ? (WifiHelper.GetConnectedSsid() ?? "Venue Wi-Fi") : ssid.Trim();
        WifiPassword = password.Trim();
        WifiPasswordDisplay = string.IsNullOrWhiteSpace(WifiPassword) ? "No Password Required" : WifiPassword;

        // Persist to store & settings
        WifiPasswordStore.SetPasswordForSsid(WifiSsid, WifiPassword);
        _gameStateService.Settings.WifiSsid = WifiSsid;
        _gameStateService.Settings.WifiPassword = WifiPassword;

        GenerateQrCodes();
        StatusMessage = "Wi-Fi credentials updated";
    }

    public void GenerateQrCodes()
    {
        // 1. Generate Game URL QR Code (Dark Green modules on White)
        try
        {
            ConnectUrl = _webServer.ConnectUrl;
            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(ConnectUrl, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(data);
            byte[] bytes = qrCode.GetGraphic(20, [6, 78, 59, 255], [255, 255, 255, 255]); // Dark green (#064E3B) modules on white

            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            QrCodeImage = image;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to generate Game QR: {ex.Message}");
            QrCodeImage = null;
        }

        // 2. Generate Wi-Fi QR Code (Dark Purple modules on White)
        try
        {
            string payload = string.IsNullOrWhiteSpace(WifiPassword)
                ? $"WIFI:S:{WifiSsid};T:nopass;;;"
                : $"WIFI:S:{WifiSsid};T:WPA;P:{WifiPassword};;";

            using var generator = new QRCoder.QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(data);
            byte[] bytes = qrCode.GetGraphic(20, [76, 29, 149, 255], [255, 255, 255, 255]); // Dark purple (#4C1D95) modules on white

            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            WifiQrCodeImage = image;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to generate Wi-Fi QR: {ex.Message}");
            WifiQrCodeImage = null;
        }
    }

    [RelayCommand]
    private void RefreshConnection()
    {
        InitializeNetwork();
        StatusMessage = "Connection & QR codes refreshed";
    }

    [RelayCommand]
    private void CopyUrl()
    {
        try
        {
            Clipboard.SetText(ConnectUrl);
            StatusMessage = "Game URL copied to clipboard!";
        }
        catch { }
    }

    [RelayCommand]
    private void StartGame()
    {
        _gameStateService.StartGame();
        GameStartRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void PushToAudience()
    {
        PushToAudienceRequested?.Invoke(this, EventArgs.Empty);
        StatusMessage = "Connect Screen sent to Audience Display";
    }
}
