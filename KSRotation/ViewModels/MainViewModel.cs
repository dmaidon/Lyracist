// Edited on Aug 10, 2026 @ 14:18:00 -> Default ActiveSpecialEvent to "None"
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
        private readonly DispatcherTimer _dbDebounceTimer;
        private readonly DispatcherTimer _jsonCacheDebounceTimer;
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

        public DisplayTarget[] AvailableDisplayTargets { get; } = (DisplayTarget[])Enum.GetValues(typeof(DisplayTarget));

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
                    QueueSaveSettings();
                    break;

                case nameof(IsDjBannerQrCodeEnabled):
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
                    foreach (var option in SpecialEventOptions)
                    {
                        if (option.Value == ActiveSpecialEvent)
                        {
                            if (!option.IsSelected) option.IsSelected = true;
                        }
                        else
                        {
                            if (option.IsSelected) option.IsSelected = false;
                        }
                    }
                    UpdateDjBannerPath();
                    QueueSaveSettings();
                    RebuildRotationJsonCacheNow();
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
            "Vinyl Turntable"
            // TODO (future): "Jumbotron" — full-bleed stadium scoreboard style with huge singer name
            //                on a bright LED matrix background, scrolling ticker at the bottom.
            // TODO (future): "Neon Bar Sign" — dark brick-wall backdrop with a glowing neon-tube
            //                sign rendering the singer name in flickering neon colors.
        ];

        [ObservableProperty]
        public partial string SelectedProjectionView { get; set; } = "Normal List";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        public partial string VenueName { get; set; } = "Karaoke Night";

        [ObservableProperty]
        public partial string? SelectedVenue { get; set; }

        [ObservableProperty]
        public partial string NewVenueName { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WindowTitle))]
        public partial string DjName { get; set; } = "Guest DJ";

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
        public partial string WifiPassword { get; set; } = string.Empty;

        partial void OnWifiPasswordChanged(string value)
        {
            string? ssid = WifiHelper.GetConnectedSsid();
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                WifiPasswordStore.SetPasswordForSsid(ssid, value);
            }
            RefreshConnectInstructionsBanner();
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
            catch
            {
                // Ignore background rendering exceptions
            }
#endif
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
                if (screenSelection.Equals("All Screens", StringComparison.OrdinalIgnoreCase) ||
                    screenSelection.Equals("All Screens / Monitors", StringComparison.OrdinalIgnoreCase))
                {
                    int maxW = 1920, maxH = 1080;
                    foreach (var m in monitors)
                    {
                        if (m.Width > maxW) maxW = m.Width;
                        if (m.Height > maxH) maxH = m.Height;
                    }
                    return (maxW, maxH);
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
#endif
            return (1920, 1080);
        }

        public ObservableCollection<DjBannerItem> AvailableDjBanners { get; } = [];

        [ObservableProperty]
        public partial string SelectedHelpTopic { get; set; } = "🚀 Getting Started";

        public List<string> HelpTopics { get; } =
        [
            "🚀 Getting Started",
            "🎤 Rotation Management",
            "📺 Display Projection",
            "⚙️ Settings & Venues",
            "📺 Display & DJ Banners",
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

        /// <summary>Count of karaoke singers in the rotation, excluding background music entries (<see cref="SingerEntry.IsMusic"/>).</summary>
        public int SingersInRotationCount => Singers.Count(s => !s.IsMusic);

        public ObservableCollection<string> KnownSingers { get; } = [];
        public ObservableCollection<string> FilteredSingers { get; } = [];

        // ── Assembly Info Properties ──────────────────────────────────────────
        // Resolved once via reflection and cached; these values never change for the app's lifetime,
        // so the About-tab bindings don't re-walk custom attributes on every read.
        private static readonly string s_appTitle = ResolveAppTitle();

        private static readonly string s_appVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        private static readonly string s_appCompany = Lyracist.Shared.Globals.CompanyName;
        private static readonly string s_appCopyright = Lyracist.Shared.Globals.Copyright;
        private static readonly string s_appAuthor = ResolveAppAuthor(s_appCompany);

        public string AppTitle => s_appTitle;
        public string WindowTitle => $"{AppTitle} - Venue: {VenueName} | DJ: {DjName}";
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

        public MainViewModel()
        {
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

            Singers.CollectionChanged += OnSingersCollectionChanged;

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

            DjBannerMonitorDevice = settings.DjBannerMonitorDevice ?? string.Empty;
            SelectedDjBannerPath = settings.SelectedDjBannerPath ?? string.Empty;
            IsDjBannerEnabled = settings.IsDjBannerEnabled;
            IsDjBannerQrCodeEnabled = settings.IsDjBannerQrCodeEnabled;
            string? currentSsid = WifiHelper.GetConnectedSsid();
            string savedWifiPassword = !string.IsNullOrWhiteSpace(currentSsid) ? WifiPasswordStore.GetPasswordForSsid(currentSsid) : string.Empty;
            WifiPassword = !string.IsNullOrEmpty(savedWifiPassword) ? savedWifiPassword : (settings.WifiPassword ?? string.Empty);
            ActiveSpecialEvent = string.IsNullOrEmpty(settings.ActiveSpecialEvent) ? "None" : settings.ActiveSpecialEvent;
            RefreshConnectInstructionsBanner();

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
            SaveSettings();
            StartRequestServer();
            RebuildRotationJsonCacheNow();
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

        private void AddActiveSinger(SingerEntry newSinger)
        {
            int activeCount = 0;
            for (int i = 0; i < Singers.Count; i++)
            {
                if (!Singers[i].IsInactive)
                {
                    activeCount++;
                }
            }
            Singers.Insert(activeCount, newSinger);
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

        /// <summary>Permanently deletes a singer row from the rotation (unlike <see cref="ToggleSingerInactive"/>,
        /// which only hides it). Recorded performance history for the singer is unaffected and still appears in
        /// the night's report. Master-console only — not exposed from the DJ web remote or the MAUI app.</summary>
        [RelayCommand]
        private void RemoveSinger(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Permanently remove \"{entry.Name}\" from the rotation? This cannot be undone.",
                "Confirm Remove",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            if (entry.IsCurrent)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                SingerEntry? nextCurrent = Singers.FirstOrDefault(s => s != entry && s.IsNext && !s.IsInactive && !s.IsPaused);

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = Singers.IndexOf(entry);
                    int count = Singers.Count;
                    for (int i = 1; i < count; i++)
                    {
                        SingerEntry candidate = Singers[(currentIndex + i) % count];
                        if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused)
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }

                entry.IsCurrent = false;
                if (nextCurrent != null)
                {
                    nextCurrent.IsCurrent = true;
                    nextCurrent.IsNext = false;
                }
            }

            Singers.Remove(entry);
            UpdateNextSingerHighlight();
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        [RelayCommand]
        private void ClearRotation()
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear the entire rotation list? This cannot be undone.",
                "Confirm Clear",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
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
        private static bool IsSameSingerName(string? name1, string? name2)
        {
            if (name1 == null || name2 == null) return false;

            // Trim and replace any non-breaking spaces or tabs with regular spaces
            string clean1 = name1.Replace('\u00A0', ' ').Replace('\t', ' ').Trim();
            string clean2 = name2.Replace('\u00A0', ' ').Replace('\t', ' ').Trim();

            // Collapse multiple spaces into one
            clean1 = System.Text.RegularExpressions.Regex.Replace(clean1, @"\s+", " ");
            clean2 = System.Text.RegularExpressions.Regex.Replace(clean2, @"\s+", " ");

            return string.Equals(clean1, clean2, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameSong(string? title1, string? artist1, string? title2, string? artist2)
        {
            string cleanTitle1 = title1?.Replace('\u00A0', ' ').Replace('\t', ' ').Trim() ?? string.Empty;
            string cleanArtist1 = artist1?.Replace('\u00A0', ' ').Replace('\t', ' ').Trim() ?? string.Empty;
            string cleanTitle2 = title2?.Replace('\u00A0', ' ').Replace('\t', ' ').Trim() ?? string.Empty;
            string cleanArtist2 = artist2?.Replace('\u00A0', ' ').Replace('\t', ' ').Trim() ?? string.Empty;

            // Collapse multiple spaces into one
            cleanTitle1 = System.Text.RegularExpressions.Regex.Replace(cleanTitle1, @"\s+", " ");
            cleanArtist1 = System.Text.RegularExpressions.Regex.Replace(cleanArtist1, @"\s+", " ");
            cleanTitle2 = System.Text.RegularExpressions.Regex.Replace(cleanTitle2, @"\s+", " ");
            cleanArtist2 = System.Text.RegularExpressions.Regex.Replace(cleanArtist2, @"\s+", " ");

            return string.Equals(cleanTitle1, cleanTitle2, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(cleanArtist1, cleanArtist2, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SingerHasSong(SingerEntry singer, string? song, string? artist)
        {
            if (string.IsNullOrWhiteSpace(song)) return false;

            if (IsSameSong(singer.Song, singer.Artist, song, artist))
            {
                return true;
            }

            return singer.QueuedSongs.Any(qs => IsSameSong(qs.Song, qs.Artist, song, artist));
        }

        public bool TryAddPerformer(string? name, string? song, string? artist)
        {
            string trimmedName = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return false;
            }

            // Normalize spaces in the added singer's name to clean up any tabs/non-breaking spaces
            trimmedName = System.Text.RegularExpressions.Regex.Replace(
                trimmedName.Replace('\u00A0', ' ').Replace('\t', ' '),
                @"\s+",
                " "
            ).Trim();

            var existingSinger = Singers.FirstOrDefault(s => IsSameSingerName(s.Name, trimmedName));
            if (existingSinger != null)
            {
                bool wasInactive = existingSinger.IsInactive;
                existingSinger.IsInactive = false;
                if (wasInactive)
                {
                    EnforceActiveInactiveOrder(existingSinger);
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
                    Song = song?.Trim() ?? string.Empty,
                    Artist = artist?.Trim() ?? string.Empty,
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
            else
            {
                int index = Singers.IndexOf(entry);
                if (index >= 0)
                {
                    int showRound = 1;
                    var activeBefore = new System.Collections.Generic.List<SingerEntry>();
                    for (int j = 0; j < index; j++)
                    {
                        var s = Singers[j];
                        if (!s.IsInactive && !s.IsPaused && !s.IsMusic)
                        {
                            activeBefore.Add(s);
                        }
                    }

                    if (activeBefore.Count > 0)
                    {
                        int maxBefore = 0;
                        foreach (var s in activeBefore)
                        {
                            int highest = s.GetHighestCompletedRound();
                            if (highest > maxBefore)
                            {
                                maxBefore = highest;
                            }
                        }
                        showRound = Math.Max(1, maxBefore);
                    }
                    else
                    {
                        int maxAll = 0;
                        foreach (var s in Singers)
                        {
                            if (!s.IsInactive && !s.IsPaused && !s.IsMusic)
                            {
                                int highest = s.GetHighestCompletedRound();
                                if (highest > maxAll)
                                {
                                    maxAll = highest;
                                }
                            }
                        }
                        showRound = Math.Max(1, maxAll + 1);
                    }

                    int calculatedRound = Math.Max(roundToMark, showRound);
                    if (calculatedRound >= 1 && calculatedRound <= 10)
                    {
                        roundToMark = calculatedRound;
                    }
                }
            }

            if (roundToMark > 0)
            {
                // 1. Mark completed (triggers HandleSongCompletionChanged to record the current song details)
                entry.MarkRoundCompleted(roundToMark);

                // 2. Temporarily set flag so that clearing Song/Artist does not overwrite performance history
                _isFinishingSong = true;
                try
                {
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
                            var reqSongs = pendingRequest.Songs != null && pendingRequest.Songs.Count > 0
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
                        var reqSongs = pendingRequest.Songs != null && pendingRequest.Songs.Count > 0
                            ? pendingRequest.Songs
                            : new List<RequestedSong> { new RequestedSong(pendingRequest.Song, pendingRequest.Artist) };

                        entry.Song = string.Empty;
                        entry.Artist = string.Empty;

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

                    // Advance rotation sequentially after entry (wraps around to top of rotation if last singer)
                    RotationHelpers.AdvanceRotationAfterFinished(Singers, entry);

                    // A music request with nothing left queued (and no further pending request merged in above)
                    // has been fully played — unlike a karaoke singer, it doesn't wait around in the rotation for
                    // another turn, so remove it now. The completed performance(s) were already recorded above via
                    // MarkRoundCompleted and remain in history/report independent of Singers membership.
                    if (entry.IsMusic && string.IsNullOrWhiteSpace(entry.Song))
                    {
                        Singers.Remove(entry);
                        UpdateNextSingerHighlight();
                    }
                }
                finally
                {
                    _isFinishingSong = false;
                }

                // 3. Save state and notify displays
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
                bool pausing = !entry.IsInactive && entry.IsCurrent;
                SingerEntry? nextCurrent = null;

                if (pausing)
                {
                    // 1. Try the singer already flagged as Next (manual next-singer override)
                    nextCurrent = Singers.FirstOrDefault(s => s != entry && s.IsNext && !s.IsInactive && !s.IsPaused);

                    if (nextCurrent == null)
                    {
                        // 2. Fall back to standard index-based rotation
                        int currentIndex = Singers.IndexOf(entry);
                        int count = Singers.Count;
                        for (int i = 1; i < count; i++)
                        {
                            SingerEntry candidate = Singers[(currentIndex + i) % count];
                            if (candidate != entry && !candidate.IsInactive && !candidate.IsPaused)
                            {
                                nextCurrent = candidate;
                                break;
                            }
                        }
                    }
                }

                entry.IsInactive = !entry.IsInactive;

                if (entry.IsInactive && entry.IsCurrent)
                {
                    entry.IsCurrent = false;
                }

                if (pausing && nextCurrent != null)
                {
                    nextCurrent.IsCurrent = true;
                    nextCurrent.IsNext = false;
                }

                EnforceActiveInactiveOrder(entry);
                UpdateNextSingerHighlight();
            }
            finally
            {
                _isFinishingSong = false;
            }

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
            int index = Singers.IndexOf(entry);
            if (index > 0)
            {
                Singers.Move(index, index - 1);
                UpdateNextSingerHighlight();
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
            }
        }

        [RelayCommand]
        public void MoveSingerDown(SingerEntry entry)
        {
            if (entry == null) return;
            int index = Singers.IndexOf(entry);
            if (index >= 0 && index < Singers.Count - 1)
            {
                Singers.Move(index, index + 1);
                UpdateNextSingerHighlight();
                RebuildRotationJsonCacheNow();
                QueueSaveDatabase();
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
        }

        [RelayCommand]
        private void MoveUp(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            int index = Singers.IndexOf(entry);
            if (index > 0)
            {
                if (entry.IsInactive)
                {
                    // Inactive singer can only move up if the one above is also inactive
                    if (Singers[index - 1].IsInactive)
                    {
                        Singers.Move(index, index - 1);
                    }
                }
                else
                {
                    // Active singer can always move up
                    Singers.Move(index, index - 1);
                }
            }
        }

        [RelayCommand]
        private void MoveDown(SingerEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            int index = Singers.IndexOf(entry);
            if (index >= 0 && index < Singers.Count - 1)
            {
                if (!entry.IsInactive)
                {
                    // Active singer can only move down if the one below is also active
                    if (!Singers[index + 1].IsInactive)
                    {
                        Singers.Move(index, index + 1);
                    }
                }
                else
                {
                    // Inactive singer can always move down
                    Singers.Move(index, index + 1);
                }
            }
        }

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
                RotationHelpers.SetCurrentSinger(Singers, entry);
            }
            finally
            {
                _isFinishingSong = false;
            }

            // IsCurrent/IsNext changes made above were suppressed by _isFinishingSong, so rebuild explicitly
            // — patron/DJ web clients read this cache and shouldn't see a stale rotation after this action.
            RebuildRotationJsonCacheNow();
            QueueSaveDatabase();
            if (IsDisplayEnabled)
            {
                _displayWindowService.Update(Singers);
            }
        }

        /// <summary>Clears IsNext on all singers, then marks the first active singer after <paramref name="doneEntry"/> as next.</summary>
        private void MarkNextSinger(SingerEntry doneEntry) => RotationHelpers.MarkNextSinger(Singers, doneEntry);

        /// <summary>Recalculates the next active singer relative to the current singer and sets the green highlight.</summary>
        private void UpdateNextSingerHighlight() => RotationHelpers.UpdateNextSingerHighlight(Singers);

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

                var (pdfPath, csvPath) = await RotationReportService.SaveAsync(
                    singersSnapshot,
                    historySnapshot,
                    VenueName,
                    EmailRecipient,
                    SendEmailOnSave);

                // Flush database on Save Rotation
                NightDatabaseService.Flush();
                lock (_performanceHistoryLock)
                {
                    _performanceHistory.Clear();
                }
                Singers.Clear();

                string emailMsg = SendEmailOnSave && !string.IsNullOrWhiteSpace(EmailRecipient)
                    ? "\n\nAn email draft was also created and opened with the reports attached."
                    : string.Empty;

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

            if (!_isInitializing)
            {
                UpdateNextSingerHighlight();
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
                    var target = Singers.FirstOrDefault(s => s != entry && IsSameSingerName(s.Name, entry.Name));
                    if (target != null)
                    {
                        Action mergeAction = () =>
                        {
                            if (Singers.Contains(entry))
                            {
                                var existingTarget = Singers.FirstOrDefault(s => s != entry && IsSameSingerName(s.Name, entry.Name));
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
                                    foreach (var q in entry.QueuedSongs)
                                    {
                                        existingTarget.QueuedSongs.Add(q);
                                    }

                                    // 4. Delete the duplicate row
                                    Singers.Remove(entry);

                                    // 5. Update lists & database save
                                    UpdateNextSingerHighlight();
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
                                var reqSongs = pendingRequest.Songs != null && pendingRequest.Songs.Count > 0
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
                            var reqSongs = pendingRequest.Songs != null && pendingRequest.Songs.Count > 0
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
                        SongTitle = p.SongTitle,
                        ArtistName = p.ArtistName,
                        Round = p.Round,
                        Timestamp = p.Timestamp,
                        IsMusic = p.IsMusic
                    })
                ];
            }
        }

        private void SaveSettingsNow()
        {
            AppSettings settings = new()
            {
                VenueName = string.IsNullOrWhiteSpace(VenueName) ? "Karaoke Night" : VenueName.Trim(),
                DjName = string.IsNullOrWhiteSpace(DjName) ? "Guest DJ" : DjName.Trim(),
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
                SelectedDjBannerPath = SelectedDjBannerPath,
                IsDjBannerEnabled = IsDjBannerEnabled,
                IsDjBannerQrCodeEnabled = IsDjBannerQrCodeEnabled,
                WifiPassword = WifiPassword,
                ActiveSpecialEvent = ActiveSpecialEvent,
                SpecialEvents = SpecialEvents.ToList(),
                RotationTarget = RotationTarget,
                AutoAcceptRequests = AutoAcceptRequests
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
            ActiveSpecialEvent = value;
        }

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

    public class SpecialEventOptionViewModel : ObservableObject
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

        public SpecialEventOptionViewModel(string displayName, string value, bool isSelected, Action<string> onSelected)
        {
            DisplayName = displayName;
            Value = value;
            _isSelected = isSelected;
            _onSelected = onSelected;
        }
    }
}