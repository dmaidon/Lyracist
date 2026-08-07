// Edited on Aug 6, 2026 @ 09:12:55 -> Populate vocalRange and customTitle properties in RotationItemDto
// Edited on Aug 4, 2026 @ 10:24:00 -> Add AcceptAllPendingRequests method to auto-accept pending requests when checked
// Edited on Jul 28, 2026 @ 18:40:00 -> Add support for processing patron music requests and mapping isMusic
// Last Edit: Jul 28, 2026 12:44 - Serialize isPaused and implement paused/deleted/restore API actions
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Shared;
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

        /// <summary>When true, incoming patron requests are added straight to the rotation instead of waiting here for DJ approval.</summary>
        [ObservableProperty]
        public partial bool AutoAcceptRequests { get; set; }

        [RelayCommand]
        public void AcceptRequest(PatronRequest request)
        {
            if (request == null) return;

            string normalizedName = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                normalizedName = System.Text.RegularExpressions.Regex.Replace(
                    request.Name.Replace('\u00A0', ' ').Replace('\t', ' '),
                    @"\s+",
                    " "
                ).Trim();
            }

            var requestedSongs = request.Songs != null && request.Songs.Count > 0
                ? request.Songs
                : new List<RequestedSong> { new RequestedSong(request.Song, request.Artist) };

            var existingSinger = Singers.FirstOrDefault(s => IsSameSingerName(s.Name, normalizedName) && s.IsMusic == (request.RequestType == "Music"));
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

                foreach (var reqSong in requestedSongs)
                {
                    if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                    if (SingerHasSong(existingSinger, reqSong.Song, reqSong.Artist)) continue;

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
                    IsMusic = request.RequestType == "Music"
                };

                foreach (var reqSong in requestedSongs)
                {
                    if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                    if (SingerHasSong(newSinger, reqSong.Song, reqSong.Artist)) continue;

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

        private void HandleRequestReceived(string name, List<RequestedSong> songs, string requestType)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var first = songs.FirstOrDefault();
                var request = new PatronRequest
                {
                    Name = name,
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
                isPaused = s.IsPaused,
                isMusic = s.IsMusic,
                vocalRange = s.VocalRange,
                customTitle = s.CustomTitle,
                queuedSongs = s.QueuedSongs.Select(q => new QueuedSongDto { song = q.Song, artist = q.Artist }).ToList(),
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

                        if (singer.IsInactive) return "Cannot pause a deleted singer.";

                        bool pausing = !singer.IsPaused && singer.IsCurrent;
                        SingerEntry? nextCurrent = null;

                        if (pausing)
                        {
                            // 1. Try the singer already flagged as Next (manual next-singer override)
                            nextCurrent = Singers.FirstOrDefault(s => s != singer && s.IsNext && !s.IsInactive && !s.IsPaused);

                            if (nextCurrent == null)
                            {
                                // 2. Fall back to standard index-based rotation
                                int currentIndex = Singers.IndexOf(singer);
                                int count = Singers.Count;
                                for (int i = 1; i < count; i++)
                                {
                                    SingerEntry candidate = Singers[(currentIndex + i) % count];
                                    if (candidate != singer && !candidate.IsInactive && !candidate.IsPaused)
                                    {
                                        nextCurrent = candidate;
                                        break;
                                    }
                                }
                            }
                        }

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

                        RotationHelpers.UpdateNextSingerHighlight(Singers);
                        RebuildRotationJsonCacheNow();
                        QueueSaveDatabase();
                        return "";
                    }
                case "delete":
                    {
                        var singer = Singers.FirstOrDefault(s => string.Equals(s.Id.ToString(), targetId, StringComparison.OrdinalIgnoreCase));
                        if (singer == null) return "Singer not found.";

                        bool wasCurrent = singer.IsCurrent;
                        SingerEntry? nextCurrent = null;

                        if (wasCurrent)
                        {
                            // 1. Try the singer already flagged as Next (manual next-singer override)
                            nextCurrent = Singers.FirstOrDefault(s => s != singer && s.IsNext && !s.IsInactive && !s.IsPaused);

                            if (nextCurrent == null)
                            {
                                // 2. Fall back to standard index-based rotation
                                int currentIndex = Singers.IndexOf(singer);
                                int count = Singers.Count;
                                for (int i = 1; i < count; i++)
                                {
                                    SingerEntry candidate = Singers[(currentIndex + i) % count];
                                    if (candidate != singer && !candidate.IsInactive && !candidate.IsPaused)
                                    {
                                        nextCurrent = candidate;
                                        break;
                                    }
                                }
                            }
                        }

                        singer.IsInactive = true;
                        singer.IsCurrent = false;
                        singer.IsNext = false;

                        // Move to the very end of the list
                        int oldIdx = Singers.IndexOf(singer);
                        if (oldIdx != -1)
                        {
                            Singers.Move(oldIdx, Singers.Count - 1);
                        }

                        if (wasCurrent)
                        {
                            if (nextCurrent != null)
                            {
                                nextCurrent.IsCurrent = true;
                                nextCurrent.IsNext = false;
                            }
                        }

                        RotationHelpers.UpdateNextSingerHighlight(Singers);
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
                            RotationHelpers.UpdateNextSingerHighlight(Singers);
                            RebuildRotationJsonCacheNow();
                            QueueSaveDatabase();
                        }
                        return "";
                    }
                case "add-singer":
                    {
                        if (!TryAddPerformer(name, song, artist)) return "Singer name is required.";

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

        private void AcceptAllPendingRequests()
        {
            var requests = System.Linq.Enumerable.ToList(IncomingRequests);
            foreach (var req in requests)
            {
                AcceptRequest(req);
            }
        }
    }
}
