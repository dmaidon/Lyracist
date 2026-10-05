// Edited on Oct 5, 2026 @ 07:52:00 -> Support clear-last-round-done action in request server
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Shared;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
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
        public partial string KioskConnectionUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ImageSource? KioskQrCodeImage { get; set; }

        [ObservableProperty]
        public partial string BillboardConnectionUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ImageSource? BillboardQrCodeImage { get; set; }

        [ObservableProperty]
        public partial ImageSource? WifiQrCodeImage { get; set; }

        [ObservableProperty]
        public partial bool IsDjQrVisible { get; set; }

        [ObservableProperty]
        public partial bool IsKioskQrVisible { get; set; }

        [ObservableProperty]
        public partial bool IsBillboardQrVisible { get; set; }

        [ObservableProperty]
        public partial string HandoffConnectionUrl { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ImageSource? HandoffQrCodeImage { get; set; }

        public ObservableCollection<DiscoveredPeer> DiscoveredPeers { get; } = [];

        [ObservableProperty]
        public partial DiscoveredPeer? SelectedDiscoveredPeer { get; set; }

        [ObservableProperty]
        public partial bool IsSearchingForPeers { get; set; }

        [ObservableProperty]
        public partial string DjPin { get; set; } = string.Empty;

        /// <summary>When true, incoming patron requests are added straight to the rotation instead of waiting here for DJ approval.</summary>
        [ObservableProperty]
        public partial bool AutoAcceptRequests { get; set; }

        [RelayCommand]
        public void AcceptRequest(PatronRequest request)
        {
            if (request == null) return;

            string normalizedName = RotationHelpers.NormalizeForComparison(request.Name);

            var requestedSongs = request.Songs?.Count > 0
                ? request.Songs
                : [new RequestedSong(request.Song, request.Artist)];

            var existingSinger = Singers.FirstOrDefault(s => RotationHelpers.IsSameSingerName(s.Name, normalizedName) && s.IsMusic == (request.RequestType == "Music"));
            if (existingSinger != null)
            {
                if (!string.IsNullOrWhiteSpace(request.DuetPartnerName))
                {
                    existingSinger.DuetPartnerName = request.DuetPartnerName;
                }

                // Only reposition if the singer was actually paused — reactivating them needs to move
                // them back into the active section. An already-active singer (including the one
                // currently performing) accepting a new request should stay exactly where they are.
                bool wasInactive = existingSinger.IsInactive;
                existingSinger.IsInactive = false;
                if (wasInactive)
                {
                    EnforceActiveInactiveOrder(existingSinger);
                }

                foreach (var reqSong in requestedSongs)
                {
                    if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                    if (SingerHasSong(existingSinger, reqSong.Song, reqSong.Artist)) continue;

                    // The patron-portal submission checked this at request time, but the session can
                    // change between submission and a DJ clicking Accept (e.g. someone else performed
                    // or got queued for the same song in the meantime) - re-check here so accepting a
                    // now-stale request doesn't silently let a duplicate back in.
                    if (BlockDuplicateSongsInSession && IsSongInCurrentSession(reqSong.Song, reqSong.Artist)) continue;

                    if (string.IsNullOrWhiteSpace(existingSinger.Song))
                    {
                        existingSinger.Song = reqSong.Song;
                        existingSinger.Artist = reqSong.Artist;
                    }
                    else
                    {
                        existingSinger.QueuedSongs.Add(new QueuedSong(reqSong.Song, reqSong.Artist));
                    }
                }
            }
            else
            {
                var newSinger = new SingerEntry
                {
                    Name = normalizedName,
                    DuetPartnerName = request.DuetPartnerName,
                    IsMusic = request.RequestType == "Music"
                };

                foreach (var reqSong in requestedSongs)
                {
                    if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                    if (SingerHasSong(newSinger, reqSong.Song, reqSong.Artist)) continue;

                    // See matching comment in the existingSinger branch above.
                    if (BlockDuplicateSongsInSession && IsSongInCurrentSession(reqSong.Song, reqSong.Artist)) continue;

                    if (string.IsNullOrWhiteSpace(newSinger.Song))
                    {
                        newSinger.Song = reqSong.Song;
                        newSinger.Artist = reqSong.Artist;
                    }
                    else
                    {
                        newSinger.QueuedSongs.Add(new QueuedSong(reqSong.Song, reqSong.Artist));
                    }
                }

                if (!string.IsNullOrWhiteSpace(newSinger.Song))
                {
                    AddActiveSinger(newSinger);
                    if (!string.IsNullOrWhiteSpace(normalizedName))
                    {
                        AddKnownSinger(normalizedName);
                    }
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
            // Use stored PIN if available (to persist logins across accidental app closures), otherwise generate fresh.
            if (string.IsNullOrWhiteSpace(DjPin))
            {
                DjPin = GenerateDjPin();
                QueueSaveSettings();
            }

            if (IsTestMode)
            {
                IncomingRequests.Add(new PatronRequest { Name = "Charlie Miller", Song = "Let It Be", Artist = "The Beatles", RequestType = "Karaoke" });
                IncomingRequests.Add(new PatronRequest { Name = "Dana Scully", Song = "X-Files Theme", Artist = "Mark Snow", RequestType = "Music" });
                IncomingRequests.Add(new PatronRequest { Name = "Fox Mulder", Song = "I Want to Believe", Artist = "Aliens", RequestType = "Karaoke" });
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
                        HandleDjAction,
                        GetSpecialEventsJson,
                        () => ActiveSpecialEvent,
                        GetVenueInfoJson,
                        (song, artist) => BlockDuplicateSongsInSession && IsSongInCurrentSession(song, artist),
                        () => IsRequestSubmissionAllowed(out string r) ? null : r,
                        OnVenueLocationSyncedFromPeer,
                        ExportSessionHandoffPayload,
                        payload => ImportSessionHandoffPayloadAsync(payload),
                        GetDiscoveredPeerInfo,
                        OnSessionExportedToPeer,
                        OnSingerProfileChangedFromPatron);
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

            if (!started && System.Windows.Application.Current != null)
            {
#if !MAUI
                System.Windows.MessageBox.Show(
                    $"Failed to start local web server on ports {ServerPort} to {ServerPort + 2}.\n\nRequests from travel router or phone portal will not be active.",
                    "Web Server Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
#endif
            }

            ConnectionUrl = started
                ? $"http://{host}:{activePort}"
                : "(patron requests unavailable — port in use)";

            DjConnectionUrl = started
                ? $"http://{host}:{activePort}/dj.html"
                : "";

            KioskConnectionUrl = started
                ? $"http://{host}:{activePort}/kiosk.html"
                : "";

            BillboardConnectionUrl = started
                ? $"http://{host}:{activePort}/billboard.html"
                : "";

            // The PIN deliberately does NOT go in this URL/QR - the handoff page it points to
            // is unauthenticated (anyone can scan/open it), and embedding the PIN here would
            // display the DJ's master credential in plaintext to anyone who scans the code.
            // The receiving side enters the PIN itself, either in the /handoff page or in the
            // app's own Switch Device screen.
            HandoffConnectionUrl = started
                ? $"http://{host}:{activePort}/handoff"
                : "";

            _activeServerPort = activePort;
 
            QrCodeImage = started ? GenerateQRCode(ConnectionUrl) : null;
            DjQrCodeImage = started ? GenerateQRCode(DjConnectionUrl) : null;
            KioskQrCodeImage = started ? GenerateQRCode(KioskConnectionUrl) : null;
            BillboardQrCodeImage = started ? GenerateQRCode(BillboardConnectionUrl) : null;
            HandoffQrCodeImage = started ? GenerateQRCode(HandoffConnectionUrl) : null;
            string startSsid = WifiHelper.GetConnectedSsid() ?? string.Empty;
            string startPass = !string.IsNullOrWhiteSpace(startSsid) ? WifiPasswordStore.GetPasswordForSsid(startSsid) : string.Empty;
            string startWifiPayload = WifiHelper.BuildWifiQrPayload(startSsid, startPass);
            WifiQrCodeImage = started && !string.IsNullOrWhiteSpace(startSsid) ? GenerateQRCode(startWifiPayload) : null;
            _displayWindowService.SetConnectionInfo(ConnectionUrl, QrCodeImage);
            _displayWindowService.SetWifiInfo(startSsid, string.IsNullOrWhiteSpace(startPass) ? "No Password Required" : startPass, WifiQrCodeImage);
            RefreshConnectInstructionsBanner();
        }

        private void OnSingerProfileChangedFromPatron(string singerName)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                AddKnownSinger(singerName);
                _ = LoadAllUsersAsync();
            });
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
            KioskConnectionUrl = $"http://{host}:{_activeServerPort}/kiosk.html";
            BillboardConnectionUrl = $"http://{host}:{_activeServerPort}/billboard.html";
            HandoffConnectionUrl = $"http://{host}:{_activeServerPort}/handoff";
            QrCodeImage = GenerateQRCode(ConnectionUrl);
            DjQrCodeImage = GenerateQRCode(DjConnectionUrl);
            KioskQrCodeImage = GenerateQRCode(KioskConnectionUrl);
            BillboardQrCodeImage = GenerateQRCode(BillboardConnectionUrl);
            HandoffQrCodeImage = GenerateQRCode(HandoffConnectionUrl);
            string refreshSsid = WifiHelper.GetConnectedSsid() ?? string.Empty;
            string refreshPass = !string.IsNullOrWhiteSpace(refreshSsid) ? WifiPasswordStore.GetPasswordForSsid(refreshSsid) : string.Empty;
            string refreshWifiPayload = WifiHelper.BuildWifiQrPayload(refreshSsid, refreshPass);
            WifiQrCodeImage = !string.IsNullOrWhiteSpace(refreshSsid) ? GenerateQRCode(refreshWifiPayload) : null;
            _displayWindowService.SetConnectionInfo(ConnectionUrl, QrCodeImage);
            _displayWindowService.SetWifiInfo(refreshSsid, string.IsNullOrWhiteSpace(refreshPass) ? "No Password Required" : refreshPass, WifiQrCodeImage);
            // Debounced — this runs on every keystroke of PreferredHostIp (UpdateSourceTrigger=PropertyChanged),
            // and the banner regeneration underneath is a full QR render + PNG encode + disk write.
            QueueRefreshConnectInstructionsBanner();
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

        private void HandleRequestReceived(string name, List<RequestedSong> songs, string requestType, string duetPartner)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var first = songs.FirstOrDefault();
                var request = new PatronRequest
                {
                    Name = name,
                    DuetPartnerName = duetPartner,
                    Song = first?.Song ?? string.Empty,
                    Artist = first?.Artist ?? string.Empty,
                    Songs = songs,
                    RequestType = requestType
                };

                IncomingRequests.Add(request);
                if (AutoAcceptRequests)
                {
                    AcceptRequest(request);
                }
            }));
        }

        private void OnVenueLocationSyncedFromPeer(string venueName, double lat, double lon)
        {
            if (string.IsNullOrWhiteSpace(venueName)) return;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!Venues.Any(v => string.Equals(v, venueName, StringComparison.OrdinalIgnoreCase)))
                {
                    Venues.Add(venueName);
                    VenueService.Save(Venues);
                }
                SelectedVenue = venueName;
                VenueName = venueName;
                QueueSaveSettings();
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
                partner = s.Partner,
                song = s.Song,
                artist = s.Artist,
                isCurrent = s.IsCurrent,
                isNext = s.IsNext,
                isInactive = s.IsInactive,
                isPaused = s.IsPaused,
                isSkipped = s.IsSkipped,
                isSpecial = s.IsSpecial,
                isMusic = s.IsMusic,
                isRotationStart = s.IsRotationStart,
                hasSungInLastRound = s.HasSungInLastRound,
                vocalRange = s.VocalRange,
                customTitle = s.CustomTitle,
                queuedSongs = s.QueuedSongs.ConvertAll(q => new QueuedSongDto { song = q.Song, artist = q.Artist }),
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

        private (string Ssid, string Password) _cachedWifiInfo = (string.Empty, string.Empty);
        private DateTime _wifiInfoCachedAt = DateTime.MinValue;
        private readonly object _wifiInfoLock = new();

        // WifiHelper.GetConnectedSsid() can fall back to spawning "netsh wlan show interfaces" and
        // blocking up to 1 second when the native WLAN API path fails - GetVenueInfoJson is wired to
        // /api/info, polled every few seconds by every connected patron/kiosk/billboard client, and
        // the SSID essentially never changes mid-event, so re-querying it on every single poll turned
        // an occasional slow call into a recurring per-client latency hit.
        private static readonly TimeSpan WifiInfoCacheDuration = TimeSpan.FromSeconds(30);

        private string GetVenueInfoJson()
        {
            // Concurrent per-connection request threads (see PatronRequestServer) all call this - a
            // plain check-then-refresh without a lock lets every one of them observe the same stale
            // timestamp at a cache-expiry boundary and independently pay the ~1s netsh fallback, and
            // (string, string) isn't assigned atomically, so a reader could see a torn (newSsid,
            // oldPassword) pair mid-refresh. The lock also spans the read so it always sees a
            // consistent pair, not just a consistent write.
            string ssid, pass;
            lock (_wifiInfoLock)
            {
                if (DateTime.UtcNow - _wifiInfoCachedAt >= WifiInfoCacheDuration)
                {
                    string freshSsid = WifiHelper.GetConnectedSsid() ?? string.Empty;
                    string freshPass = !string.IsNullOrWhiteSpace(freshSsid) ? WifiPasswordStore.GetPasswordForSsid(freshSsid) : string.Empty;
                    _cachedWifiInfo = (freshSsid, freshPass);
                    _wifiInfoCachedAt = DateTime.UtcNow;
                }

                ssid = _cachedWifiInfo.Ssid;
                pass = _cachedWifiInfo.Password;
            }

            var dto = new VenueInfoResponseDto
            {
                venue = VenueName,
                dj = DjName,
                portalUrl = ConnectionUrl,
                billboardUrl = BillboardConnectionUrl,
                wifiSsid = ssid,
                wifiPassword = pass,
                isLastRound = IsLastRound,
                showQrCode = ShowQrCodeOnRotationScreen,
                listDjAndVenue = ListDjAndVenueOnBillboard,
                defaultSongLengthMinutes = DefaultSongLengthMinutes
            };
            return JsonSerializer.Serialize(dto, AppJsonContext.Default.VenueInfoResponseDto);
        }

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

        /// <summary>Serializes the pending patron requests as JSON on the UI thread and caches it.
        /// The TCP server reads the cached string from a background thread without touching the live
        /// ObservableCollection, which must only be mutated/enumerated from the UI thread.</summary>
        private void RebuildRequestsJsonCacheNow()
        {
            _cachedRequestsJson = JsonSerializer.Serialize(IncomingRequests.ToList(), AppJsonContext.Default.ListPatronRequest);
        }

        private string GetRequestsJson() => _cachedRequestsJson;

        // How long a DJ action request will wait for the UI thread before giving up. Without a bound,
        // a stray modal (e.g. the app's own DispatcherUnhandledException message box) stalling the
        // dispatcher would hang this call forever - and with MaxConcurrentConnections capped at 64,
        // enough hung requests here starve the whole server, including patrons just trying to load
        // the request page.
        private static readonly TimeSpan DjActionTimeout = TimeSpan.FromSeconds(5);

        private async Task<string> HandleDjAction(string action, string targetId, string extraData, string name, string song, string artist, string duetPartner, bool isSpecial)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Marshal view updates safely onto the main UI thread using the shimmed Dispatcher.
            // Deliberately not awaited here - completion is observed below via tcs.Task instead,
            // with its own timeout, so this posts the work and returns immediately. WPF's real
            // BeginInvoke returns an awaitable DispatcherOperation (hence the discard, needed now
            // that this method is async); the MAUI shim's BeginInvoke returns void.
#if !MAUI
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
#else
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
#endif
            {
                try
                {
                    string err = ExecuteDjActionOnUi(action, targetId, extraData, name, song, artist, duetPartner, isSpecial);
                    tcs.TrySetResult(err);
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("MainViewModel.HandleDjAction", ex);
                    tcs.TrySetResult("Action failed.");
                }
            });

            try
            {
                return await tcs.Task.WaitAsync(DjActionTimeout);
            }
            catch (TimeoutException)
            {
                return "DJ station is busy. Please try again.";
            }
        }

        private string ExecuteDjActionOnUi(string action, string targetId, string extraData, string name, string song, string artist, string duetPartner, bool isSpecial)
        {
            switch (action.ToLowerInvariant())
            {
                case "accept":
                    {
                        var request = IncomingRequests.FirstOrDefault(r => string.Equals(r.Id, targetId, StringComparison.OrdinalIgnoreCase));
                        if (request == null) return "Request not found.";
                        AcceptRequest(request);
                        return "";
                    }
                case "decline":
                    {
                        var request = IncomingRequests.FirstOrDefault(r => string.Equals(r.Id, targetId, StringComparison.OrdinalIgnoreCase));
                        if (request == null) return "Request not found.";
                        DeclineRequest(request);
                        return "";
                    }
                case "set-current":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        if (singer.IsPaused || singer.IsInactive) return "Singer is paused or inactive.";

                        if (singer.IsSkipped)
                        {
                            singer.IsSkipped = false;
                        }

                        RotationHelpers.SetCurrentSinger(Singers, singer, isLastRound: IsLastRound);

                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "set-rotation-start":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        // ToggleRotationStartSinger handles both directions, including reassigning the
                        // anchor to the next active singer when clearing it - setting IsRotationStart
                        // = false directly here (as this used to) left no singer holding the anchor
                        // until some unrelated mutation happened to call EnsureRotationStartFlag.
                        RotationHelpers.ToggleRotationStartSinger(Singers, singer);

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

                        if (singer.IsInactive) return "Cannot pause a deleted singer.";

                        bool pausing = !singer.IsPaused && singer.IsCurrent;
                        SingerEntry? nextCurrent = pausing ? RotationHelpers.FindNextEligibleSinger(Singers, singer, isLastRound: IsLastRound) : null;

                        singer.IsPaused = !singer.IsPaused;
                        if (singer.IsPaused && singer.IsCurrent)
                        {
                            singer.IsCurrent = false;
                        }

                        if (pausing && nextCurrent != null)
                        {
                            nextCurrent.IsCurrent = true;
                            nextCurrent.IsNext = false;
                        }

                        RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "toggle-skip":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        if (singer.IsInactive) return "Cannot skip an inactive singer.";

                        ToggleSkipSinger(singer);
                        return "";
                    }
                case "toggle-special":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        ToggleSpecialSinger(singer);
                        return "";
                    }
                case "clear-last-round-done":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        ClearLastRoundDone(singer);
                        return "";
                    }
                case "delete":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        if (singer.IsRotationStart)
                        {
                            RotationHelpers.HandleSingerRetiredOrRemoved(Singers, singer);
                        }

                        bool wasCurrent = singer.IsCurrent;
                        SingerEntry? nextCurrent = wasCurrent ? RotationHelpers.FindNextEligibleSinger(Singers, singer, isLastRound: IsLastRound) : null;

                        singer.IsInactive = true;
                        singer.IsCurrent = false;
                        singer.IsNext = false;

                        // Move to the very end of the list
                        int oldIdx = Singers.IndexOf(singer);
                        if (oldIdx != -1)
                        {
                            Singers.Move(oldIdx, Singers.Count - 1);
                        }

                        if (wasCurrent && nextCurrent != null)
                        {
                            RotationHelpers.SetCurrentSinger(Singers, nextCurrent, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                        }

                        RotationHelpers.EnsureRotationStartFlag(Singers);
                        RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "restore":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        if (singer.IsInactive)
                        {
                            singer.IsInactive = false;
                            int oldIndex = Singers.IndexOf(singer);
                            if (oldIndex != -1)
                            {
                                int activeCount = 0;
                                for (int i = 0; i < Singers.Count; i++)
                                {
                                    if (!Singers[i].IsInactive && Singers[i] != singer)
                                    {
                                        activeCount++;
                                    }
                                }
                                Singers.Move(oldIndex, activeCount);
                            }
                            RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                            RebuildRotationJsonCacheNow();
                            QueueSaveDatabase();
                        }
                        return "";
                    }
                case "add-singer":
                case "add-performer":
                    {
                        if (!TryAddPerformer(name, song, artist, duetPartner, isSpecial)) return "Singer name is required.";

                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "remove-queued-song":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        if (int.TryParse(extraData, out int songIndex) && songIndex >= 0 && songIndex < singer.QueuedSongs.Count)
                        {
                            singer.QueuedSongs.RemoveAt(songIndex);
                            RebuildRotationJsonCacheNow();
                            QueueSaveDatabase();
                            return "";
                        }
                        return "Invalid queued song index.";
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

                            RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                            RebuildRotationJsonCacheNow();
                            QueueSaveDatabase();
                            return "";
                        }
                        return "Invalid round index.";
                    }
                case "set-special-event":
                    {
                        string performerName = !string.IsNullOrWhiteSpace(name) ? name : extraData;
                        if (targetId.Equals("Birthday", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(performerName))
                        {
                            try
                            {
                                string birthdayFilePath = Path.Combine(Globals.EventBannersDir, "Birthday.png");
                                DjBannerFileManager.CreatePersonalizedBirthdayBannerPng(birthdayFilePath, performerName);
                            }
                            catch (Exception ex)
                            {
                                LoggerService.LogError("Failed creating personalized birthday banner", ex);
                            }
                        }
                        else if (targetId.Equals("Announcement", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(performerName))
                        {
                            try
                            {
                                DjBannerFileManager.CreateAnnouncementBanner(Globals.EventBannersDir, performerName);
                            }
                            catch (Exception ex)
                            {
                                LoggerService.LogError("Failed creating dynamic announcement banner", ex);
                            }
                        }
                        ActiveSpecialEvent = targetId;
                        UpdateDjBannerPath();
                        UpdateLastSongState();
                        return "";
                    }
                case "toggle-last-round":
                    {
                        ToggleLastRound();
                        QueueSaveDatabase();
                        return "";
                    }
                case "set-last-round":
                    {
                        if (bool.TryParse(extraData, out bool lrVal))
                        {
                            IsLastRound = lrVal;
                        }
                        else
                        {
                            ToggleLastRound();
                        }
                        QueueSaveDatabase();
                        return "";
                    }
                case "next-singer":
                    {
                        var current = RotationHelpers.GetCurrentSinger(Singers);
                        if (current != null)
                        {
                            var next = RotationHelpers.GetNextActiveSingers(Singers, current, 1, isLastRound: IsLastRound).FirstOrDefault();
                            if (next != null)
                            {
                                RotationHelpers.SetCurrentSinger(Singers, next, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                                RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                                RebuildRotationJsonCacheNow();
                                QueueSaveDatabase();
                            }
                        }
                        else
                        {
                            var first = Singers.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped && (!IsLastRound || !s.HasSungInLastRound));
                            if (first != null)
                            {
                                RotationHelpers.SetCurrentSinger(Singers, first, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                                RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                                RebuildRotationJsonCacheNow();
                                QueueSaveDatabase();
                            }
                        }
                        return "";
                    }
                case "previous-singer":
                    {
                        var current = RotationHelpers.GetCurrentSinger(Singers);
                        if (current != null)
                        {
                            int currentIndex = Singers.IndexOf(current);
                            int count = Singers.Count;
                            for (int i = 1; i < count; i++)
                            {
                                int prevIndex = (currentIndex - i + count) % count;
                                var candidate = Singers[prevIndex];
                                if (!candidate.IsInactive && !candidate.IsPaused && !candidate.IsSkipped && (!IsLastRound || !candidate.HasSungInLastRound))
                                {
                                    RotationHelpers.SetCurrentSinger(Singers, candidate, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                                    RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);
                                    RebuildRotationJsonCacheNow();
                                    QueueSaveDatabase();
                                    break;
                                }
                            }
                        }
                        return "";
                    }
                default:
                    return $"Unsupported action: '{action}'";
            }
        }

        private string GetSpecialEventsJson()
        {
            var data = new
            {
                activeSpecialEvent = ActiveSpecialEvent,
                specialEvents = SpecialEvents.Select(e => e.EventName).ToList()
            };
            return JsonSerializer.Serialize(data);
        }

        private void AcceptAllPendingRequests()
        {
            var requests = IncomingRequests.ToList();
            foreach (var req in requests)
            {
                AcceptRequest(req);
            }
        }

        public DiscoveredPeer GetDiscoveredPeerInfo()
        {
            var cur = RotationHelpers.GetCurrentSinger(Singers);
            return new DiscoveredPeer
            {
                Host = ResolveConnectionHost(),
                Port = _activeServerPort,
                AppName = "KSRotation",
                DeviceName = Environment.MachineName,
                VenueName = VenueName,
                DjName = DjName,
                SingerCount = Singers.Count(s => !s.IsInactive),
                CurrentSinger = cur != null ? $"{cur.Name} - {cur.Song}" : "None"
            };
        }

        public async Task<(bool Success, string? Error)> PullSessionFromHostAsync(string host, int port, string pin)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return (false, "Host address cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(pin))
            {
                return (false, "DJ PIN is required.");
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.Add("X-DJ-PIN", pin ?? string.Empty);

                int localPort = _activeServerPort > 0 ? _activeServerPort : ServerPort;
                string localPin = DjPin ?? string.Empty;

                string url = $"http://{host.Trim()}:{port}/api/session/handoff?pin={Uri.EscapeDataString(pin ?? string.Empty)}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return (false, "Unauthorized: Incorrect or missing DJ PIN.");
                    }
                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        return (false, "Too many PIN attempts. Please wait 1 minute before trying again.");
                    }
                    return (false, $"Server returned error: {response.StatusCode}");
                }

                string json = await response.Content.ReadAsStringAsync();
                var payload = JsonSerializer.Deserialize<SessionHandoffPayload>(json, AppJsonContext.Default.SessionHandoffPayload);
                if (payload == null)
                {
                    return (false, "Received invalid or empty session payload.");
                }

                // requireConfirmation: false - the caller (Switch Device UI on WPF and MAUI) already
                // showed its own "this will replace..." confirmation before calling this method.
                string? importError = await ImportSessionHandoffPayloadAsync(payload, requireConfirmation: false);
                if (!string.IsNullOrEmpty(importError))
                {
                    return (false, importError);
                }

                // Only now that the import has actually succeeded locally do we tell the source
                // device the handoff is complete - that's what drives its auto-switch into Remote
                // DJ mode, so firing it any earlier (e.g. right after the export GET above) could
                // leave the source device switched away from its own host UI even though this
                // device never actually took over the session.
                try
                {
                    string ackUrl = $"http://{host.Trim()}:{port}/api/session/handoff/ack?pin={Uri.EscapeDataString(pin ?? string.Empty)}&hostPort={localPort}&djPin={Uri.EscapeDataString(localPin)}";
                    await client.GetAsync(ackUrl);
                }
                catch (Exception ackEx)
                {
                    LoggerService.LogError("MainViewModel.PullSessionFromHostAsync.Ack", ackEx);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.PullSessionFromHostAsync", ex);
                return (false, ex.Message);
            }
        }

        private void OnSessionExportedToPeer(string peerHost, int peerPort, string peerPin)
        {
            SessionHandedOffToPeer?.Invoke(peerHost, peerPort, peerPin);
        }

        public async Task DiscoverPeersOnLanAsync()
        {
            if (IsSearchingForPeers) return;
            IsSearchingForPeers = true;
            DiscoveredPeers.Clear();

            try
            {
                var localIp = LocalNetworkHelper.GetLocalIPv4Address();
                if (localIp == null) return;

                byte[] ipBytes = localIp.GetAddressBytes();
                string subnetPrefix = $"{ipBytes[0]}.{ipBytes[1]}.{ipBytes[2]}.";
                int myLastOctet = ipBytes[3];

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var tasks = new List<Task<DiscoveredPeer?>>();

                for (int i = 1; i <= 254; i++)
                {
                    if (i == myLastOctet) continue;
                    string candidateIp = subnetPrefix + i;
                    tasks.Add(ProbePeerAsync(candidateIp, _activeServerPort, cts.Token));
                }

                var results = await Task.WhenAll(tasks);
                foreach (var peer in results)
                {
                    if (peer != null)
                    {
                        DiscoveredPeers.Add(peer);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.DiscoverPeersOnLanAsync", ex);
            }
            finally
            {
                IsSearchingForPeers = false;
            }
        }

        // Shared across the whole LAN sweep instead of a fresh HttpClient per candidate IP - a
        // /24 scan can probe up to 253 addresses, and standing up a new client (and its socket
        // handler) for each one is wasteful churn on a tablet's already-constrained connection
        // pool. Per-probe timeout is enforced with a linked CancellationTokenSource below instead
        // of HttpClient.Timeout, since that's an instance-level setting.
        private static readonly HttpClient s_peerProbeClient = new();

        private static async Task<DiscoveredPeer?> ProbePeerAsync(string ip, int port, CancellationToken token)
        {
            try
            {
                using var probeTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                probeTimeoutCts.CancelAfter(TimeSpan.FromMilliseconds(500));
                string url = $"http://{ip}:{port}/api/device/probe";
                var res = await s_peerProbeClient.GetAsync(url, probeTimeoutCts.Token);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(token);
                    var peer = JsonSerializer.Deserialize<DiscoveredPeer>(json, AppJsonContext.Default.DiscoveredPeer);
                    if (peer != null)
                    {
                        peer.Host = ip;
                        peer.Port = port;
                        return peer;
                    }
                }
            }
            catch
            {
                // Inevitable timeout / socket failure for nonexistent hosts
            }
            return null;
        }
    }
}
