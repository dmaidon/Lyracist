// Last Edit: Jul 02, 2026 14:10 - Added manual PreferredHostIp override support for patron portal URL and QR generation.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using KSRotation.Services;
using QRCoder;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
#if !MAUI
using System.Windows.Media;
using System.Windows.Media.Imaging;
#else
using ImageSource = Microsoft.Maui.Controls.ImageSource;
#endif

namespace KSRotation.ViewModels
{
    public partial class MainViewModel
    {
        public ObservableCollection<PatronRequest> IncomingRequests { get; } = [];

        [ObservableProperty]
        public partial string ConnectionUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ImageSource? QrCodeImage { get; set; }

        [ObservableProperty]
        public partial string DjConnectionUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ImageSource? DjQrCodeImage { get; set; }

        [ObservableProperty]
        public partial bool IsDjQrVisible { get; set; }

        [ObservableProperty]
        public partial string DjPin { get; set; } = string.Empty;

        [RelayCommand]
        public void AcceptRequest(PatronRequest request)
        {
            if (request == null) return;

            var existingSinger = Singers.FirstOrDefault(s => string.Equals(s.Name?.Trim(), request.Name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existingSinger != null)
            {
                // Only reposition if the singer was actually paused — reactivating them needs to move
                // them back into the active section. An already-active singer (including the one
                // currently performing) accepting a new request should stay exactly where they are.
                bool wasInactive = existingSinger.IsInactive;
                existingSinger.IsInactive = false;
                if (wasInactive)
                {
                    EnforceActiveInactiveOrder(existingSinger);
                }

                if (string.IsNullOrWhiteSpace(existingSinger.Song))
                {
                    existingSinger.Song = request.Song;
                    existingSinger.Artist = request.Artist;
                }
                else
                {
                    existingSinger.QueuedSongs.Add(new QueuedSong(request.Song, request.Artist));
                }
            }
            else
            {
                AddActiveSinger(new SingerEntry
                {
                    Name = request.Name,
                    Song = request.Song,
                    Artist = request.Artist
                });
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    AddKnownSinger(request.Name);
                }
            }

            IncomingRequests.Remove(request);
            QueueSaveSettings();
            QueueSaveDatabase();
        }

        [RelayCommand]
        public void DeclineRequest(PatronRequest request)
        {
            if (request == null) return;
            IncomingRequests.Remove(request);
        }

        private void StartRequestServer()
        {
            // Fresh random PIN each app session — shown on-screen to the operator so they can hand it
            // to the DJ verbally. Replaces the previous hardcoded PIN, which any device on the venue's
            // Wi-Fi could guess instantly.
            DjPin = GenerateDjPin();

            if (IsTestMode)
            {
                IncomingRequests.Add(new PatronRequest { Name = "Charlie Miller", Song = "Let It Be", Artist = "The Beatles" });
                IncomingRequests.Add(new PatronRequest { Name = "Dana Scully", Song = "X-Files Theme", Artist = "Mark Snow" });
                IncomingRequests.Add(new PatronRequest { Name = "Fox Mulder", Song = "I Want to Believe", Artist = "Aliens" });
            }

            string host = ResolveConnectionHost();

            int activePort = ServerPort;
            bool started = false;
            for (int p = ServerPort; p < ServerPort + 3 && !started; p++)
            {
                try
                {
                    _requestServer = new PatronRequestServer(
                        p, 
                        HandleRequestReceived, 
                        GetRotationJson,
                        VerifyDjPin,
                        GetRequestsJson,
                        HandleDjAction);
                    _requestServer.Start();
                    activePort = p;
                    started = true;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError($"StartRequestServer port {p}", ex);
                    _requestServer = null;
                }
            }

            ConnectionUrl = started
                ? $"http://{host}:{activePort}"
                : "(patron requests unavailable — port in use)";

            DjConnectionUrl = started
                ? $"http://{host}:{activePort}/dj.html"
                : "";
 
            _activeServerPort = activePort;
 
            QrCodeImage = started ? GenerateQRCode(ConnectionUrl) : null;
            DjQrCodeImage = started ? GenerateQRCode(DjConnectionUrl, [239, 68, 68], [255, 255, 255]) : null;
            _displayWindowService.SetConnectionInfo(ConnectionUrl, QrCodeImage);
        }

        private void RefreshConnectionInfo()
        {
            if (_requestServer is null || string.IsNullOrWhiteSpace(ConnectionUrl) || !ConnectionUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string host = ResolveConnectionHost();
            ConnectionUrl = $"http://{host}:{_activeServerPort}";
            DjConnectionUrl = $"http://{host}:{_activeServerPort}/dj.html";
            QrCodeImage = GenerateQRCode(ConnectionUrl);
            DjQrCodeImage = GenerateQRCode(DjConnectionUrl, [239, 68, 68], [255, 255, 255]);
            _displayWindowService.SetConnectionInfo(ConnectionUrl, QrCodeImage);
        }

        private string ResolveConnectionHost()
        {
            string configuredHostIp = PreferredHostIp?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(configuredHostIp))
            {
                return GetLocalIPAddress();
            }

            if (IPAddress.TryParse(configuredHostIp, out IPAddress? parsedAddress)
                && parsedAddress.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(parsedAddress))
            {
                return parsedAddress.ToString();
            }

            return GetLocalIPAddress();
        }

        private static ImageSource? GenerateQRCode(string text, byte[]? darkColorRgb = null, byte[]? lightColorRgb = null)
        {
            if (string.IsNullOrEmpty(text)) return null;
 
            try
            {
                using QRCodeGenerator qrGenerator = new();
                using QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                using PngByteQRCode qrCode = new(qrCodeData);
                byte[] qrCodeBytes = (darkColorRgb != null && lightColorRgb != null)
                    ? qrCode.GetGraphic(20, darkColorRgb, lightColorRgb, false)
                    : qrCode.GetGraphic(20);
#if !MAUI
                using MemoryStream ms = new(qrCodeBytes);
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
#else
                return ImageSource.FromStream(() => new MemoryStream(qrCodeBytes));
#endif
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.GenerateQRCode", ex);
                return null;
            }
        }

        private void HandleRequestReceived(string name, string song, string artist)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                IncomingRequests.Add(new PatronRequest
                {
                    Name = name,
                    Song = song,
                    Artist = artist
                });
            }));
        }

        /// <summary>Debounces a rotation JSON cache rebuild. Per-keystroke edits (Name/Song/Artist) coalesce into a
        /// single rebuild ~250 ms after typing stops, instead of re-serializing the whole list on every character.
        /// Patron web clients only poll every 5 s, so this latency is invisible to them.</summary>
        private void QueueRebuildRotationJsonCache()
        {
            _jsonCacheDebounceTimer.Stop();
            _jsonCacheDebounceTimer.Start();
        }

        /// <summary>Serializes the current singer queue as JSON on the UI thread and caches it.
        /// The TCP server reads the cached string from a background thread without synchronization overhead.</summary>
        private void RebuildRotationJsonCacheNow()
        {
            // A pending debounced rebuild is now redundant.
            _jsonCacheDebounceTimer.Stop();
            var queue = Singers.Select(s => new RotationItemDto
            {
                id = s.Id.ToString(),
                name = s.Name,
                song = s.Song,
                artist = s.Artist,
                isCurrent = s.IsCurrent,
                isNext = s.IsNext,
                isInactive = s.IsInactive,
                song1Completed = s.Song1Completed,
                song2Completed = s.Song2Completed,
                song3Completed = s.Song3Completed,
                song4Completed = s.Song4Completed,
                song5Completed = s.Song5Completed,
                song6Completed = s.Song6Completed,
                song7Completed = s.Song7Completed,
                song8Completed = s.Song8Completed,
                song9Completed = s.Song9Completed,
                song10Completed = s.Song10Completed
            }).ToList();
            _cachedRotationJson = JsonSerializer.Serialize(queue, AppJsonContext.Default.ListRotationItemDto);
        }

        private string GetRotationJson() => _cachedRotationJson;

        private static string GetLocalIPAddress()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    try
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up)
                        {
                            continue;
                        }

                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback
                            || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        {
                            continue;
                        }

                        // Deliberately not filtering on IPInterfaceProperties.GatewayAddresses here — it
                        // throws PlatformNotSupportedException on Android (and potentially other MAUI
                        // targets), which would abort this whole search on the first interface checked.
                        IPInterfaceProperties properties = nic.GetIPProperties();

                        foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                        {
                            if (unicast.Address.AddressFamily == AddressFamily.InterNetwork
                                && !IPAddress.IsLoopback(unicast.Address))
                            {
                                return unicast.Address.ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // One unreadable NIC shouldn't stop us from checking the rest.
                        LoggerService.LogError($"MainViewModel.GetLocalIPAddress nic={nic.Name}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.GetLocalIPAddress", ex);
            }
            return "127.0.0.1";
        }

        private static string GenerateDjPin()
        {
            // 6-digit numeric PIN, generated with a CSPRNG rather than a fixed value so every
            // app session has a distinct code that must be read off this screen to use.
            return System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        }

        private bool VerifyDjPin(string pin)
        {
            if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(DjPin))
            {
                return false;
            }

            byte[] provided = System.Text.Encoding.UTF8.GetBytes(pin);
            byte[] expected = System.Text.Encoding.UTF8.GetBytes(DjPin);

            // Constant-time comparison so response timing can't be used to narrow down the PIN digit-by-digit.
            return provided.Length == expected.Length
                && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(provided, expected);
        }

        private string GetRequestsJson()
        {
            return JsonSerializer.Serialize(IncomingRequests.ToList(), AppJsonContext.Default.ListPatronRequest);
        }

        private string HandleDjAction(string action, string targetId, string extraData, string name, string song, string artist)
        {
            var tcs = new TaskCompletionSource<string>();

            // Marshal view updates safely onto the main UI thread using the shimmed Dispatcher
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    string err = ExecuteDjActionOnUi(action, targetId, extraData, name, song, artist);
                    tcs.SetResult(err);
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("MainViewModel.HandleDjAction", ex);
                    tcs.SetResult("Action failed.");
                }
            });

            return tcs.Task.Result;
        }

        private string ExecuteDjActionOnUi(string action, string targetId, string extraData, string name, string song, string artist)
        {
            switch (action.ToLowerInvariant())
            {
                case "accept":
                    {
                        var request = IncomingRequests.FirstOrDefault(r => string.Equals(r.Name?.Trim(), targetId?.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (request == null) return "Request not found.";
                        AcceptRequest(request);
                        return "";
                    }
                case "decline":
                    {
                        var request = IncomingRequests.FirstOrDefault(r => string.Equals(r.Name?.Trim(), targetId?.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (request == null) return "Request not found.";
                        DeclineRequest(request);
                        return "";
                    }
                case "set-current":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        RotationHelpers.SetCurrentSinger(Singers, singer);

                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "finish-song":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";
                        FinishSingerSong(singer);
                        return "";
                    }
                case "move-up":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";
                        MoveSingerUp(singer);
                        return "";
                    }
                case "move-down":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";
                        MoveSingerDown(singer);
                        return "";
                    }
                case "toggle-inactive":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";
                        
                        singer.IsInactive = !singer.IsInactive;
                        if (singer.IsInactive && singer.IsCurrent)
                        {
                            singer.IsCurrent = false;
                        }
                        
                        RotationHelpers.UpdateNextSingerHighlight(Singers);
                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "delete":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";
                        
                        var isCurrent = singer.IsCurrent;
                        Singers.Remove(singer);
                        if (isCurrent)
                        {
                            RotationHelpers.UpdateNextSingerHighlight(Singers);
                        }
                        
                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "add-singer":
                    {
                        if (!TryAddPerformer(name, song, artist)) return "Singer name is required.";

                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "complete-round":
                case "clear-round":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";
                        
                        if (int.TryParse(extraData, out int round) && round >= 1 && round <= 10)
                        {
                            bool isCompleted = action.Equals("complete-round", StringComparison.OrdinalIgnoreCase);
                            singer.SetRoundCompleted(round, isCompleted);
                            
                            RotationHelpers.UpdateNextSingerHighlight(Singers);
                            RebuildRotationJsonCacheNow();
                            QueueSaveDatabase();
                            return "";
                        }
                        return "Invalid round index.";
                    }
                default:
                    return $"Unsupported action: '{action}'";
            }
        }
    }
}
