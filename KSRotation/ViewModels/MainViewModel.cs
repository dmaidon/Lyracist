// Edited on Sep 21, 2026 @ 11:58:45 -> Add RoundEstimateNoticeText and RecalculateRoundEstimation for real-time round duration and ETA tracking
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KSRotation.Models;
using KSRotation.Services;
using Lyracist.Shared;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Windows.Threading;

namespace KSRotation.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DisplayWindowService _displayWindowService = new();
        private readonly DjBannerWindowService _djBannerWindowService = new();
        private readonly List<SingerEntry> _subscribedSingers = [];
        private readonly DispatcherTimer _saveDebounceTimer;
        private readonly DispatcherTimer _roundEstimateTimer;
        private RoundEstimationInfo _roundEstimation = new();

        /// <summary>Fired when this device's active session is exported/handed off to another peer on the LAN.</summary>
        public event Action<string, int, string>? SessionHandedOffToPeer;
        private readonly DispatcherTimer _dbDebounceTimer;
        private readonly DispatcherTimer _jsonCacheDebounceTimer;
        private readonly DispatcherTimer _requestsJsonCacheDebounceTimer;
        private readonly DispatcherTimer _connectBannerDebounceTimer;
        private readonly List<SongPerformance> _performanceHistory = [];
        private readonly Lock _performanceHistoryLock = new();
        private readonly Dictionary<SingerEntry, string> _lastSingerNames = [];
        private readonly bool _isInitializing;
        private bool _isFinishingSong;
        private bool _djBannerWasAutoDisabled;
        private bool _isAutoDisablingDjBanner;
        private readonly Random _random = new();
        private PatronRequestServer? _requestServer;
        private int _activeServerPort = ServerPort;
        private const int ServerPort = 5000;

        // Snapshot serialized on the UI thread; read lock-free by the TCP server thread.
        private volatile string _cachedRotationJson = "[]";
        private volatile string _cachedRequestsJson = "[]";

        private static readonly (string Song, string Artist)[] TestPool =
        [
            ("Billie Jean", "Michael Jackson"),
            ("Hotel California", "Eagles"),
            ("Hey Jude", "The Beatles"),
            ("Stayin' Alive", "Bee Gees"),
            ("Take On Me", "A-ha"),
            ("Sweet Child O' Mine", "Guns N' Roses"),
            ("Smells Like Teen Spirit", "Nirvana"),
            ("Bad Romance", "Lady Gaga"),
            ("Uptown Funk", "Mark Ronson ft. Bruno Mars"),
            ("Thriller", "Michael Jackson"),
            ("Wonderwall", "Oasis"),
            ("Bohemian Rhapsody", "Queen"),
            ("Karma Chameleon", "Culture Club"),
            ("Careless Whisper", "George Michael"),
            ("Eye of the Tiger", "Survivor"),
            ("Purple Rain", "Prince"),
            ("Beat It", "Michael Jackson"),
            ("All Star", "Smash Mouth"),
            ("Toxic", "Britney Spears"),
            ("Single Ladies", "Beyoncé"),
            ("Rolling in the Deep", "Adele"),
            ("I Will Survive", "Gloria Gaynor"),
            ("Dancing Queen", "ABBA"),
            ("Livin' on a Prayer", "Bon Jovi"),
            ("Don't Stop Believin'", "Journey"),
            ("Like a Prayer", "Madonna"),
            ("I Wanna Dance with Somebody", "Whitney Houston"),
            ("I'll Leave This World Loving You","Ricky Van Shelton")
        ];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDjBannerAvailable))]
        public partial bool IsDisplayEnabled { get; set; }

        // The rotation display and the DJ banner will typically be shown on the same physical
        // screen, so the DJ banner is unavailable while the rotation display is on-screen.
        public bool IsDjBannerAvailable => !IsDisplayEnabled;

        [ObservableProperty]
        public partial bool IsTestMode { get; set; }

        [ObservableProperty]
        public partial bool BlockDuplicateSongsInSession { get; set; }

        [ObservableProperty]
        public partial bool EnableSessionSchedule { get; set; }

        [ObservableProperty]
        public partial string SessionStartTime { get; set; } = "8:00 PM";

        [ObservableProperty]
        public partial string SessionStopTime { get; set; } = "2:00 AM";

        [ObservableProperty]
        public partial bool EnableLastRequestTime { get; set; }

        [ObservableProperty]
        public partial string LastRequestTime { get; set; } = "1:30 AM";

        public IReadOnlyList<string> TimeOptions => SessionScheduleHelper.StandardTimeOptions;

        public bool IsRequestSubmissionAllowed(out string reason, DateTime? now = null)
        {
            return SessionScheduleHelper.IsRequestSubmissionAllowed(
                EnableSessionSchedule,
                SessionStartTime,
                SessionStopTime,
                EnableLastRequestTime,
                LastRequestTime,
                out reason,
                now);
        }

        public bool IsSongInCurrentSession(string songTitle, string artist = "")
        {
            if (string.IsNullOrWhiteSpace(songTitle)) return false;
            string cleanTitle = songTitle.Trim();
            string cleanArtist = artist?.Trim() ?? string.Empty;

            lock (_performanceHistoryLock)
            {
                if (_performanceHistory.Any(p => RotationHelpers.IsSameSongLenient(p.SongTitle, p.ArtistName, cleanTitle, cleanArtist)))
                {
                    return true;
                }
            }

            // Singers is an ObservableCollection mutated only on the UI thread (Dispatcher.BeginInvoke
            // everywhere else in this app), but this method is invoked directly from PatronRequestServer's
            // background request-handling thread via the onCheckDuplicateSong delegate, which can race
            // with a UI-thread Add/Remove/Move mid-enumeration. Fails open (treats it as not a duplicate)
            // rather than blocking - see ReadWithConcurrentRetry's own comment for why Dispatcher.Invoke
            // isn't used here.
            bool queuedMatch = RotationHelpers.ReadWithConcurrentRetry(
                () => Singers.Any(s => RotationHelpers.IsSameSongLenient(s.Song, s.Artist, cleanTitle, cleanArtist)),
                fallback: false);
            if (queuedMatch)
            {
                return true;
            }

            return false;
        }

        /// <summary>When true, indicates the final round of the night is underway.</summary>
        [ObservableProperty]
        public partial bool IsLastRound { get; set; }

        partial void OnIsLastRoundChanged(bool value)
        {
            if (_isInitializing) return;

            if (value)
            {
                foreach (var s in Singers)
                {
                    s.HasSungInLastRound = false;
                }
            }
            UpdateNextSingerHighlight();
            RefreshBillboardState();
            RebuildRotationJsonCacheNow();
            _displayWindowService.SetLastRound(value);
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
            RecalculateRoundEstimation();
        }

        [RelayCommand]
        public void ToggleLastRound()
        {
            IsLastRound = !IsLastRound;
        }

        /// <summary>When true, the current singer always floats to index 0 of the active rotation list.</summary>
        [ObservableProperty]
        public partial bool FloatCurrentSingerToTop { get; set; } = true;

        /// <summary>Default estimated song length in minutes, used by the rotation-screen "estimated
        /// wait time" badge whenever a queued song's actual duration isn't known/resolvable.</summary>
        [ObservableProperty]
        public partial double DefaultSongLengthMinutes { get; set; } = 4.75;

        partial void OnDefaultSongLengthMinutesChanged(double value)
        {
            if (_isInitializing) return;
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: value * 60.0, enabled: ShowEstimatedWaitTime);
            RecalculateRoundEstimation();
            QueueSaveSettings();
        }

        /// <summary>Whether the rotation-screen "estimated wait time" badge is shown at all. Some
        /// DJs prefer not to display wait estimates to the audience; defaults on.</summary>
        [ObservableProperty]
        public partial bool ShowEstimatedWaitTime { get; set; } = true;

        /// <summary>When true, this device automatically switches to the in-app Remote DJ view (dj.html) when its session is transferred to or pulled by a peer.</summary>
        [ObservableProperty]
        public partial bool AutoSwitchToRemoteDjOnHandoff { get; set; } = true;

        partial void OnAutoSwitchToRemoteDjOnHandoffChanged(bool value)
        {
            if (_isInitializing) return;
            QueueSaveSettings();
        }

        partial void OnShowEstimatedWaitTimeChanged(bool value)
        {
            if (_isInitializing) return;
            _displayWindowService.SetShowEstimatedWaitTime(value);
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: value);
            RefreshBillboardState();
            RebuildRotationJsonCacheNow();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
            QueueSaveSettings();
        }

        partial void OnFloatCurrentSingerToTopChanged(bool value)
        {
            if (_isInitializing) return;

            if (value)
            {
                var current = RotationHelpers.GetCurrentSinger(Singers);
                if (current == null)
                {
                    var first = Singers.FirstOrDefault(s => s.IsRotationStart && !s.IsInactive && !s.IsPaused && !s.IsSkipped)
                                ?? Singers.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped);
                    if (first != null)
                    {
                        RotationHelpers.SetCurrentSinger(Singers, first, floatCurrentToTop: true, isLastRound: IsLastRound);
                    }
                }
                else
                {
                    RotationHelpers.FloatCurrentSingerToTop(Singers);
                }
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
                if (IsDisplayEnabled)
                {
                    _displayWindowService.Update(Singers);
                }
            }
            QueueSaveSettings();
        }

        public DisplayTarget[] AvailableDisplayTargets { get; } = Enum.GetValues<DisplayTarget>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsChromecastSelectionVisible))]
        public partial DisplayTarget RotationTarget { get; set; } = DisplayTarget.Monitor;

        [ObservableProperty]
        public partial ObservableCollection<ChromecastDevice> AvailableChromecasts { get; set; } = [];

        [ObservableProperty]
        public partial ChromecastDevice? SelectedChromecast { get; set; }

        public bool IsChromecastSelectionVisible => RotationTarget == DisplayTarget.Chromecast;

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (_isInitializing) return;

            switch (e.PropertyName)
            {
                case nameof(IsTestMode):
                    if (!IsTestMode)
                    {
                        Singers.Clear();
                        _performanceHistory.Clear();
                        SaveDatabaseNow();
                    }
                    break;

                case nameof(IsDisplayEnabled):
                    if (IsDisplayEnabled)
                    {
                        // Re-scan connected monitors right before using one, so a display plugged
                        // in after the app launched (e.g. an HDMI projector connected mid-show)
                        // is picked up without the user having to know to hit "Refresh" first.
                        RefreshAvailableMonitors();

                        if (IsDjBannerEnabled)
                        {
                            _isAutoDisablingDjBanner = true;
                            try
                            {
                                IsDjBannerEnabled = false;
                                _djBannerWasAutoDisabled = true;
                            }
                            finally
                            {
                                _isAutoDisablingDjBanner = false;
                            }
                        }
                        // Enabling the display window only ever shows it locally on
                        // SelectedMonitorDevice. It must NOT also start casting to whatever
                        // target happens to be selected on the Display tab - actual casting
                        // (Miracast, Chromecast, etc.) only starts when the user explicitly
                        // clicks "Cast Rotation".
                        _displayWindowService.SetConnectionInfo(ConnectionUrl, QrCodeImage);
                        _displayWindowService.SetWatermarkOpacity(WatermarkOpacity);
                        _displayWindowService.SetSelectedMonitor(SelectedMonitorDevice);
                        _displayWindowService.SelectedDevice = SelectedChromecast;
                        _displayWindowService.Show(Singers);
                        _displayWindowService.SetProjectionView(SelectedProjectionView);
                        _displayWindowService.SetBannerText(BannerText, VenueName, DjName);
                        _displayWindowService.SetCrawlBannerText(CrawlBannerText, VenueName, DjName);
                    }
                    else
                    {
                        _displayWindowService.Hide();
                        CheckRestoreDjBanner();
                    }
#if !MAUI
                    CheckAndSyncTriviaPause();
#endif
                    QueueSaveSettings();
                    break;

                case nameof(SelectedChromecast):
                    _displayWindowService.SelectedDevice = SelectedChromecast;
                    QueueSaveSettings();
                    break;

                case nameof(RotationTarget):
                    // Just remembers which target "Cast Rotation" will use next - selecting a
                    // target here must not itself start casting.
                    OnPropertyChanged(nameof(IsChromecastSelectionVisible));
                    if (RotationTarget == DisplayTarget.Chromecast)
                    {
                        _ = DiscoverChromecastsAsync();
                    }
                    QueueSaveSettings();
                    break;

                case nameof(SelectedMonitorDevice):
                    _displayWindowService.SetSelectedMonitor(SelectedMonitorDevice);
                    if (IsDisplayEnabled)
                    {
                        _displayWindowService.RepositionWindow();
                    }
#if !MAUI
                    PositionTriviaDisplayWindow(SelectedMonitorDevice);
#endif
                    QueueSaveSettings();
                    break;

                case nameof(IsDjBannerQrCodeEnabled):
                    QueueSaveSettings();
                    break;

                case nameof(ShowQrCodeOnRotationScreen):
                    _displayWindowService.SetShowQrCode(ShowQrCodeOnRotationScreen);
                    QueueSaveSettings();
                    break;

                case nameof(AutoAcceptRequests):
                    if (AutoAcceptRequests)
                    {
                        AcceptAllPendingRequests();
                    }
                    QueueSaveSettings();
                    break;

                case nameof(IsDjBannerEnabled):
                    if (IsDjBannerEnabled)
                    {
                        if (IsDisplayEnabled)
                        {
                            _isAutoDisablingDjBanner = true;
                            try
                            {
                                IsDjBannerEnabled = false;
                                _djBannerWasAutoDisabled = true;
                            }
                            finally
                            {
                                _isAutoDisablingDjBanner = false;
                            }
                            break;
                        }
                        _djBannerWasAutoDisabled = false;
                        _djBannerWindowService.SetSelectedMonitor(DjBannerMonitorDevice);
                        _djBannerWindowService.SetBannerPath(ResolveActiveBannerPath());
                        _djBannerWindowService.Show(this);
                    }
                    else
                    {
                        if (!_isAutoDisablingDjBanner)
                        {
                            _djBannerWasAutoDisabled = false;
                        }
                        _djBannerWindowService.Hide();
                    }
#if !MAUI
                    CheckAndSyncTriviaPause();
#endif
                    QueueSaveSettings();
                    break;

                case nameof(DjBannerMonitorDevice):
                    _djBannerWindowService.SetSelectedMonitor(DjBannerMonitorDevice);
                    if (IsDjBannerEnabled)
                    {
                        _djBannerWindowService.RepositionWindow();
                    }
                    QueueSaveSettings();
                    break;

                case nameof(SelectedDjBannerPath):
                    UpdateDjBannerPath();
                    QueueSaveSettings();
                    RebuildRotationJsonCacheNow();
                    break;

                case nameof(ActiveSpecialEvent):
                    SyncSpecialEventOptions(ActiveSpecialEvent);
                    UpdateDjBannerPath();
                    UpdateLastSongState();
                    QueueSaveSettings();
                    RebuildRotationJsonCacheNow();
#if !MAUI
                    CheckAndSyncTriviaPause();
#endif
                    break;

                case nameof(SelectedProjectionView):
                    _displayWindowService.SetProjectionView(SelectedProjectionView);
                    QueueSaveSettings();
                    break;

                case nameof(WatermarkOpacity):
                    _displayWindowService.SetWatermarkOpacity(WatermarkOpacity);
                    QueueSaveSettings();
                    break;

                case nameof(VenueName):
                case nameof(DjName):
                    _displayWindowService.SetBannerText(BannerText, VenueName, DjName);
                    _displayWindowService.SetCrawlBannerText(CrawlBannerText, VenueName, DjName);
                    QueueSaveSettings();
                    break;

                case nameof(ListDjAndVenueOnBillboard):
                    QueueSaveSettings();
                    break;

                case nameof(SelectedVenue):
                    if (!string.IsNullOrWhiteSpace(SelectedVenue))
                    {
                        VenueName = SelectedVenue.Trim();
                    }
                    break;

                case nameof(SelectedDj):
                    if (!string.IsNullOrWhiteSpace(SelectedDj))
                    {
                        DjName = SelectedDj.Trim();
                    }
                    break;

                case nameof(SelectedTheme):
                    string normalizedTheme = NormalizeTheme(SelectedTheme);
                    if (!string.Equals(SelectedTheme, normalizedTheme, System.StringComparison.Ordinal))
                    {
                        SelectedTheme = normalizedTheme;
                    }
                    else
                    {
                        ThemeService.Apply(normalizedTheme);
                        QueueSaveSettings();
                    }
                    break;

                case nameof(BannerText):
                    _displayWindowService.SetBannerText(BannerText, VenueName, DjName);
                    QueueSaveSettings();
                    break;

                case nameof(CrawlBannerText):
                    _displayWindowService.SetCrawlBannerText(CrawlBannerText, VenueName, DjName);
                    QueueSaveSettings();
                    break;

                case nameof(MarqueeSpeed):
                    _displayWindowService.SetMarqueeSpeed(MarqueeSpeed);
                    QueueSaveSettings();
                    break;

                case nameof(DefaultSongLengthMinutes):
                    QueueSaveSettings();
                    break;

                case nameof(BlockDuplicateSongsInSession):
                case nameof(EnableSessionSchedule):
                case nameof(SessionStartTime):
                case nameof(SessionStopTime):
                case nameof(EnableLastRequestTime):
                case nameof(LastRequestTime):
                    QueueSaveSettings();
                    break;

                case nameof(PreferredHostIp):
                    string normalizedPreferredHostIp = PreferredHostIp?.Trim() ?? string.Empty;
                    if (!string.Equals(PreferredHostIp, normalizedPreferredHostIp, System.StringComparison.Ordinal))
                    {
                        PreferredHostIp = normalizedPreferredHostIp;
                    }
                    else
                    {
                        RefreshConnectionInfo();
                        QueueSaveSettings();
                    }
                    break;
            }
        }

        [ObservableProperty]
        public partial bool ShowFullRotation { get; set; }

        public ObservableCollection<string> ProjectionViews { get; } =
        [
            "Normal List",
            "Star Wars Crawl",
            "Vegas Marquee",
            "Vinyl Turntable",
            "Disco Ball",
            "Synthwave Grid"
            // TODO (future): "Jumbotron" — full-bleed stadium scoreboard style with huge singer name
            //                on a bright LED matrix background, scrolling ticker at the bottom.
            // TODO (future): "Neon Bar Sign" — dark brick-wall backdrop with a glowing neon-tube
            //                sign rendering the singer name in flickering neon colors.
        ];

        [ObservableProperty]
        public partial string SelectedProjectionView { get; set; } = "Normal List";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
#if !MAUI
        [NotifyPropertyChangedFor(nameof(TriviaVenueName))]
#endif
        public partial string VenueName { get; set; } = "Karaoke Night";

        [ObservableProperty]
        public partial string? SelectedVenue { get; set; }

        [ObservableProperty]
        public partial string NewVenueName { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
#if !MAUI
        [NotifyPropertyChangedFor(nameof(TriviaHostName))]
#endif
        public partial string DjName { get; set; } = "Guest DJ";

        [ObservableProperty]
        public partial bool ListDjAndVenueOnBillboard { get; set; } = true;

        [ObservableProperty]
        public partial string? SelectedDj { get; set; }

        [ObservableProperty]
        public partial string NewDjName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedTheme { get; set; } = "System";

        [ObservableProperty]
        public partial string BannerText { get; set; } = "Welcome to Karaoke Night!";

        [ObservableProperty]
        public partial string CrawlBannerText { get; set; } = AppSettings.DefaultCrawlBannerText;

        [ObservableProperty]
        public partial double MarqueeSpeed { get; set; } = 60;

        [ObservableProperty]
        public partial string EmailRecipient { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool SendEmailOnSave { get; set; }

        [ObservableProperty]
        public partial double WatermarkOpacity { get; set; } = 0.06;

        private string _preferredHostIp = string.Empty;

        public string PreferredHostIp
        {
            get => _preferredHostIp;
            set => SetProperty(ref _preferredHostIp, value);
        }

        public ObservableCollection<MonitorItem> AvailableMonitors { get; } = [];
        public ObservableCollection<MonitorItem> AvailableMonitorsWithAll { get; } = [];

        [ObservableProperty]
        public partial string SelectedMonitorDevice { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DjBannerMonitorDevice { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ConnectInstructionsScreen { get; set; } = "All Screens / Monitors";

        partial void OnConnectInstructionsScreenChanged(string value)
        {
            QueueSaveSettings();
            RefreshConnectInstructionsBanner();
        }

        [ObservableProperty]
        public partial string SelectedDjBannerPath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsDjBannerEnabled { get; set; } = false;

        [ObservableProperty]
        public partial string ActiveSpecialEvent { get; set; } = "None";

        public ObservableCollection<Lyracist.Shared.SpecialEventConfig> SpecialEvents { get; } = [];
        public ObservableCollection<string> AvailableEventBannerFiles { get; } = [];
        public ObservableCollection<SpecialEventOptionViewModel> SpecialEventOptions { get; } = [];

        [ObservableProperty]
        public partial bool IsDjBannerQrCodeEnabled { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowQrCodeOnRotationScreen { get; set; } = true;

        [ObservableProperty]
        public partial string WifiPassword { get; set; } = string.Empty;

        partial void OnWifiPasswordChanged(string value)
        {
            // Skip during startup load — ConnectionUrl isn't set until StartRequestServer() runs
            // later in the constructor, which does its own banner refresh with the real URL. Refreshing
            // here too would just write a "localhost" placeholder that gets overwritten a moment later.
            if (_isInitializing) return;

            string? ssid = WifiHelper.GetConnectedSsid();
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                WifiPasswordStore.SetPasswordForSsid(ssid, value);
            }
            RefreshConnectInstructionsBanner();
            UpdateDjBannerPath();
        }

// Edited on Aug 10, 2026 @ 12:56:00 -> Guard WPF banner rendering and monitor enumeration in MainViewModel.cs with #if !MAUI for MAUI build compatibility
        public void RefreshConnectInstructionsBanner()
        {
#if !MAUI
            try
            {
                var (w, h) = GetTargetScreenResolution(ConnectInstructionsScreen);
                DjBannerFileManager.CreateConnectInstructionsBannerPng(
                    System.IO.Path.Combine(Globals.EventBannersDir, "ConnectInstructions.png"),
                    WifiHelper.GetConnectedSsid() ?? string.Empty,
                    WifiPassword,
                    ConnectionUrl,
                    w, h);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.RefreshConnectInstructionsBanner", ex);
            }
#endif
        }

        // Debounces banner regeneration (QR render + PNG encode + disk write, up to 3840x2160) so it
        // doesn't run synchronously on every keystroke of a bound text field (e.g. PreferredHostIp).
        private void QueueRefreshConnectInstructionsBanner()
        {
            _connectBannerDebounceTimer.Stop();
            _connectBannerDebounceTimer.Start();
        }

        private static (int Width, int Height) GetTargetScreenResolution(string screenSelection)
        {
#if !MAUI
            try
            {
                if (string.IsNullOrWhiteSpace(screenSelection) || screenSelection.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    return (1920, 1080);
                }

                var monitors = MonitorEnumerator.GetMonitors();
                if (screenSelection.Equals("All Screens / Monitors", StringComparison.OrdinalIgnoreCase))
                {
                    return (1920, 1080);
                }

                foreach (var m in monitors)
                {
                    if (string.Equals(m.DeviceName, screenSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        return (m.Width, m.Height);
                    }
                }
            }
            catch
            {
                // Ignore monitor enumeration errors
            }
#else
            _ = screenSelection;
#endif
            return (1920, 1080);
        }

#if !MAUI
        [RelayCommand]
        private void LaunchTrivia()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = System.IO.Path.Combine(baseDir, "Lyracist.Trivia.exe");

                if (!System.IO.File.Exists(exePath))
                {
                    string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "Lyracist.Trivia", "bin", "Debug", "net9.0-windows", "Lyracist.Trivia.exe"));
                    if (System.IO.File.Exists(devPath))
                    {
                        exePath = devPath;
                    }
                    else
                    {
                        exePath = "Lyracist.Trivia.exe";
                    }
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(exePath) ?? baseDir
                });
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.LaunchTrivia", ex);
            }
        }
#endif

        [RelayCommand]
        private void PreviewConnectInstructions()
        {
            try
            {
                string bannerPath = System.IO.Path.Combine(Globals.EventBannersDir, "ConnectInstructions.png");
                if (System.IO.File.Exists(bannerPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(bannerPath) { UseShellExecute = true });
                }
                else
                {
                    RefreshConnectInstructionsBanner();
                    if (System.IO.File.Exists(bannerPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(bannerPath) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.PreviewConnectInstructions", ex);
            }
        }

        #region Billboard View Properties
        public SingerEntry? CurrentSinger => RotationHelpers.GetCurrentSinger(Singers);
        public SingerEntry? NextSinger => Singers.FirstOrDefault(s => s.IsNext && (!IsLastRound || !s.HasSungInLastRound));
        public List<SingerEntry> UpcomingSingers => Singers.Where(s => !s.IsInactive && !s.IsCurrent && !s.IsNext && (!IsLastRound || !s.HasSungInLastRound)).ToList();
        public bool HasCurrentSinger => CurrentSinger != null;
        public bool HasNextSinger => NextSinger != null;
        public bool HasUpcomingSingers => UpcomingSingers.Count > 0;
        public string ActiveSingerCountText => $"{Singers.Count(s => !s.IsInactive && (!IsLastRound || !s.HasSungInLastRound))} Singers";
        public string CurrentTimeString => DateTime.Now.ToString("h:mm tt");
        public string WifiSsidDisplay => !string.IsNullOrWhiteSpace(WifiHelper.GetConnectedSsid()) ? WifiHelper.GetConnectedSsid()! : "DJ Travel Router";
        public string WifiPasswordDisplay => !string.IsNullOrWhiteSpace(WifiPasswordStore.GetPasswordForSsid(WifiSsidDisplay)) ? WifiPasswordStore.GetPasswordForSsid(WifiSsidDisplay) : "None";

        public string CurrentSingerNameDisplay => CurrentSinger?.DisplayNameWithDuet ?? "No Singer Performing";
        public string CurrentSingerSongDisplay => !string.IsNullOrWhiteSpace(CurrentSinger?.Song) ? CurrentSinger.Song : "Queue is Ready";
        public string CurrentSingerArtistDisplay => !string.IsNullOrWhiteSpace(CurrentSinger?.Artist) ? CurrentSinger.Artist : string.Empty;

        public string NextSingerNameDisplay => NextSinger?.DisplayNameWithDuet ?? "Next Singer TBA";
        public string NextSingerSongDisplay => !string.IsNullOrWhiteSpace(NextSinger?.Song) ? NextSinger.Song : string.Empty;
        public string NextSingerArtistDisplay => !string.IsNullOrWhiteSpace(NextSinger?.Artist) ? NextSinger.Artist : string.Empty;

        public bool CurrentSingerIsRotationStart => CurrentSinger?.IsRotationStart ?? false;
        public bool NextSingerIsRotationStart => NextSinger?.IsRotationStart ?? false;
        public bool CurrentSingerIsSpecial => CurrentSinger?.IsSpecial ?? false;
        public bool NextSingerIsSpecial => NextSinger?.IsSpecial ?? false;

        public void RefreshBillboardState()
        {
            OnPropertyChanged(nameof(IsLastRound));
            OnPropertyChanged(nameof(CurrentSinger));
            OnPropertyChanged(nameof(NextSinger));
            OnPropertyChanged(nameof(UpcomingSingers));
            OnPropertyChanged(nameof(HasCurrentSinger));
            OnPropertyChanged(nameof(HasNextSinger));
            OnPropertyChanged(nameof(HasUpcomingSingers));
            OnPropertyChanged(nameof(ActiveSingerCountText));
            OnPropertyChanged(nameof(CurrentTimeString));
            OnPropertyChanged(nameof(WifiSsidDisplay));
            OnPropertyChanged(nameof(WifiPasswordDisplay));
            OnPropertyChanged(nameof(CurrentSingerNameDisplay));
            OnPropertyChanged(nameof(CurrentSingerSongDisplay));
            OnPropertyChanged(nameof(CurrentSingerArtistDisplay));
            OnPropertyChanged(nameof(NextSingerNameDisplay));
            OnPropertyChanged(nameof(NextSingerSongDisplay));
            OnPropertyChanged(nameof(NextSingerArtistDisplay));
            OnPropertyChanged(nameof(CurrentSingerIsRotationStart));
            OnPropertyChanged(nameof(NextSingerIsRotationStart));
            OnPropertyChanged(nameof(CurrentSingerIsSpecial));
            OnPropertyChanged(nameof(NextSingerIsSpecial));
        }
        #endregion

        public ObservableCollection<DjBannerItem> AvailableDjBanners { get; } = [];

        [ObservableProperty]
        public partial string SelectedHelpTopic { get; set; } = "🚀 Getting Started";

        public List<string> HelpTopics { get; } =
        [
            "🚀 Getting Started",
            "🎤 Rotation Management",
            "🔄 Device Switching & Live Handoff",
            "📺 Display Projection",
            "⚙️ Settings & Venues",
            "📺 Display & DJ Banners",
            "📡 Connect & Wi-Fi Instructions",
            "🎯 Trivia Night Pro",
            "❓ FAQ & Shortcuts",
        ];

        public ObservableCollection<string> Themes { get; } =
        [
            "Light",
            "Dark",
            "System",
        ];

        public ObservableCollection<string> Venues { get; } = [];

        public ObservableCollection<string> Djs { get; } = [];

        public ObservableCollection<SingerEntry> Singers { get; } = [];

        /// <summary>Count of active karaoke singers in the rotation, excluding background music entries (<see cref="SingerEntry.IsMusic"/>) and inactive singers (<see cref="SingerEntry.IsInactive"/>).</summary>
        public int SingersInRotationCount => Singers.Count(s => !s.IsMusic && !s.IsInactive);

        /// <summary>Summary notice of remaining round duration, performer counts, and estimated completion time (ETA).</summary>
        public string RoundEstimateNoticeText => _roundEstimation.SummaryText;

        /// <summary>Short summary notice of remaining round duration and ETA.</summary>
        public string RoundEstimateShortNoticeText => _roundEstimation.ShortSummaryText;

        /// <summary>Performers remaining in the current active round.</summary>
        public int RoundRemainingPerformersCount => _roundEstimation.PerformersRemaining;

        /// <summary>Estimated minutes remaining in the current round.</summary>
        public int RoundRemainingMinutes => _roundEstimation.RemainingMinutes;

        /// <summary>Recalculates the round estimation and notifies bindings.</summary>
        public void RecalculateRoundEstimation()
        {
            _roundEstimation = RotationHelpers.CalculateRoundEstimation(
                Singers,
                isLastRound: IsLastRound,
                defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0);

            OnPropertyChanged(nameof(RoundEstimateNoticeText));
            OnPropertyChanged(nameof(RoundEstimateShortNoticeText));
            OnPropertyChanged(nameof(RoundRemainingPerformersCount));
            OnPropertyChanged(nameof(RoundRemainingMinutes));
        }

        /// <summary>Whether "Load Test Data" is safe to use — false once a real, in-progress queue exists, so an accidental tap can't wipe it.</summary>
        public bool CanLoadTestData => Singers.Count == 0;

        public ObservableCollection<string> KnownSingers { get; } = [];
        public ObservableCollection<string> FilteredSingers { get; } = [];

        // ── Assembly Info Properties ──────────────────────────────────────────
        // Resolved once via reflection and cached; these values never change for the app's lifetime,
        // so the About-tab bindings don't re-walk custom attributes on every read.
        private static readonly string s_appTitle = ResolveAppTitle();

        private static readonly string s_appVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        private const string s_appCompany = Lyracist.Shared.Globals.CompanyName;
        private static readonly string s_appCopyright = Lyracist.Shared.Globals.Copyright;
        private static readonly string s_appAuthor = ResolveAppAuthor(s_appCompany);

        public string AppTitle => s_appTitle;
        public string WindowTitle => AppTitle;
        public string AppVersion => s_appVersion;
        public string AppCompany => s_appCompany;
        public string AppCopyright => s_appCopyright;
        public string AppAuthor => s_appAuthor;

        private static string ResolveAppTitle()
        {
            var product = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
            if (!string.IsNullOrEmpty(product) && !string.Equals(product, "KSRotation", StringComparison.OrdinalIgnoreCase))
            {
                return product;
            }
            var title = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
            if (!string.IsNullOrEmpty(title) && !string.Equals(title, "KSRotation", StringComparison.OrdinalIgnoreCase))
            {
                return title;
            }
            return "Karaoke Singer Rotation";
        }

        private static string ResolveAppAuthor(string company)
        {
            var metadata = typeof(MainViewModel).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            var author = metadata.FirstOrDefault(m => string.Equals(m.Key, "Authors", StringComparison.OrdinalIgnoreCase))?.Value;
            if (!string.IsNullOrEmpty(author)) return author;

            if (company == "PAROLE Software") return "Dennis Maidon";
            return company;
        }

        // The XAML designer instantiates this ViewModel just to render MainWindow.xaml's design-time
        // preview (Window.DataContext is set directly in XAML) — with no guard, merely opening the file
        // in Visual Studio's designer would write settings/banner files to disk, open a real TCP
        // listener, and potentially spawn the DJ Banner window, none of which should happen unless the
        // app is actually run.
#if !MAUI
        private static bool IsInDesignMode =>
            System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject());
#else
        private static bool IsInDesignMode => false;
#endif

        // WPF's MessageBox.Show blocks and returns the user's real answer, so it's safe to wrap
        // synchronously. MAUI's DisplayAlert is inherently async, so ConfirmAsync must genuinely be
        // awaited there — see WpfShims.MessageBox.ShowConfirmAsync for why Show() alone can't be trusted.
#if !MAUI
        private static Task<bool> ConfirmAsync(string message, string caption) =>
            Task.FromResult(System.Windows.MessageBox.Show(message, caption, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes);
#else
        private static Task<bool> ConfirmAsync(string message, string caption) =>
            System.Windows.MessageBox.ShowConfirmAsync(message, caption);
#endif

        public MainViewModel()
        {
            _saveDebounceTimer = new DispatcherTimer();
            _dbDebounceTimer = new DispatcherTimer();
            _jsonCacheDebounceTimer = new DispatcherTimer();
            _requestsJsonCacheDebounceTimer = new DispatcherTimer();
            _connectBannerDebounceTimer = new DispatcherTimer();
            _roundEstimateTimer = new DispatcherTimer();

            if (IsInDesignMode)
            {
                return;
            }

            _isInitializing = true;
            LoggerService.CleanupLogs();
            _saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _saveDebounceTimer.Tick += (s, e) =>
            {
                _saveDebounceTimer.Stop();
                SaveSettingsNow();
            };

            _dbDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _dbDebounceTimer.Tick += (s, e) =>
            {
                _dbDebounceTimer.Stop();
                SaveDatabaseNow();
            };

            _jsonCacheDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _jsonCacheDebounceTimer.Tick += (s, e) =>
            {
                _jsonCacheDebounceTimer.Stop();
                RebuildRotationJsonCacheNow();
            };

            _requestsJsonCacheDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _requestsJsonCacheDebounceTimer.Tick += (s, e) =>
            {
                _requestsJsonCacheDebounceTimer.Stop();
                RebuildRequestsJsonCacheNow();
            };

            _connectBannerDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _connectBannerDebounceTimer.Tick += (s, e) =>
            {
                _connectBannerDebounceTimer.Stop();
                RefreshConnectInstructionsBanner();
            };

            _roundEstimateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _roundEstimateTimer.Tick += (s, e) =>
            {
                RecalculateRoundEstimation();
            };
            _roundEstimateTimer.Start();

            Singers.CollectionChanged += OnSingersCollectionChanged;
            // All IncomingRequests mutations happen on the UI thread (see HandleRequestReceived/HandleDjAction),
            // so rebuilding the cache here is thread-safe; GetRequestsJson then reads the cache lock-free.
            // Debounced the same way as the rotation cache above - a burst of requests arriving in
            // the same moment (e.g. several patrons submitting near-simultaneously) would otherwise
            // re-serialize the whole list once per addition instead of once per burst.
            IncomingRequests.CollectionChanged += (_, _) =>
            {
                _requestsJsonCacheDebounceTimer.Stop();
                _requestsJsonCacheDebounceTimer.Start();
            };

            foreach (string venue in VenueService.Load())
            {
                Venues.Add(venue);
            }

            foreach (string dj in DjService.Load())
            {
                Djs.Add(dj);
            }

            AppSettings settings;
            try
            {
                settings = SettingsService.Load();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.Constructor.LoadSettings", ex);
                settings = new AppSettings();
            }

            VenueName = string.IsNullOrWhiteSpace(settings.VenueName) ? "Karaoke Night" : settings.VenueName.Trim();
            SelectedVenue = Venues.FirstOrDefault(v => string.Equals(v, VenueName, System.StringComparison.OrdinalIgnoreCase))
                            ?? Venues.FirstOrDefault();

            DjName = string.IsNullOrWhiteSpace(settings.DjName) ? "Guest DJ" : settings.DjName.Trim();
            SelectedDj = Djs.FirstOrDefault(d => string.Equals(d, DjName, System.StringComparison.OrdinalIgnoreCase))
                         ?? Djs.FirstOrDefault();
            ListDjAndVenueOnBillboard = settings.ListDjAndVenueOnBillboard;

            SelectedTheme = NormalizeTheme(settings.Theme);
            BannerText = string.IsNullOrWhiteSpace(settings.BannerText)
                ? "Welcome to Karaoke Night!"
                : settings.BannerText.Trim();

            CrawlBannerText = string.IsNullOrWhiteSpace(settings.CrawlBannerText)
                ? AppSettings.DefaultCrawlBannerText
                : settings.CrawlBannerText.Trim();

            MarqueeSpeed = Math.Clamp(settings.MarqueeSpeed, 20, 200);
            IsTestMode = settings.IsTestMode;
            EmailRecipient = settings.EmailRecipient ?? string.Empty;
            SendEmailOnSave = settings.SendEmailOnSave;
            WatermarkOpacity = Math.Clamp(settings.WatermarkOpacity, 0.0, 1.0);
            PreferredHostIp = string.IsNullOrWhiteSpace(settings.PreferredHostIp)
                ? string.Empty
                : settings.PreferredHostIp.Trim();
            DjPin = string.IsNullOrWhiteSpace(settings.DjPin)
                ? string.Empty
                : settings.DjPin.Trim();
            AutoAcceptRequests = settings.AutoAcceptRequests;
            FloatCurrentSingerToTop = settings.FloatCurrentSingerToTop;
            DefaultSongLengthMinutes = Math.Clamp(settings.DefaultSongLengthMinutes > 0 ? settings.DefaultSongLengthMinutes : 4.75, 1, 20);
            ShowEstimatedWaitTime = settings.ShowEstimatedWaitTime;
            BlockDuplicateSongsInSession = settings.BlockDuplicateSongsInSession;
            EnableSessionSchedule = settings.EnableSessionSchedule;
            SessionStartTime = string.IsNullOrWhiteSpace(settings.SessionStartTime) ? "8:00 PM" : settings.SessionStartTime;
            SessionStopTime = string.IsNullOrWhiteSpace(settings.SessionStopTime) ? "2:00 AM" : settings.SessionStopTime;
            EnableLastRequestTime = settings.EnableLastRequestTime;
            LastRequestTime = string.IsNullOrWhiteSpace(settings.LastRequestTime) ? "1:30 AM" : settings.LastRequestTime;
            AutoSwitchToRemoteDjOnHandoff = settings.AutoSwitchToRemoteDjOnHandoff;
            _displayWindowService.SetShowEstimatedWaitTime(ShowEstimatedWaitTime);
            _displayWindowService.SetWatermarkOpacity(WatermarkOpacity);
            SelectedProjectionView = "Normal List";
            _displayWindowService.SetBannerText(BannerText, VenueName, DjName);
            _displayWindowService.SetCrawlBannerText(CrawlBannerText, VenueName, DjName);
            _displayWindowService.SetMarqueeSpeed(MarqueeSpeed);
            _displayWindowService.SetProjectionView(SelectedProjectionView);

            SelectedMonitorDevice = settings.SelectedMonitorDevice ?? string.Empty;
            _displayWindowService.SetSelectedMonitor(SelectedMonitorDevice);
            RefreshAvailableMonitors();

            // Restores which target "Cast Rotation" will use next, but must NOT start casting
            // to it automatically on launch - casting only ever starts from an explicit
            // "Cast Rotation" click.
            RotationTarget = settings.RotationTarget;
            if (RotationTarget == DisplayTarget.Chromecast)
            {
                _ = DiscoverChromecastsAsync();
            }

// Edited on Aug 11, 2026 -> Suppress Wi-Fi password auto-population when running under Visual Studio Debugger, but retain in the field
            DjBannerMonitorDevice = settings.DjBannerMonitorDevice ?? string.Empty;
            SelectedDjBannerPath = settings.SelectedDjBannerPath ?? string.Empty;
            ConnectInstructionsScreen = string.IsNullOrWhiteSpace(settings.ConnectInstructionsScreen)
                ? "All Screens / Monitors"
                : settings.ConnectInstructionsScreen;
            IsDjBannerEnabled = !System.Diagnostics.Debugger.IsAttached && settings.IsDjBannerEnabled;
            IsDjBannerQrCodeEnabled = settings.IsDjBannerQrCodeEnabled;
            ShowQrCodeOnRotationScreen = settings.ShowQrCodeOnRotationScreen;
            _displayWindowService.SetShowQrCode(ShowQrCodeOnRotationScreen);
            string? currentSsid = WifiHelper.GetConnectedSsid();
            string savedWifiPassword = !string.IsNullOrWhiteSpace(currentSsid) ? WifiPasswordStore.GetPasswordForSsid(currentSsid) : string.Empty;
            WifiPassword = System.Diagnostics.Debugger.IsAttached ? string.Empty : (!string.IsNullOrEmpty(savedWifiPassword) ? savedWifiPassword : (settings.WifiPassword ?? string.Empty));
            ActiveSpecialEvent = string.IsNullOrEmpty(settings.ActiveSpecialEvent) ? "None" : settings.ActiveSpecialEvent;
            // Not calling RefreshConnectInstructionsBanner() here — ConnectionUrl isn't set until
            // StartRequestServer() runs at the end of this constructor, which refreshes the banner
            // itself once the real URL is known.

            SpecialEvents.Clear();
            if (settings.SpecialEvents != null)
            {
                foreach (var ev in settings.SpecialEvents)
                {
                    SpecialEvents.Add(ev);
                }
            }

            foreach (var stdName in DjBannerFileManager.StandardEventNames)
            {
                if (!SpecialEvents.Any(e => e.EventName.Equals(stdName, StringComparison.OrdinalIgnoreCase)))
                {
                    SpecialEvents.Add(new Lyracist.Shared.SpecialEventConfig
                    {
                        EventName = stdName,
                        BannerFileName = DjBannerFileManager.GetStandardBannerFileName(stdName)
                    });
                }
            }

            RefreshAvailableEventBannerFiles();
            RebuildSpecialEventOptions();

            _djBannerWindowService.SetSelectedMonitor(DjBannerMonitorDevice);
            _djBannerWindowService.SetBannerPath(ResolveActiveBannerPath());
            UpdateLastSongState();

            RefreshAvailableDjBanners();

            if (IsDjBannerEnabled)
            {
                _djBannerWindowService.Show(this);
            }

            IsDisplayEnabled = false;

            ThemeService.Apply(SelectedTheme);

            LoadKnownSingers();
            if (IsTestMode)
            {
                AddKnownSinger("Alice Smith");
                AddKnownSinger("Bob Jones");
                AddKnownSinger("Charlie Miller");
                AddKnownSinger("David Taylor");
                AddKnownSinger("Eve Anderson");
                LoadTestData();
            }
            else
            {
                // Load existing rotation from last session
                try
                {
                    LoadDatabaseNow();
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("MainViewModel.Constructor.LoadDatabase", ex);
                }
            }

            _isInitializing = false;
            if (FloatCurrentSingerToTop && Singers.Count > 0)
            {
                var current = RotationHelpers.GetCurrentSinger(Singers);
                if (current == null)
                {
                    var first = Singers.FirstOrDefault(s => s.IsRotationStart && !s.IsInactive && !s.IsPaused && !s.IsSkipped)
                                ?? Singers.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && !s.IsSkipped);
                    if (first != null)
                    {
                        RotationHelpers.SetCurrentSinger(Singers, first, floatCurrentToTop: true, isLastRound: IsLastRound);
                    }
                }
                else
                {
                    RotationHelpers.FloatCurrentSingerToTop(Singers);
                }
            }
            SaveSettings();
            StartRequestServer();
            RebuildRotationJsonCacheNow();
            _ = AutoDetectVenueLocationAsync();
#if !MAUI
            InitializeTrivia();
            _ = LoadAllUsersAsync();
#endif
        }

        private async Task AutoDetectVenueLocationAsync()
        {
            try
            {
                var coords = await WindowsLocationService.Instance.GetCurrentCoordinatesAsync();
                string? ssid = WifiHelper.GetConnectedSsid();
                var matched = VenueLocationStore.FindMatchingVenue(coords?.Latitude, coords?.Longitude, ssid);
                if (matched != null && !string.IsNullOrWhiteSpace(matched.Name))
                {
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (!Venues.Any(v => string.Equals(v, matched.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                Venues.Add(matched.Name);
                                VenueService.Save(Venues);
                            }
                            SelectedVenue = matched.Name;
                            VenueName = matched.Name;
                            QueueSaveSettings();
                        });
                    }
                }
            }
            catch { }
        }

        [RelayCommand]
        private async Task TagCurrentLocationAsync()
        {
            string venue = VenueName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(venue)) return;

            var coords = await WindowsLocationService.Instance.GetCurrentCoordinatesAsync();
            string? ssid = WifiHelper.GetConnectedSsid();
            VenueLocationStore.UpsertVenue(venue, coords?.Latitude, coords?.Longitude, ssid);

            if (coords.HasValue)
            {
#if !MAUI
                System.Windows.MessageBox.Show(
                    $"Location coordinates ({coords.Value.Latitude:F4}, {coords.Value.Longitude:F4}) tagged to '{venue}'. It will auto-retrieve on return visits!",
                    "Venue Location Tagged",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
#endif
            }
        }

        private static async Task AutoSaveNewVenueLocationAsync(string venueName)
        {
            try
            {
                var coords = await WindowsLocationService.Instance.GetCurrentCoordinatesAsync();
                string? ssid = WifiHelper.GetConnectedSsid();
                VenueLocationStore.UpsertVenue(venueName, coords?.Latitude, coords?.Longitude, ssid);
            }
            catch { }
        }

        [RelayCommand]
        private void AddVenue()
        {
            string trimmed = NewVenueName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }

            bool alreadyExists = Venues.Any(v => string.Equals(v, trimmed, System.StringComparison.OrdinalIgnoreCase));
            if (!alreadyExists)
            {
                Venues.Add(trimmed);
                VenueService.Save(Venues);
                _ = AutoSaveNewVenueLocationAsync(trimmed);
            }

            SelectedVenue = Venues.FirstOrDefault(v => string.Equals(v, trimmed, System.StringComparison.OrdinalIgnoreCase));
            NewVenueName = string.Empty;
        }

        [RelayCommand]
        private void RemoveVenue()
        {
            if (string.IsNullOrWhiteSpace(SelectedVenue))
            {
                return;
            }

            string toRemove = SelectedVenue;
            int idx = Venues.IndexOf(toRemove);
            Venues.Remove(toRemove);
            VenueService.Save(Venues);

            SelectedVenue = Venues.ElementAtOrDefault(Math.Max(0, idx - 1));
            if (!string.IsNullOrWhiteSpace(SelectedVenue))
            {
                VenueName = SelectedVenue;
                QueueSaveSettings();
            }
        }

        [RelayCommand]
        private void AddDj()
        {
            string trimmed = NewDjName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }

            bool alreadyExists = Djs.Any(d => string.Equals(d, trimmed, System.StringComparison.OrdinalIgnoreCase));
            if (!alreadyExists)
            {
                Djs.Add(trimmed);
                DjService.Save(Djs);
            }

            SelectedDj = Djs.FirstOrDefault(d => string.Equals(d, trimmed, System.StringComparison.OrdinalIgnoreCase));
            NewDjName = string.Empty;
        }

        [RelayCommand]
        private void RemoveDj()
        {
            if (string.IsNullOrWhiteSpace(SelectedDj))
            {
                return;
            }

            string toRemove = SelectedDj;
            int idx = Djs.IndexOf(toRemove);
            Djs.Remove(toRemove);
            DjService.Save(Djs);

            SelectedDj = Djs.ElementAtOrDefault(Math.Max(0, idx - 1));
            if (!string.IsNullOrWhiteSpace(SelectedDj))
            {
                DjName = SelectedDj;
                QueueSaveSettings();
            }
        }

        [ObservableProperty]
        public partial SingerEntry? LastInsertedSinger { get; set; }

        public event EventHandler<SingerEntry>? SingerInsertedForEditing;

        private void AddActiveSinger(SingerEntry newSinger)
        {
            LastInsertedSinger = newSinger;
            // Linked-adjacency enforcement runs off Singers.CollectionChanged (see
            // OnSingersCollectionChanged) - InsertNewSinger's own Insert() already triggers it.
            RotationHelpers.InsertNewSinger(Singers, newSinger);

            if (newSinger.IsSpecial)
            {
                RotationHelpers.EnforceLinkedAdjacency(Singers);
                RefreshLinkedPartnerNames();
                UpdateNextSingerHighlight();
                RefreshBillboardState();
                RebuildRotationJsonCacheNow();
                if (IsDisplayEnabled)
                {
                    _displayWindowService.Update(Singers);
                }
            }

            // Keeps wait-time badges current as soon as a singer is added - otherwise a fresh
            // rotation shows no badges at all until the first singer finishes, since nothing else
            // recalculates them.
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);

            SingerInsertedForEditing?.Invoke(this, newSinger);
        }

        [RelayCommand]
        private void AddSinger()
        {
            AddActiveSinger(new SingerEntry
            {
                Name = "New Singer",
                Song = string.Empty,
                Artist = string.Empty,
            });
        }

        [RelayCommand]
        private void AddSpecialSinger()
        {
            AddActiveSinger(new SingerEntry
            {
                Name = "Special Guest",
                Song = string.Empty,
                Artist = string.Empty,
                IsSpecial = true,
            });
        }

        /// <summary>Singer the DJ has clicked "Link" on, waiting for a second click on another
        /// singer to complete the link. Null when no link is pending.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingLinkSinger))]
        public partial SingerEntry? PendingLinkSinger { get; set; }

        public bool HasPendingLinkSinger => PendingLinkSinger != null;

        [RelayCommand]
        private void CancelPendingLink() => PendingLinkSinger = null;

        /// <summary>
        /// Two-click linking: clicking "Link" on a singer with nothing pending arms it as the
        /// pending half of a pair; clicking a second (different) singer completes the link.
        /// Clicking the pending singer again cancels. Clicking an already-linked singer unlinks it
        /// instead. See <see cref="RotationHelpers.LinkSingers{T}"/>/<see cref="RotationHelpers.UnlinkSinger{T}"/>.
        /// </summary>
        [RelayCommand]
        private void ToggleLinkSinger(SingerEntry entry)
        {
            if (entry == null) return;

            if (entry.IsLinked)
            {
                RotationHelpers.UnlinkSinger(Singers, entry);
                RefreshLinkedPartnerNames();
                if (PendingLinkSinger == entry) PendingLinkSinger = null;
                RefreshBillboardState();
                return;
            }

            if (PendingLinkSinger == null)
            {
                PendingLinkSinger = entry;
                return;
            }

            if (PendingLinkSinger == entry)
            {
                PendingLinkSinger = null;
                return;
            }

            RotationHelpers.LinkSingers(Singers, PendingLinkSinger, entry);
            RefreshLinkedPartnerNames();
            PendingLinkSinger = null;

            RefreshBillboardState();
        }

        /// <summary>
        /// Refreshes every linked singer's <see cref="SingerEntry.LinkedPartnerName"/> display
        /// convenience from the current rotation. Name isn't part of the shared IRotationSinger
        /// interface, so this lookup has to happen here rather than in RotationHelpers - call it
        /// after anything that could change link state, rotation membership, or a linked singer's Name.
        /// </summary>
        private void RefreshLinkedPartnerNames()
        {
            foreach (var s in Singers)
            {
                if (s.LinkedSingerId.HasValue)
                {
                    var partner = Singers.FirstOrDefault(p => p.Id == s.LinkedSingerId.Value);
                    s.LinkedPartnerName = partner?.Name ?? string.Empty;
                }
                else if (!string.IsNullOrEmpty(s.LinkedPartnerName))
                {
                    s.LinkedPartnerName = string.Empty;
                }
            }
        }

        /// <summary>Permanently deletes a singer row from the rotation (unlike <see cref="ToggleSingerInactive"/>,
        /// which only hides it). Recorded performance history for the singer is unaffected and still appears in
        /// the night's report. Master-console only — not exposed from the DJ web remote or the MAUI app.</summary>
        [RelayCommand]
        private async Task RemoveSinger(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            bool confirmed = await ConfirmAsync(
                $"Permanently remove \"{entry.Name}\" from the rotation? This cannot be undone.",
                "Confirm Remove");

            if (!confirmed)
            {
                return;
            }

            if (entry.IsRotationStart)
            {
                RotationHelpers.HandleSingerRetiredOrRemoved(Singers, entry);
            }

            if (entry.IsCurrent)
            {
                SingerEntry? nextCurrent = RotationHelpers.FindNextEligibleSinger(Singers, entry, isLastRound: IsLastRound);

                entry.IsCurrent = false;
                if (nextCurrent != null)
                {
                    RotationHelpers.SetCurrentSinger(Singers, nextCurrent, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                }
            }

            Singers.Remove(entry);
            RotationHelpers.UnlinkSinger(Singers, entry);
            if (PendingLinkSinger == entry) PendingLinkSinger = null;
            RefreshLinkedPartnerNames();
            RotationHelpers.EnsureRotationStartFlag(Singers);
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
            UpdateNextSingerHighlight();
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        [RelayCommand]
        private async Task ClearRotation()
        {
            bool confirmed = await ConfirmAsync(
                "Are you sure you want to clear the entire rotation list? This cannot be undone.",
                "Confirm Clear");

            if (confirmed)
            {
                Singers.Clear();
                if (IsDisplayEnabled)
                {
                    _displayWindowService.Update(Singers);
                }
            }
        }

        /// <summary>
        /// Adds a fully-specified performer (name/song/artist) to the active section of the queue and
        /// records the name in the known-singers autocomplete list. Returns false without changing
        /// anything if no name is given.
        /// </summary>
        /// <remarks>
        /// Single implementation shared by every UI entry point that lets an operator add a performer
        /// with song details in one step (DJ web portal, MAUI console) so the insertion rules can't drift.
        /// </remarks>
        public bool AddPatronSongRequest(string name, string? duetPartner, string? song, string? artist)
        {
            string trimmedName = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return false;
            }

            string trimmedSong = song?.Trim() ?? string.Empty;
            string trimmedArtist = artist?.Trim() ?? string.Empty;

            var existingSinger = Singers.FirstOrDefault(s => RotationHelpers.IsSameSingerName(s.Name, trimmedName));
            if (existingSinger != null)
            {
                if (existingSinger.IsInactive)
                {
                    existingSinger.IsInactive = false;
                    existingSinger.Song = trimmedSong;
                    existingSinger.Artist = trimmedArtist;
                    existingSinger.DuetPartnerName = duetPartner?.Trim() ?? string.Empty;
                    EnforceActiveInactiveOrder(existingSinger);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existingSinger.Song))
                    {
                        existingSinger.Song = trimmedSong;
                        existingSinger.Artist = trimmedArtist;
                    }
                    else
                    {
                        existingSinger.QueuedSongs.Add(new QueuedSong(trimmedSong, trimmedArtist));
                    }
                }
            }
            else
            {
                AddActiveSinger(new SingerEntry
                {
                    Name = trimmedName,
                    DuetPartnerName = duetPartner?.Trim() ?? string.Empty,
                    Song = trimmedSong,
                    Artist = trimmedArtist,
                });
            }

            AddKnownSinger(trimmedName);
            QueueSaveDatabase();
            return true;
        }

        private static bool SingerHasSong(SingerEntry singer, string? song, string? artist)
        {
            if (string.IsNullOrWhiteSpace(song)) return false;

            if (RotationHelpers.IsSameSong(singer.Song, singer.Artist, song, artist))
            {
                return true;
            }

            return singer.QueuedSongs.Any(qs => RotationHelpers.IsSameSong(qs.Song, qs.Artist, song, artist));
        }

        public bool TryAddPerformer(string? name, string? song, string? artist, string? duetPartner = "", bool isSpecial = false)
        {
            string trimmedName = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return false;
            }

            // Normalize spaces in the added singer's name to clean up any tabs/non-breaking spaces
            trimmedName = RotationHelpers.NormalizeForComparison(trimmedName);

            var existingSinger = Singers.FirstOrDefault(s => RotationHelpers.IsSameSingerName(s.Name, trimmedName));
            if (existingSinger != null)
            {
                bool wasInactive = existingSinger.IsInactive;
                existingSinger.IsInactive = false;
                if (isSpecial)
                {
                    existingSinger.IsSpecial = true;
                    RotationHelpers.PromoteSpecialSingerToCurrent(Singers, existingSinger);
                    RotationHelpers.EnforceLinkedAdjacency(Singers);
                    RefreshLinkedPartnerNames();
                    UpdateNextSingerHighlight();
                    RefreshBillboardState();
                    RebuildRotationJsonCacheNow();
                    if (IsDisplayEnabled)
                    {
                        _displayWindowService.Update(Singers);
                    }
                }
                else if (wasInactive)
                {
                    EnforceActiveInactiveOrder(existingSinger);
                }

                if (!string.IsNullOrWhiteSpace(duetPartner))
                {
                    existingSinger.DuetPartnerName = duetPartner.Trim();
                }

                string trimmedSong = song?.Trim() ?? string.Empty;
                string trimmedArtist = artist?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(trimmedSong))
                {
                    if (string.IsNullOrWhiteSpace(existingSinger.Song))
                    {
                        existingSinger.Song = trimmedSong;
                        existingSinger.Artist = trimmedArtist;
                    }
                    else
                    {
                        existingSinger.QueuedSongs.Add(new QueuedSong(trimmedSong, trimmedArtist));
                    }
                }
            }
            else
            {
                AddActiveSinger(new SingerEntry
                {
                    Name = trimmedName,
                    DuetPartnerName = duetPartner?.Trim() ?? string.Empty,
                    Song = song?.Trim() ?? string.Empty,
                    Artist = artist?.Trim() ?? string.Empty,
                    IsSpecial = isSpecial,
                });
            }

            AddKnownSinger(trimmedName);
            QueueSaveDatabase();
            return true;
        }

        [RelayCommand]
        private void FinishSingerSong(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            int roundToMark = entry.GetNextIncompleteRound();
            if (roundToMark == 0)
            {
                roundToMark = 10;
            }

            if (roundToMark > 0)
            {
                // 1. Mark completed (triggers HandleSongCompletionChanged to record the current song details)
                entry.MarkRoundCompleted(roundToMark);

                // 2. Temporarily set flag so that clearing Song/Artist/Duet does not overwrite performance history
                _isFinishingSong = true;
                try
                {
                    // Clear the duet partner for subsequent songs in the rotation unless a pending request specifies one
                    entry.DuetPartnerName = string.Empty;

                    var pendingRequest = IncomingRequests.FirstOrDefault(r =>
                        string.Equals(r.Name?.Trim(), entry.Name?.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(r.RequestType, "Music", StringComparison.OrdinalIgnoreCase) == entry.IsMusic);

                    if (entry.QueuedSongs.Count > 0)
                    {
                        var nextSong = entry.QueuedSongs[0];
                        entry.QueuedSongs.RemoveAt(0);
                        entry.Song = nextSong.Song;
                        entry.Artist = nextSong.Artist;

                        if (pendingRequest != null)
                        {
                            var reqSongs = pendingRequest.Songs?.Count > 0
                                ? pendingRequest.Songs
                                : new List<RequestedSong> { new RequestedSong(pendingRequest.Song, pendingRequest.Artist) };

                            foreach (var reqSong in reqSongs)
                            {
                                if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                                if (SingerHasSong(entry, reqSong.Song, reqSong.Artist)) continue;

                                entry.QueuedSongs.Add(new QueuedSong(reqSong.Song, reqSong.Artist));
                            }
                            IncomingRequests.Remove(pendingRequest);
                            QueueSaveSettings();
                            QueueSaveDatabase();
                        }
                    }
                    else if (pendingRequest != null)
                    {
                        var reqSongs = pendingRequest.Songs?.Count > 0
                            ? pendingRequest.Songs
                            : new List<RequestedSong> { new RequestedSong(pendingRequest.Song, pendingRequest.Artist) };

                        entry.Song = string.Empty;
                        entry.Artist = string.Empty;
                        if (!string.IsNullOrWhiteSpace(pendingRequest.DuetPartnerName))
                        {
                            entry.DuetPartnerName = pendingRequest.DuetPartnerName;
                        }

                        bool isFirst = true;
                        foreach (var reqSong in reqSongs)
                        {
                            if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                            if (SingerHasSong(entry, reqSong.Song, reqSong.Artist)) continue;

                            if (isFirst)
                            {
                                entry.Song = reqSong.Song;
                                entry.Artist = reqSong.Artist;
                                isFirst = false;
                            }
                            else
                            {
                                entry.QueuedSongs.Add(new QueuedSong(reqSong.Song, reqSong.Artist));
                            }
                        }
                        IncomingRequests.Remove(pendingRequest);
                        QueueSaveSettings();
                        QueueSaveDatabase();
                    }
                    else if (!entry.IsMusic && IsTestMode)
                    {
                        var choices = TestPool.Where(p => p.Song != entry.Song).ToArray();
                        if (choices.Length > 0)
                        {
                            var (song, artist) = choices[_random.Next(choices.Length)];
                            entry.Song = song;
                            entry.Artist = artist;
                        }
                        else
                        {
                            entry.Song = string.Empty;
                            entry.Artist = string.Empty;
                        }
                    }
                    else
                    {
                        entry.Song = string.Empty;
                        entry.Artist = string.Empty;
                    }

                    if (IsLastRound)
                    {
                        entry.HasSungInLastRound = true;
                    }

                    // Advance rotation sequentially after entry (wraps around to top of rotation if last singer)
                    RotationHelpers.AdvanceRotationAfterFinished(Singers, entry, FloatCurrentSingerToTop, isLastRound: IsLastRound);

                    if (entry.IsSpecial)
                    {
                        EnforceActiveInactiveOrder(entry);
                    }

                    // A music request with nothing left queued (and no further pending request merged in above)
                    // has been fully played — unlike a karaoke singer, it doesn't wait around in the rotation for
                    // another turn, so remove it now. The completed performance(s) were already recorded above via
                    // MarkRoundCompleted and remain in history/report independent of Singers membership.
                    if (entry.IsMusic && string.IsNullOrWhiteSpace(entry.Song))
                    {
                        Singers.Remove(entry);
                        RotationHelpers.UnlinkSinger(Singers, entry);
                        if (PendingLinkSinger == entry) PendingLinkSinger = null;
                        UpdateNextSingerHighlight();
                    }
                }
                finally
                {
                    _isFinishingSong = false;
                }

                // Re-establishes the 1st-singer invariant now that _isFinishingSong is suppressing
                // OnSingersCollectionChanged's own reentrant call — matters when a fully-played music
                // request holding the flag was just removed above, since nothing else in this method
                // reassigns it (unlike the RotationHelpers.HandleSingerRetiredOrRemoved calls elsewhere).
                RotationHelpers.EnsureRotationStartFlag(Singers);

                // Linked Singers stays linked all night (no auto-unlink) - EnforceLinkedAdjacency
                // exempts a pair while either half IsCurrent, so this won't drag the finished singer
                // back up next to a partner who was just promoted to perform next; once neither is
                // current anymore (both have had their turn), it re-unites them for their next joint turn.
                RotationHelpers.EnforceLinkedAdjacency(Singers);
                RefreshLinkedPartnerNames();

                // Recalculate every waiting singer's estimated wait time now that the rotation
                // order has settled - KSRotation has no song-duration library, so this always
                // falls back to RotationHelpers.DefaultEstimatedPerformanceSeconds per song ahead.
                RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);

                // 3. Save state and notify displays
                RefreshBillboardState();
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
                if (IsDisplayEnabled)
                {
                    _displayWindowService.Update(Singers);
                }
            }
        }

        /// <summary>Toggles the singer's inactive state instead of removing them.</summary>
        [RelayCommand]
        private void ToggleSingerInactive(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            _isFinishingSong = true;
            try
            {
                bool retiring = !entry.IsInactive;
                if (retiring && entry.IsRotationStart)
                {
                    RotationHelpers.HandleSingerRetiredOrRemoved(Singers, entry);
                }

                bool wasCurrent = !entry.IsInactive && entry.IsCurrent;
                SingerEntry? nextCurrent = wasCurrent ? RotationHelpers.FindNextEligibleSinger(Singers, entry, isLastRound: IsLastRound) : null;

                entry.IsInactive = !entry.IsInactive;

                if (entry.IsInactive && entry.IsCurrent)
                {
                    entry.IsCurrent = false;
                }

                if (wasCurrent && nextCurrent != null)
                {
                    RotationHelpers.SetCurrentSinger(Singers, nextCurrent, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                }

                EnforceActiveInactiveOrder(entry);
                RotationHelpers.EnsureRotationStartFlag(Singers);
                UpdateNextSingerHighlight();
            }
            finally
            {
                _isFinishingSong = false;
            }

            // _isFinishingSong suppressed OnSingersCollectionChanged's reentrant call above (same
            // reason FinishSingerSong re-invokes this once it clears the flag) - without this, pausing
            // or reactivating one half of a linked/duet pair could leave them non-adjacent until some
            // unrelated mutation happened to trigger EnforceLinkedAdjacency again.
            RotationHelpers.EnforceLinkedAdjacency(Singers);
            RefreshLinkedPartnerNames();
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);

            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        /// <summary>Toggles the singer's skipped state for the current round without forfeiting rotation placement.</summary>
        [RelayCommand]
        public void ToggleSkipSinger(SingerEntry entry)
        {
            if (entry == null || entry.IsInactive)
            {
                return;
            }

            _isFinishingSong = true;
            try
            {
                bool skipping = !entry.IsSkipped && entry.IsCurrent;

                entry.IsSkipped = !entry.IsSkipped;

                // If the current performer is being skipped, advance rotation to the next eligible singer,
                // relocating the skipped singer the same way a finished singer would be (matches Lyracist).
                if (skipping)
                {
                    RotationHelpers.AdvanceRotationAfterFinished(Singers, entry, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                }

                UpdateNextSingerHighlight();
            }
            finally
            {
                _isFinishingSong = false;
            }

            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
            RefreshBillboardState();
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        /// <summary>Toggles the singer's one-time special performance state.</summary>
        [RelayCommand]
        public void ToggleSpecialSinger(SingerEntry entry)
        {
            if (entry == null) return;
            entry.IsSpecial = !entry.IsSpecial;
            if (entry.IsSpecial)
            {
                RotationHelpers.PromoteSpecialSingerToCurrent(Singers, entry);
                RotationHelpers.EnforceLinkedAdjacency(Singers);
                RefreshLinkedPartnerNames();
                RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
                UpdateNextSingerHighlight();
                RefreshBillboardState();
            }
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        [RelayCommand]
        public void MoveSingerUp(SingerEntry entry)
        {
            if (entry == null) return;
            if (RotationHelpers.MoveSingerUp(Singers, entry))
            {
                UpdateNextSingerHighlight();
                RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
                RefreshBillboardState();
                if (IsDisplayEnabled)
                {
                    _displayWindowService.Update(Singers);
                }
            }
        }

        [RelayCommand]
        public void MoveSingerDown(SingerEntry entry)
        {
            if (entry == null) return;
            if (RotationHelpers.MoveSingerDown(Singers, entry))
            {
                UpdateNextSingerHighlight();
                RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
                RefreshBillboardState();
                if (IsDisplayEnabled)
                {
                    _displayWindowService.Update(Singers);
                }
            }
        }

        private void EnforceActiveInactiveOrder(SingerEntry entry)
        {
            int oldIndex = Singers.IndexOf(entry);
            if (oldIndex == -1) return;

            if (entry.IsInactive)
            {
                // Move to the very end of the list
                int targetIndex = Singers.Count - 1;
                if (oldIndex != targetIndex)
                {
                    Singers.Move(oldIndex, targetIndex);
                }
            }
            else
            {
                // Move to the end of the active section (index = number of other active singers)
                int activeCount = 0;
                for (int i = 0; i < Singers.Count; i++)
                {
                    if (!Singers[i].IsInactive && Singers[i] != entry)
                    {
                        activeCount++;
                    }
                }

                if (oldIndex != activeCount)
                {
                    Singers.Move(oldIndex, activeCount);
                }
            }

            // Linked-adjacency enforcement runs off Singers.CollectionChanged (see
            // OnSingersCollectionChanged) - the Move() calls above already triggered it.
        }

        [RelayCommand]
        private void MoveUp(SingerEntry entry) => MoveSingerUp(entry);

        [RelayCommand]
        private void MoveDown(SingerEntry entry) => MoveSingerDown(entry);

        /// <summary>Promotes the chosen singer to current, reactivating them first if paused.</summary>
        [RelayCommand]
        private void SetCurrentSinger(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            _isFinishingSong = true;
            try
            {
                RotationHelpers.SetCurrentSinger(Singers, entry, FloatCurrentSingerToTop, isLastRound: IsLastRound);
            }
            finally
            {
                _isFinishingSong = false;
            }

            // _isFinishingSong suppressed OnSingersCollectionChanged's reentrant call above, so
            // (as in FinishSingerSong/ToggleSingerInactive) re-invoke it explicitly - manually
            // promoting one half of a linked/duet pair to current shouldn't leave them non-adjacent.
            RotationHelpers.EnforceLinkedAdjacency(Singers);
            RefreshLinkedPartnerNames();

            // Keeps wait-time badges current as soon as someone's marked current - otherwise a fresh
            // rotation shows no badges at all until the first singer finishes.
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);

            // IsCurrent/IsNext changes made above were suppressed by _isFinishingSong, so rebuild explicitly
            // — patron/DJ web clients read this cache and shouldn't see a stale rotation after this action.
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
            RefreshBillboardState();
        }

        /// <summary>
        /// Toggles the 1st singer (round start) designation on the selected singer: designates them if they
        /// don't already hold it, or clears it (auto-reassigning to the first active singer) if they do — so
        /// a mis-flagged singer can be undone without picking a specific replacement.
        /// </summary>
        [RelayCommand]
        private void SetRotationStartSinger(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            bool wasRotationStart = entry.IsRotationStart;
            RotationHelpers.ToggleRotationStartSinger(Singers, entry);
            if (!wasRotationStart && FloatCurrentSingerToTop)
            {
                RotationHelpers.SetCurrentSinger(Singers, entry, floatCurrentToTop: true, isLastRound: IsLastRound);
            }
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        /// <summary>Clears IsNext on all singers, then marks the first active singer after <paramref name="doneEntry"/> as next.</summary>
        private void MarkNextSinger(SingerEntry doneEntry) => RotationHelpers.MarkNextSinger(Singers, doneEntry, isLastRound: IsLastRound);

        /// <summary>Recalculates the next active singer relative to the current singer and sets the green highlight.
        /// Deliberately does NOT touch Linked Singers adjacency - this is called reentrantly from inside
        /// OnSingersCollectionChanged's dispatch of Singers' own CollectionChanged event, and ObservableCollection
        /// forbids mutating a collection while still inside that dispatch. Adjacency is enforced separately
        /// there instead, deferred via Dispatcher.BeginInvoke so it runs once the dispatch has unwound.</summary>
        private void UpdateNextSingerHighlight() => RotationHelpers.UpdateNextSingerHighlight(Singers, isLastRound: IsLastRound);

        /// <summary>Saves an end-of-night report and optionally emails it.</summary>
        [RelayCommand]
        private async Task SaveRotation()
        {
            try
            {
                List<SingerEntry> singersSnapshot =
                [
                    .. Singers.Select(s => new SingerEntry
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Song = s.Song,
                        Artist = s.Artist,
                        IsCurrent = s.IsCurrent,
                        IsNext = s.IsNext,
                        IsInactive = s.IsInactive
                    })
                ];

                List<SongPerformance> historySnapshot = GetPerformanceHistorySnapshot();

                var (pdfPath, csvPath, emailError) = await RotationReportService.SaveAsync(
                    singersSnapshot,
                    historySnapshot,
                    VenueName,
                    EmailRecipient,
                    SendEmailOnSave);

                // The PDF/CSV are on disk by this point regardless of whether the (optional) email draft
                // step succeeded, so the night always gets reset — an email failure alone must not leave
                // the DJ thinking the save failed and skip flushing/clearing.
                NightDatabaseService.Flush();
                lock (_performanceHistoryLock)
                {
                    _performanceHistory.Clear();
                }
                Singers.Clear();

                string emailMsg;
                if (emailError != null)
                {
                    emailMsg = $"\n\nWarning: the reports saved successfully, but the email draft could not be created: {emailError}";
                }
                else if (SendEmailOnSave && !string.IsNullOrWhiteSpace(EmailRecipient))
                {
                    emailMsg = "\n\nAn email draft was also created and opened with the reports attached.";
                }
                else
                {
                    emailMsg = string.Empty;
                }

                System.Windows.MessageBox.Show(
                    $"Rotation reports saved to:\nPDF: {pdfPath}\nCSV: {csvPath}\n\nThe database has been flushed and active queue cleared.{emailMsg}",
                    "Rotation Saved & Reset",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.SaveRotation", ex);
                System.Windows.MessageBox.Show(
                    $"Failed to save rotation reports:\n{ex.Message}",
                    "Save Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnSingersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (SingerEntry entry in _subscribedSingers)
                {
                    entry.PropertyChanged -= OnSingerEntryPropertyChanged;
                }
                _subscribedSingers.Clear();
                _lastSingerNames.Clear();
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (SingerEntry entry in e.OldItems)
                    {
                        entry.PropertyChanged -= OnSingerEntryPropertyChanged;
                        _subscribedSingers.Remove(entry);
                        _lastSingerNames.Remove(entry);
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (SingerEntry entry in e.NewItems)
                    {
                        entry.PropertyChanged += OnSingerEntryPropertyChanged;
                        _subscribedSingers.Add(entry);
                        _lastSingerNames[entry] = entry.Name;
                    }
                }
            }

            // _isFinishingSong guards the same window here as everywhere else in this class: several
            // RotationHelpers operations (e.g. AdvanceRotationAfterFinished's float branch) reorder
            // Singers via internal RemoveAt+Insert pairs, which fire this handler mid-operation. Reacting
            // to those intermediate states here would be redundant at best — the outer operation already
            // calls EnsureRotationStartFlag/UpdateNextSingerHighlight itself once it's done.
            if (!_isInitializing && !_isFinishingSong)
            {
                RotationHelpers.EnsureRotationStartFlag(Singers);
                UpdateNextSingerHighlight();
                RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);

                // EnforceLinkedAdjacency can itself Move/RemoveAt+Insert on Singers, which
                // ObservableCollection forbids while still inside the dispatch of this very
                // CollectionChanged event (throws "Cannot change ObservableCollection during a
                // CollectionChanged event"). Defer it to run once the current dispatch has fully
                // unwound - this single hook fires on every structural change to Singers, so it's
                // the one place that gives full coverage without needing to chase every mutation
                // call site individually.
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    RotationHelpers.EnforceLinkedAdjacency(Singers);
                    RefreshLinkedPartnerNames();
                }));
            }

            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }

            if (!_isInitializing)
            {
                QueueSaveDatabase();
            }

            RebuildRotationJsonCacheNow();
            OnPropertyChanged(nameof(SingersInRotationCount));
            OnPropertyChanged(nameof(CanLoadTestData));
            RecalculateRoundEstimation();
            RefreshBillboardState();
        }

        private void OnSingerEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is not SingerEntry entry) return;
            if (_isFinishingSong) return;

            bool databaseChanged = false;

            if (e.PropertyName == nameof(SingerEntry.Name))
            {
                if (_lastSingerNames.TryGetValue(entry, out string? oldName) && oldName != entry.Name)
                {
                    lock (_performanceHistoryLock)
                    {
                        foreach (var perf in _performanceHistory.Where(p => p.SingerId == entry.Id))
                        {
                            perf.SingerName = entry.Name;
                        }
                    }
                    _lastSingerNames[entry] = entry.Name;
                    databaseChanged = true;
                }
                if (!string.IsNullOrWhiteSpace(entry.Name) && !string.Equals(entry.Name, "New Singer", StringComparison.OrdinalIgnoreCase))
                {
                    AddKnownSinger(entry.Name);

                    // Check if another singer already exists with the same name (ignoring spaces/casing)
                    var target = Singers.FirstOrDefault(s => s != entry && RotationHelpers.IsSameSingerName(s.Name, entry.Name));
                    if (target != null)
                    {
                        Action mergeAction = () =>
                        {
                            if (Singers.Contains(entry))
                            {
                                var existingTarget = Singers.FirstOrDefault(s => s != entry && RotationHelpers.IsSameSingerName(s.Name, entry.Name));
                                if (existingTarget != null)
                                {
                                    // 1. Reactivate the target singer if they were inactive
                                    existingTarget.IsInactive = false;

                                    // 2. Merge the current song details from the duplicate row
                                    if (!string.IsNullOrWhiteSpace(entry.Song))
                                    {
                                        if (string.IsNullOrWhiteSpace(existingTarget.Song))
                                        {
                                            existingTarget.Song = entry.Song;
                                            existingTarget.Artist = entry.Artist;
                                        }
                                        else
                                        {
                                            existingTarget.QueuedSongs.Add(new QueuedSong(entry.Song, entry.Artist));
                                        }
                                    }

                                    // 3. Merge any queued songs from the duplicate row
                                    existingTarget.QueuedSongs.AddRange(entry.QueuedSongs);

                                    // 4. Same cleanup RemoveSinger performs before deleting a row - without
                                    // this, merging away a singer who happened to be IsCurrent silently left
                                    // no current singer at all, and a linked/rotation-start entry left a
                                    // dangling reference instead of being reassigned.
                                    if (entry.IsRotationStart)
                                    {
                                        RotationHelpers.HandleSingerRetiredOrRemoved(Singers, entry);
                                    }

                                    if (entry.IsCurrent)
                                    {
                                        SingerEntry? nextCurrent = RotationHelpers.FindNextEligibleSinger(Singers, entry, isLastRound: IsLastRound);

                                        entry.IsCurrent = false;
                                        if (nextCurrent != null)
                                        {
                                            RotationHelpers.SetCurrentSinger(Singers, nextCurrent, FloatCurrentSingerToTop, isLastRound: IsLastRound);
                                        }
                                    }

                                    // 5. Delete the duplicate row
                                    Singers.Remove(entry);
                                    RotationHelpers.UnlinkSinger(Singers, entry);
                                    if (PendingLinkSinger == entry) PendingLinkSinger = null;
                                    RotationHelpers.EnsureRotationStartFlag(Singers);
                                    RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);

                                    // 6. Update lists & database save
                                    UpdateNextSingerHighlight();
                                    RefreshLinkedPartnerNames();
                                    RebuildRotationJsonCacheNow();
                                    QueueSaveDatabase();
                                }
                            }
                        };

                        if (IsTestMode)
                        {
                            mergeAction();
                        }
                        else
                        {
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(mergeAction);
                        }
                    }
                }
            }
            else if (e.PropertyName == nameof(SingerEntry.Song) || e.PropertyName == nameof(SingerEntry.Artist))
            {
                // Disabled to prevent overwriting past performance history when current song/artist changes
                // UpdatePerformanceForSinger(entry);
                databaseChanged = true;

                // Handle song cleared case: automatically post the next song in the performer's queue
                if (!_isFinishingSong && string.IsNullOrWhiteSpace(entry.Song))
                {
                    var pendingRequest = IncomingRequests.FirstOrDefault(r =>
                        string.Equals(r.Name?.Trim(), entry.Name?.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(r.RequestType, "Music", StringComparison.OrdinalIgnoreCase) == entry.IsMusic);
                    if (entry.QueuedSongs.Count > 0)
                    {
                        var nextSong = entry.QueuedSongs[0];
                        entry.QueuedSongs.RemoveAt(0);

                        _isFinishingSong = true;
                        try
                        {
                            entry.Song = nextSong.Song;
                            entry.Artist = nextSong.Artist;

                            if (pendingRequest != null)
                            {
                                var reqSongs = pendingRequest.Songs?.Count > 0
                                    ? pendingRequest.Songs
                                    : new List<RequestedSong> { new RequestedSong(pendingRequest.Song, pendingRequest.Artist) };

                                foreach (var reqSong in reqSongs)
                                {
                                    if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                                    if (SingerHasSong(entry, reqSong.Song, reqSong.Artist)) continue;

                                    entry.QueuedSongs.Add(new QueuedSong(reqSong.Song, reqSong.Artist));
                                }
                                IncomingRequests.Remove(pendingRequest);
                                QueueSaveSettings();
                                QueueSaveDatabase();
                            }
                        }
                        finally
                        {
                            _isFinishingSong = false;
                        }
                    }
                    else if (pendingRequest != null)
                    {
                        _isFinishingSong = true;
                        try
                        {
                            var reqSongs = pendingRequest.Songs?.Count > 0
                                ? pendingRequest.Songs
                                : new List<RequestedSong> { new RequestedSong(pendingRequest.Song, pendingRequest.Artist) };

                            bool isFirst = true;
                            foreach (var reqSong in reqSongs)
                            {
                                if (string.IsNullOrWhiteSpace(reqSong.Song)) continue;

                                if (SingerHasSong(entry, reqSong.Song, reqSong.Artist)) continue;

                                if (isFirst)
                                {
                                    entry.Song = reqSong.Song;
                                    entry.Artist = reqSong.Artist;
                                    isFirst = false;
                                }
                                else
                                {
                                    entry.QueuedSongs.Add(new QueuedSong(reqSong.Song, reqSong.Artist));
                                }
                            }
                            IncomingRequests.Remove(pendingRequest);
                            QueueSaveSettings();
                            QueueSaveDatabase();
                        }
                        finally
                        {
                            _isFinishingSong = false;
                        }
                    }
                }
            }
            else if (e.PropertyName != null && TryGetSongRound(e.PropertyName, out int completionRound))
            {
                HandleSongCompletionChanged(entry, completionRound, entry.IsRoundCompleted(completionRound));
                databaseChanged = true;
            }

            if (databaseChanged)
            {
                QueueSaveDatabase();
            }

            if (IsDisplayEnabled
                && (e.PropertyName == nameof(SingerEntry.IsCurrent)
                    || e.PropertyName == nameof(SingerEntry.IsInactive)
                    || e.PropertyName == nameof(SingerEntry.Name)
                    || e.PropertyName == nameof(SingerEntry.Song)
                    || e.PropertyName == nameof(SingerEntry.Artist)))
            {
                _displayWindowService.Update(Singers);
            }

            if (e.PropertyName == nameof(SingerEntry.IsCurrent) && entry.IsCurrent && FloatCurrentSingerToTop)
            {
                RotationHelpers.FloatCurrentSingerToTop(Singers);
            }

            if (e.PropertyName == nameof(SingerEntry.Name)
                || e.PropertyName == nameof(SingerEntry.Song)
                || e.PropertyName == nameof(SingerEntry.Artist)
                || e.PropertyName == nameof(SingerEntry.IsCurrent)
                || e.PropertyName == nameof(SingerEntry.IsNext)
                || e.PropertyName == nameof(SingerEntry.IsInactive)
                || TryGetSongRound(e.PropertyName ?? string.Empty, out _))
            {
                // IsCurrent/IsNext/IsInactive/RoundCompleted are single-shot toggles → rebuild immediately so the
                // web view reflects rotation changes promptly. Name/Song/Artist stream per-keystroke → debounce.
                if (e.PropertyName == nameof(SingerEntry.Name)
                    || e.PropertyName == nameof(SingerEntry.Song)
                    || e.PropertyName == nameof(SingerEntry.Artist))
                {
                    QueueRebuildRotationJsonCache();
                }
                else
                {
                    RebuildRotationJsonCacheNow();
                }
            }

            if (e.PropertyName == nameof(SingerEntry.IsInactive) || e.PropertyName == nameof(SingerEntry.IsMusic))
            {
                OnPropertyChanged(nameof(SingersInRotationCount));
            }

            if (e.PropertyName == nameof(SingerEntry.IsInactive)
                || e.PropertyName == nameof(SingerEntry.IsMusic)
                || e.PropertyName == nameof(SingerEntry.IsCurrent)
                || e.PropertyName == nameof(SingerEntry.IsNext)
                || e.PropertyName == nameof(SingerEntry.IsPaused)
                || e.PropertyName == nameof(SingerEntry.IsSkipped)
                || e.PropertyName == nameof(SingerEntry.IsSpecial)
                || e.PropertyName == nameof(SingerEntry.IsRotationStart)
                || e.PropertyName == nameof(SingerEntry.HasSungInLastRound)
                || e.PropertyName == nameof(SingerEntry.EstimatedPerformanceSeconds))
            {
                RecalculateRoundEstimation();
            }

            RefreshBillboardState();
        }

        private void UpdatePerformanceForSinger(SingerEntry entry)
        {
            int highestCheckedRound = entry.GetHighestCompletedRound();

            if (highestCheckedRound > 0)
            {
                SongPerformance? perf;
                lock (_performanceHistoryLock)
                {
                    perf = _performanceHistory.FirstOrDefault(p => p.SingerId == entry.Id && p.Round == highestCheckedRound);
                }

                if (perf != null)
                {
                    perf.SongTitle = entry.Song;
                    perf.ArtistName = entry.Artist;
                }
            }
        }

        private static bool TryGetSongRound(string propertyName, out int round)
        {
            // Matches "Song1Completed" … "Song10Completed" without allocating.
            if (propertyName.StartsWith("Song", StringComparison.Ordinal)
                && propertyName.EndsWith("Completed", StringComparison.Ordinal)
                && int.TryParse(propertyName.AsSpan(4, propertyName.Length - 13), out round)
                && round >= 1 && round <= 10)
            {
                return true;
            }
            round = 0;
            return false;
        }

        private void HandleSongCompletionChanged(SingerEntry entry, int round, bool isCompleted)
        {
            lock (_performanceHistoryLock)
            {
                if (isCompleted)
                {
                    bool exists = _performanceHistory.Any(p => p.SingerId == entry.Id && p.Round == round);
                    if (!exists)
                    {
                        _performanceHistory.Add(new SongPerformance
                        {
                            SingerId = entry.Id,
                            SingerName = entry.Name,
                            DuetPartnerName = entry.DuetPartnerName,
                            SongTitle = entry.Song,
                            ArtistName = entry.Artist,
                            Round = round,
                            Timestamp = DateTime.Now,
                            IsMusic = entry.IsMusic
                        });
                    }
                }
                else
                {
                    _performanceHistory.RemoveAll(p => p.SingerId == entry.Id && p.Round == round);
                }
            }
        }

        private void QueueSaveDatabase()
        {
            _dbDebounceTimer.Stop();
            _dbDebounceTimer.Start();
        }

        private void SaveDatabaseNow()
        {
            if (IsTestMode) return;
            NightDatabaseService.Save(Singers, GetPerformanceHistorySnapshot());
        }

        private void LoadDatabaseNow()
        {
            if (IsTestMode) return;
            var state = NightDatabaseService.Load();

            Singers.Clear();
            foreach (var singer in state.ActiveQueue)
            {
                Singers.Add(singer);
            }

            // Guarantees someone holds the "1st singer" (IsRotationStart) badge after a restore -
            // a saved night whose flag holder is gone (or was never saved) would otherwise leave
            // every singer without it, since this loop bypasses InsertNewSinger.
            RotationHelpers.EnsureRotationStartFlag(Singers);

            lock (_performanceHistoryLock)
            {
                _performanceHistory.Clear();
                _performanceHistory.AddRange(state.PerformanceHistory);
            }
        }

        public void ResetEverything()
        {
            Singers.Clear();
            lock (_performanceHistoryLock)
            {
                _performanceHistory.Clear();
            }
            IncomingRequests.Clear();
            DjPin = GenerateDjPin();

            SaveDatabaseNow();
            SaveSettingsNow();
            RebuildRotationJsonCacheNow();
        }

        private List<SongPerformance> GetPerformanceHistorySnapshot()
        {
            lock (_performanceHistoryLock)
            {
                return
                [
                    .. _performanceHistory.Select(p => new SongPerformance
                    {
                        SingerId = p.SingerId,
                        SingerName = p.SingerName,
                        DuetPartnerName = p.DuetPartnerName,
                        SongTitle = p.SongTitle,
                        ArtistName = p.ArtistName,
                        Round = p.Round,
                        Timestamp = p.Timestamp,
                        IsMusic = p.IsMusic
                    })
                ];
            }
        }

        public SessionHandoffPayload ExportSessionHandoffPayload()
        {
            return new SessionHandoffPayload
            {
                Version = 1,
                ExportedAt = DateTime.Now,
                SourceDevice = Environment.MachineName,
                VenueName = VenueName,
                DjName = DjName,
                DjPin = DjPin,
                IsLastRound = IsLastRound,
                EnableSessionSchedule = EnableSessionSchedule,
                SessionStartTime = SessionStartTime,
                SessionStopTime = SessionStopTime,
                EnableLastRequestTime = EnableLastRequestTime,
                LastRequestTime = LastRequestTime,
                BlockDuplicateSongsInSession = BlockDuplicateSongsInSession,
                FloatCurrentSingerToTop = FloatCurrentSingerToTop,
                ShowEstimatedWaitTime = ShowEstimatedWaitTime,
                DefaultSongLengthMinutes = DefaultSongLengthMinutes,
                ActiveSpecialEvent = ActiveSpecialEvent,
                Singers = [.. Singers],
                PerformanceHistory = GetPerformanceHistorySnapshot(),
                IncomingRequests = [.. IncomingRequests]
            };
        }

        /// <summary>
        /// Applies an incoming session handoff. <paramref name="requireConfirmation"/> defaults to true
        /// because this is also the delegate the network server invokes directly on an authenticated
        /// POST /api/session/handoff (see MainViewModel.Requests.cs's server wiring) - without an
        /// explicit prompt here, anyone who has the DJ PIN could silently wipe and replace the live
        /// rotation on this device with no warning. Callers that already confirmed with the user
        /// locally before initiating the transfer (e.g. PullSessionFromHostAsync, invoked only after
        /// the Switch Device UI's own "this will replace..." dialog) pass false to avoid a redundant
        /// second prompt.
        /// </summary>
        public async Task<string?> ImportSessionHandoffPayloadAsync(SessionHandoffPayload payload, bool requireConfirmation = true)
        {
            if (payload == null) return "Payload was null.";

            if (requireConfirmation)
            {
                string source = string.IsNullOrWhiteSpace(payload.SourceDevice) ? "another device" : payload.SourceDevice;
                string venue = string.IsNullOrWhiteSpace(payload.VenueName) ? "" : $" ({payload.VenueName})";
                string message =
                    $"Incoming session handoff from \"{source}\"{venue} with {payload.Singers?.Count ?? 0} singer(s).\n\n" +
                    "This will REPLACE the current singer rotation and performance history on this device.\n\n" +
                    "Accept this transfer?";

                // Bounded so a client waiting on the HTTP response (which is what's actually
                // blocked while this prompt is up) doesn't hang forever if nobody's looking at
                // the screen - an unanswered prompt is treated as a decline.
                Task<bool> confirmTask = ConfirmationService.ShowYesNoAsync(message, "Incoming Session Handoff");
                Task completed = await Task.WhenAny(confirmTask, Task.Delay(TimeSpan.FromSeconds(30)));
                if (completed != confirmTask || !await confirmTask)
                {
                    return "Transfer was declined or timed out on the receiving device.";
                }
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ImportSessionHandoffPayloadInternal(payload);
            });

            return null;
        }

        public void ImportSessionHandoffPayloadInternal(SessionHandoffPayload payload)
        {
            if (payload == null) return;

            // 1. Session Settings
            if (!string.IsNullOrWhiteSpace(payload.VenueName))
            {
                VenueName = payload.VenueName;
                if (!Venues.Any(v => string.Equals(v, payload.VenueName, StringComparison.OrdinalIgnoreCase)))
                {
                    Venues.Add(payload.VenueName);
                    VenueService.Save(Venues);
                }
                SelectedVenue = payload.VenueName;
            }

            if (!string.IsNullOrWhiteSpace(payload.DjName))
            {
                DjName = payload.DjName;
                if (!Djs.Any(d => string.Equals(d, payload.DjName, StringComparison.OrdinalIgnoreCase)))
                {
                    Djs.Add(payload.DjName);
                    DjService.Save(Djs);
                }
                SelectedDj = payload.DjName;
            }

            if (!string.IsNullOrWhiteSpace(payload.DjPin))
            {
                DjPin = payload.DjPin;
            }

            IsLastRound = payload.IsLastRound;
            EnableSessionSchedule = payload.EnableSessionSchedule;
            if (!string.IsNullOrWhiteSpace(payload.SessionStartTime)) SessionStartTime = payload.SessionStartTime;
            if (!string.IsNullOrWhiteSpace(payload.SessionStopTime)) SessionStopTime = payload.SessionStopTime;
            EnableLastRequestTime = payload.EnableLastRequestTime;
            if (!string.IsNullOrWhiteSpace(payload.LastRequestTime)) LastRequestTime = payload.LastRequestTime;
            BlockDuplicateSongsInSession = payload.BlockDuplicateSongsInSession;
            FloatCurrentSingerToTop = payload.FloatCurrentSingerToTop;
            ShowEstimatedWaitTime = payload.ShowEstimatedWaitTime;
            DefaultSongLengthMinutes = payload.DefaultSongLengthMinutes;
            if (!string.IsNullOrWhiteSpace(payload.ActiveSpecialEvent)) ActiveSpecialEvent = payload.ActiveSpecialEvent;

            // 2. Clear and Restore Singers
            Singers.Clear();
            if (payload.Singers != null)
            {
                foreach (var s in payload.Singers)
                {
                    Singers.Add(s);
                }
            }

            // Ensure start flag invariant
            RotationHelpers.EnsureRotationStartFlag(Singers);
            RefreshLinkedPartnerNames();
            UpdateNextSingerHighlight();

            // 3. Performance History
            lock (_performanceHistoryLock)
            {
                _performanceHistory.Clear();
                if (payload.PerformanceHistory != null)
                {
                    _performanceHistory.AddRange(payload.PerformanceHistory);
                }
            }

            // 4. Incoming Requests
            IncomingRequests.Clear();
            if (payload.IncomingRequests != null)
            {
                foreach (var r in payload.IncomingRequests)
                {
                    IncomingRequests.Add(r);
                }
            }

            // 5. Recalculate wait times, refresh UI, rebuild cache, and save immediately
            RotationHelpers.RecalculateEstimatedWaits(Singers, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: DefaultSongLengthMinutes * 60.0, enabled: ShowEstimatedWaitTime);
            RefreshBillboardState();
            RebuildRotationJsonCacheNow();
            SaveDatabaseNow();
            SaveSettingsNow();
            RefreshConnectionInfo();

            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        private void SaveSettingsNow()
        {
            AppSettings settings = new()
            {
                VenueName = string.IsNullOrWhiteSpace(VenueName) ? "Karaoke Night" : VenueName.Trim(),
                DjName = string.IsNullOrWhiteSpace(DjName) ? "Guest DJ" : DjName.Trim(),
                ListDjAndVenueOnBillboard = ListDjAndVenueOnBillboard,
                Theme = NormalizeTheme(SelectedTheme),
                IsDisplayEnabled = IsDisplayEnabled,
                BannerText = string.IsNullOrWhiteSpace(BannerText) ? "Welcome to Karaoke Night!" : BannerText.Trim(),
                CrawlBannerText = string.IsNullOrWhiteSpace(CrawlBannerText) ? AppSettings.DefaultCrawlBannerText : CrawlBannerText.Trim(),
                MarqueeSpeed = Math.Clamp(MarqueeSpeed, 20, 200),
                IsTestMode = IsTestMode,
                EmailRecipient = EmailRecipient.Trim(),
                SendEmailOnSave = SendEmailOnSave,
                ProjectionView = SelectedProjectionView,
                WatermarkOpacity = WatermarkOpacity,
                PreferredHostIp = string.IsNullOrWhiteSpace(PreferredHostIp) ? string.Empty : PreferredHostIp.Trim(),
                DjPin = string.IsNullOrWhiteSpace(DjPin) ? string.Empty : DjPin.Trim(),
                SelectedMonitorDevice = SelectedMonitorDevice,
                DjBannerMonitorDevice = DjBannerMonitorDevice,
                ConnectInstructionsScreen = ConnectInstructionsScreen,
                SelectedDjBannerPath = SelectedDjBannerPath,
                IsDjBannerEnabled = IsDjBannerEnabled,
                IsDjBannerQrCodeEnabled = IsDjBannerQrCodeEnabled,
                ShowQrCodeOnRotationScreen = ShowQrCodeOnRotationScreen,
                WifiPassword = WifiPassword,
                ActiveSpecialEvent = ActiveSpecialEvent,
                SpecialEvents = SpecialEvents.ToList(),
                RotationTarget = RotationTarget,
                AutoAcceptRequests = AutoAcceptRequests,
                FloatCurrentSingerToTop = FloatCurrentSingerToTop,
                DefaultSongLengthMinutes = Math.Clamp(DefaultSongLengthMinutes, 1, 20),
                ShowEstimatedWaitTime = ShowEstimatedWaitTime,
                BlockDuplicateSongsInSession = BlockDuplicateSongsInSession,
                EnableSessionSchedule = EnableSessionSchedule,
                SessionStartTime = SessionStartTime,
                SessionStopTime = SessionStopTime,
                EnableLastRequestTime = EnableLastRequestTime,
                LastRequestTime = LastRequestTime,
                AutoSwitchToRemoteDjOnHandoff = AutoSwitchToRemoteDjOnHandoff
            };

            try
            {
                SettingsService.Save(settings);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.SaveSettingsNow", ex);
            }
        }

        private void QueueSaveSettings()
        {
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        [RelayCommand]
        private void RefreshMonitors()
        {
            RefreshAvailableMonitors();
        }

        private void RefreshAvailableDjBanners()
        {
            AvailableDjBanners.Clear();
            foreach (var item in DjBannerFileManager.ScanBanners(Globals.DjBannersDir))
            {
                AvailableDjBanners.Add(item);
            }

            if (!string.IsNullOrEmpty(SelectedDjBannerPath) && AvailableDjBanners.Any(b => b.FullPath == SelectedDjBannerPath))
            {
                // Keep it
            }
            else
            {
                SelectedDjBannerPath = AvailableDjBanners.FirstOrDefault()?.FullPath ?? string.Empty;
            }
        }

        [RelayCommand]
        private void UploadDjBanner()
        {
#if WPF
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Upload DJ Banner",
                Filter = "Supported Banners (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string dest = DjBannerFileManager.CopyInWithDedup(dialog.FileName, Globals.DjBannersDir);
                    RefreshAvailableDjBanners();
                    SelectedDjBannerPath = dest;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to upload DJ Banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
#endif
        }

        [RelayCommand]
        private void DeleteDjBanner()
        {
#if WPF
            if (string.IsNullOrEmpty(SelectedDjBannerPath)) return;

            string fileName = Path.GetFileName(SelectedDjBannerPath);
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete the DJ Banner '{fileName}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    string path = SelectedDjBannerPath;
                    SelectedDjBannerPath = string.Empty;

                    DjBannerFileManager.DeleteBanner(path);
                    RefreshAvailableDjBanners();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to delete DJ Banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
#endif
        }

        private void RefreshAvailableMonitors()
        {
#if WPF
            var monitors = MonitorEnumerator.GetMonitors();
            string currentSelection = SelectedMonitorDevice;

            AvailableMonitors.Clear();
            AvailableMonitorsWithAll.Clear();
            AvailableMonitorsWithAll.Add(new MonitorItem { DeviceName = "None", FriendlyName = "None" });
            AvailableMonitorsWithAll.Add(new MonitorItem { DeviceName = "All Screens / Monitors", FriendlyName = "All Screens / Monitors" });

            foreach (var monitor in monitors)
            {
                string friendly = $"Monitor {monitor.Index + 1} ({monitor.Width}x{monitor.Height}){(monitor.IsPrimary ? " [Primary]" : "")}";
                var item = new MonitorItem
                {
                    DeviceName = monitor.DeviceName,
                    FriendlyName = friendly
                };
                AvailableMonitors.Add(item);
                AvailableMonitorsWithAll.Add(item);
            }

            if (!string.IsNullOrEmpty(currentSelection) && AvailableMonitors.Any(m => string.Equals(m.DeviceName, currentSelection, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedMonitorDevice = currentSelection;
            }
            else
            {
                if (monitors.Count > 1)
                {
                    SelectedMonitorDevice = monitors[1].DeviceName;
                }
                else if (monitors.Count > 0)
                {
                    SelectedMonitorDevice = monitors[0].DeviceName;
                }
                else
                {
                    SelectedMonitorDevice = string.Empty;
                }
            }
#else
            AvailableMonitors.Clear();
#endif
        }

        [RelayCommand]
        private void SaveCurrentAsTestList()
        {
            try
            {
                string testListPath = Path.Combine(AppPaths.SettingsDirectoryPath, "test_singers.json");
                List<SingerEntry> testSnapshot = [.. Singers];
                AtomicJsonFile.Serialize(testListPath, testSnapshot, AppJsonContext.Default.Options);
            }
            catch (IOException ex)
            {
                LoggerService.LogError("MainViewModel.SaveCurrentAsTestList.IO", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LoggerService.LogError("MainViewModel.SaveCurrentAsTestList.Auth", ex);
            }
            catch (NotSupportedException ex)
            {
                LoggerService.LogError("MainViewModel.SaveCurrentAsTestList.NotSupported", ex);
            }
        }

        [RelayCommand]
        private void SaveSettings()
        {
            _saveDebounceTimer.Stop();
            SaveSettingsNow();
        }

        private string NormalizeTheme(string theme)
        {
            string normalizedTheme = Themes.FirstOrDefault(t =>
                string.Equals(t, theme, System.StringComparison.OrdinalIgnoreCase)) ?? "System";
            return normalizedTheme;
        }

        private void LoadTestData()
        {
            Singers.Clear();

            (string Name, string Song, string Artist)[] testData =
            [
                ("Dennis Maidon",    "I will Be Alright", "Dennis Maidon"),
                ("Marie Carter",     "Livin' on a Prayer",         "Bon Jovi"),
                ("Brenda Maidon",   "End of the World",     "Ann Murray"),
                ("Carlos Watson",   "Rap God",             "Eminem"),
                ("Amy Banks",     "Don't Stop Believin'",       "Journey"),
                ("Mike Hatton",    "Bohemian Rhapsody",          "Queen"),
                ("Stephen Rayner",     "Remember",        "Dennis Maidon"),
                ("Tim Honeycutt",    "Piano Man",                  "Billy Joel"),
                ("Sharon Jernigan",   "Since U Been Gone",          "Kelly Clarkson"),
                ("Randy Jernigan",      "Mr. Brightside",             "The Killers"),
                ("Todd Stowe",    "Dancing Queen",              "ABBA"),
                ("Wendy Stowe",      "Africa",                     "Toto"),
                ("Wendy Tart",    "Take It to the Limit",    "Eagles"),
                ("Sandra Moore",     "Somebody That I Used to Know", "Gotye"),
                ("Artie Davis",    "Wonderwall",                 "Oasis"),
            ];

            foreach ((string name, string song, string artist) in testData)
            {
                Singers.Add(new SingerEntry
                {
                    Name = name,
                    Song = song,
                    Artist = artist,
                });
            }

            // Guarantees someone holds the "1st singer" (IsRotationStart) badge - defaults to
            // whoever was entered first, since this loop bypasses InsertNewSinger.
            RotationHelpers.EnsureRotationStartFlag(Singers);
        }

        /// <summary>
        /// Shuts down the display popup when the main window closes.
        /// </summary>
        public void Shutdown()
        {
            _saveDebounceTimer.Stop();
            _dbDebounceTimer.Stop();
            _jsonCacheDebounceTimer.Stop();
            // Ensure the cache reflects any edits still pending in the debounce window before exit.
            RebuildRotationJsonCacheNow();
            _requestServer?.Stop();
            SaveSettingsNow();
            SaveDatabaseNow();
            _displayWindowService.Shutdown();
            _djBannerWindowService.Shutdown();
        }

        [RelayCommand]
        private async Task Cast()
        {
            // The explicit, only place actual casting (Miracast, Chromecast, BrowserCast,
            // AirPlay) is started. Requires "Enable Display Window" to already be on, since
            // casting mirrors that local window's content.
            await _displayWindowService.MoveRotationTo(RotationTarget);
        }

        [RelayCommand]
        private async Task Stop()
        {
            await _displayWindowService.StopCastingAsync();
        }

        [RelayCommand]
        private async Task DiscoverChromecastsAsync()
        {
            try
            {
                AvailableChromecasts.Clear();
                var discovery = new ChromecastDiscoveryService();
                var devices = await discovery.DiscoverAsync();

                foreach (var device in devices)
                {
                    AvailableChromecasts.Add(device);
                }

                if (devices.Count > 0)
                {
                    SelectedChromecast = devices[0];
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("MainViewModel.DiscoverChromecasts", ex);
            }
        }

        private void CheckRestoreDjBanner()
        {
            if (_djBannerWasAutoDisabled && !IsDisplayEnabled)
            {
                _djBannerWasAutoDisabled = false;
                IsDjBannerEnabled = true;
            }
        }

        private string ResolveActiveBannerPath()
        {
            if (!string.IsNullOrEmpty(ActiveSpecialEvent) &&
                !ActiveSpecialEvent.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                if (ActiveSpecialEvent.Equals("Last Song", StringComparison.OrdinalIgnoreCase))
                {
                    string lastSongPath = Path.Combine(Globals.EventBannersDir, "LastSong.png");
                    if (File.Exists(lastSongPath))
                    {
                        return lastSongPath;
                    }
                }

                var eventConfig = SpecialEvents.FirstOrDefault(e => e.EventName.Equals(ActiveSpecialEvent, StringComparison.OrdinalIgnoreCase));
                if (eventConfig != null)
                {
                    string fullPath = Path.Combine(Globals.EventBannersDir, eventConfig.BannerFileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            return SelectedDjBannerPath;
        }

        private void UpdateLastSongState()
        {
            bool isLastSong = !string.IsNullOrEmpty(ActiveSpecialEvent) &&
                              ActiveSpecialEvent.Equals("Last Song", StringComparison.OrdinalIgnoreCase);

            string lastSongPath = Path.Combine(Globals.EventBannersDir, "LastSong.png");
            string? activePath = (isLastSong && File.Exists(lastSongPath)) ? lastSongPath : null;

            _displayWindowService.SetLastSongBanner(activePath);
        }

        private void UpdateDjBannerPath()
        {
            _djBannerWindowService.SetBannerPath(ResolveActiveBannerPath());
        }

        public void RefreshAvailableEventBannerFiles()
        {
            DjBannerFileManager.EnsureStandardEventBanners(Globals.EventBannersDir);
            AvailableEventBannerFiles.Clear();
            AvailableEventBannerFiles.Add("None");
            foreach (var item in DjBannerFileManager.ScanBanners(Globals.EventBannersDir))
            {
                AvailableEventBannerFiles.Add(item.FileName);
            }
        }

        public void RebuildSpecialEventOptions()
        {
            if (string.IsNullOrWhiteSpace(ActiveSpecialEvent))
            {
                ActiveSpecialEvent = "None";
            }
            SpecialEventOptions.Clear();
            SpecialEventOptions.Add(new SpecialEventOptionViewModel("None", "None", ActiveSpecialEvent.Equals("None", StringComparison.OrdinalIgnoreCase), OnSpecialEventOptionChanged));
            foreach (var ev in SpecialEvents)
            {
                SpecialEventOptions.Add(new SpecialEventOptionViewModel(ev.EventName, ev.EventName, ActiveSpecialEvent.Equals(ev.EventName, StringComparison.OrdinalIgnoreCase), OnSpecialEventOptionChanged));
            }
        }

        private void OnSpecialEventOptionChanged(string value)
        {
            if (value.Equals("Birthday", StringComparison.OrdinalIgnoreCase))
            {
#if WPF
                string defaultName = Singers.FirstOrDefault(s => s.IsCurrent && !s.IsMusic)?.Name ?? "";
                string? performerName = ShowPersonalizedBirthdayPrompt(defaultName);
                if (performerName != null)
                {
                    try
                    {
                        string birthdayFilePath = Path.Combine(Globals.EventBannersDir, "Birthday.png");
                        DjBannerFileManager.CreatePersonalizedBirthdayBannerPng(birthdayFilePath, performerName);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError("Failed creating birthday banner", ex);
                    }
                }
#endif
            }
            ActiveSpecialEvent = value;
        }

        private void SyncSpecialEventOptions(string eventName)
        {
            bool foundMatch = false;
            foreach (var option in SpecialEventOptions)
            {
                bool shouldBeSelected = string.Equals(option.Value, eventName, StringComparison.OrdinalIgnoreCase);
                if (shouldBeSelected) foundMatch = true;
                if (option.IsSelected != shouldBeSelected)
                {
                    option.SetSelectedQuietly(shouldBeSelected);
                }
            }

            if (!foundMatch && !string.IsNullOrWhiteSpace(eventName) && !eventName.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                var customOption = new SpecialEventOptionViewModel(eventName, eventName, true, OnSpecialEventOptionChanged);
                SpecialEventOptions.Add(customOption);
                foundMatch = true;
            }

            if (!foundMatch)
            {
                var noneOption = SpecialEventOptions.FirstOrDefault(o => o.Value.Equals("None", StringComparison.OrdinalIgnoreCase));
                if (noneOption != null && !noneOption.IsSelected)
                {
                    noneOption.SetSelectedQuietly(true);
                }
            }
        }

#if WPF
        public static string? ShowPersonalizedBirthdayPrompt(string defaultName = "")
        {
            var window = new System.Windows.Window
            {
                Title = "Birthday Special Event Banner",
                Width = 440,
                Height = 220,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStyle = System.Windows.WindowStyle.ToolWindow,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 32)),
                Foreground = System.Windows.Media.Brushes.White,
                Topmost = true
            };

            var grid = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var label = new System.Windows.Controls.TextBlock
            {
                Text = "Enter Birthday Performer Name:",
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(label, 0);

            var textBox = new System.Windows.Controls.TextBox
            {
                Text = defaultName,
                FontSize = 16,
                Padding = new System.Windows.Thickness(8, 6, 8, 6),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 48)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                Margin = new System.Windows.Thickness(0, 0, 0, 16)
            };
            textBox.SelectAll();
            System.Windows.Controls.Grid.SetRow(textBox, 1);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "🎉 Launch Banner",
                IsDefault = true,
                Padding = new System.Windows.Thickness(16, 6, 16, 6),
                Margin = new System.Windows.Thickness(0, 0, 8, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                Foreground = System.Windows.Media.Brushes.Black,
                FontWeight = System.Windows.FontWeights.Bold,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            string? result = null;
            okButton.Click += (s, e) => { result = textBox.Text; window.DialogResult = true; window.Close(); };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                IsCancel = true,
                Padding = new System.Windows.Thickness(16, 6, 16, 6),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelButton.Click += (s, e) => { window.DialogResult = false; window.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            window.Content = grid;
            window.Loaded += (s, e) => textBox.Focus();

            bool? dialogResult = window.ShowDialog();
            return dialogResult == true ? result : null;
        }
#endif

        [RelayCommand]
        private void SaveSpecialEventsMapping()
        {
            QueueSaveSettings();
            UpdateDjBannerPath();
            RebuildRotationJsonCacheNow();
        }

        [RelayCommand]
        private void UploadEventBanner()
        {
#if WPF
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Upload Special Event Banner",
                Filter = "Supported Banners (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string dest = DjBannerFileManager.CopyInWithDedup(dialog.FileName, Globals.EventBannersDir);
                    RefreshAvailableEventBannerFiles();
                    string fileName = Path.GetFileName(dest);
                    if (!AvailableEventBannerFiles.Contains(fileName))
                    {
                        AvailableEventBannerFiles.Add(fileName);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to upload Event Banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
#endif
        }

        [RelayCommand]
        private void AddSpecialEvent()
        {
            int nextIndex = SpecialEvents.Count + 1;
            string newEventName = $"Event {nextIndex}";
            SpecialEvents.Add(new Lyracist.Shared.SpecialEventConfig
            {
                EventName = newEventName,
                BannerFileName = "None"
            });
            RebuildSpecialEventOptions();
            QueueSaveSettings();
        }

        [RelayCommand]
        private void DeleteSpecialEvent(Lyracist.Shared.SpecialEventConfig item)
        {
            if (item == null) return;
            if (DjBannerFileManager.StandardEventNames.Any(s => s.Equals(item.EventName, StringComparison.OrdinalIgnoreCase)))
            {
#if WPF
                System.Windows.MessageBox.Show(
                    $"The event '{item.EventName}' is a standard pre-saved event and cannot be deleted.",
                    "Standard Event Protected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
#endif
                return;
            }

            SpecialEvents.Remove(item);
            if (ActiveSpecialEvent == item.EventName)
            {
                ActiveSpecialEvent = "None";
            }
            RebuildSpecialEventOptions();
            QueueSaveSettings();
            UpdateDjBannerPath();
            RebuildRotationJsonCacheNow();
        }
    }

    public partial class SpecialEventOptionViewModel : ObservableObject
    {
        private readonly Action<string> _onSelected;
        public string DisplayName { get; }
        public string Value { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value) && value)
                {
                    _onSelected(Value);
                }
            }
        }

        public void SetSelectedQuietly(bool value)
        {
            SetProperty(ref _isSelected, value, nameof(IsSelected));
        }

        public SpecialEventOptionViewModel(string displayName, string value, bool isSelected, Action<string> onSelected)
        {
            DisplayName = displayName;
            Value = value;
            _isSelected = isSelected;
            _onSelected = onSelected;
        }
    }
}