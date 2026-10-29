// Edited on Oct 29, 2026 @ 03:48:30 -> Resolve Auto holiday theme using DateTime.Now with a periodic re-evaluation timer across show nights
using KSRotation.Models;
using KSRotation.ViewModels;
using KSRotation.Windows;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Threading.Tasks;
using Screen = System.Windows.Forms.Screen;
using Lyracist.Shared;

namespace KSRotation.Services
{
    public class DisplayWindowService
    {
        private SingerDisplayWindow? _window;
        private readonly DisplayViewModel _viewModel = new();
        private string _connectionUrl = string.Empty;
        private ImageSource? _qrCodeImage;
        private string _selectedMonitorDevice = string.Empty;
        private string _scaryokeMonitorDevice = string.Empty;
        private string? _specialEventBannerPath;
        private bool _openedForScaryoke;
        private string _holidayChoice = HolidayThemes.None;
        private System.Windows.Threading.DispatcherTimer? _holidayAutoTimer;
        private bool _isScaryokeProjected = true;
        private IReadOnlyList<ScaryokeWheelSegment>? _cachedScaryokeSegments;

        public void SetScaryokeActive(bool isActive, ObservableCollection<SingerEntry>? singers = null)
        {
            _viewModel.IsScaryokeActive = isActive;
            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;

            if (isActive)
            {
                if (_window == null)
                {
                    _openedForScaryoke = true;
                    Show(singers ?? []);
                }

                dispatcher.InvokeAsync(() =>
                {
                    if (_window != null)
                    {
                        PositionWindowOnTargetMonitor(_window, GetEffectiveMonitorDevice());
                        if (_cachedScaryokeSegments != null)
                        {
                            _window.BuildScaryokeWheel(_cachedScaryokeSegments);
                        }
                        if (_isScaryokeProjected)
                        {
                            _window.UpdateScaryokeOverlay(true);
                        }
                        _window.Topmost = true;
                        _window.WindowState = WindowState.Normal;
                        _window.WindowState = WindowState.Maximized;
                        _window.Activate();
                    }
                });
            }
            else
            {
                dispatcher.InvokeAsync(() =>
                {
                    if (_window != null)
                    {
                        _window.UpdateScaryokeOverlay(false);
                        if (_openedForScaryoke)
                        {
                            _openedForScaryoke = false;
                            Hide();
                        }
                        else
                        {
                            PositionWindowOnTargetMonitor(_window, _selectedMonitorDevice);
                        }
                    }
                });
            }
        }

        /// <summary>Sets the holiday screen choice ("None", "Auto" or a holiday key); "Auto" is resolved against the current show date.</summary>
        public void SetHolidayTheme(string choice)
        {
            _holidayChoice = choice ?? HolidayThemes.None;
            UpdateAutoHolidayTimer();
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.SetHolidayTheme(HolidayThemes.Resolve(_holidayChoice, DateTime.Now)));
            }
        }

        private void UpdateAutoHolidayTimer()
        {
            if (string.Equals(_holidayChoice, HolidayThemes.Auto, StringComparison.OrdinalIgnoreCase) && _window != null)
            {
                _holidayAutoTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
                _holidayAutoTimer.Tick -= OnHolidayAutoTimerTick;
                _holidayAutoTimer.Tick += OnHolidayAutoTimerTick;
                _holidayAutoTimer.Start();
            }
            else if (_holidayAutoTimer != null)
            {
                _holidayAutoTimer.Stop();
                _holidayAutoTimer.Tick -= OnHolidayAutoTimerTick;
                _holidayAutoTimer = null;
            }
        }

        private void OnHolidayAutoTimerTick(object? sender, EventArgs e)
        {
            if (_window != null && string.Equals(_holidayChoice, HolidayThemes.Auto, StringComparison.OrdinalIgnoreCase))
            {
                _window.SetHolidayTheme(HolidayThemes.Resolve(_holidayChoice, DateTime.Now));
            }
        }

        /// <summary>Drives the live curse mini-spin text and fortune line on the audience wheel overlay.</summary>
        public void SetScaryokeFlavorText(string curseText, string fortuneText)
        {
            _viewModel.ScaryokeCurseText = curseText ?? string.Empty;
            _viewModel.ScaryokeFortuneText = fortuneText ?? string.Empty;
        }

        public void SetScaryokeOverlayVisible(bool isVisible)
        {
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.UpdateScaryokeOverlay(isVisible));
            }
        }

        public void SetScaryokeProjected(bool isProjected)
        {
            _isScaryokeProjected = isProjected;
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.UpdateScaryokeOverlay(isProjected));
            }
        }

        public void UpdateScaryokeWheel(IReadOnlyList<ScaryokeWheelSegment> segments)
        {
            _cachedScaryokeSegments = segments;
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.BuildScaryokeWheel(segments));
            }
        }

        public void SetScaryokeTargetSinger(string singerName)
        {
            _viewModel.ScaryokeTargetSingerName = singerName ?? string.Empty;
        }

        public void StartScaryokeSpin(double targetAngle, double duration)
        {
            _viewModel.ScaryokeResultText = "SPINNING...";
            _viewModel.ScaryokeResultDetail = "Spinning the wheel to seal a singer's fate...";
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.StartScaryokeSpin(targetAngle, duration));
            }
        }

        public void CompleteScaryokeSpin(string resultText, string resultDetail, bool isDjsChoice, double finalAngle, bool keepVisible = true)
        {
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.CompleteScaryokeSpin(resultText, resultDetail, isDjsChoice, finalAngle, keepVisible));
            }
        }

        public void SetLastRound(bool isLastRound)
        {
            _viewModel.IsLastRound = isLastRound;
        }

        public void SetShowQrCode(bool show)
        {
            _viewModel.ShowQrCode = show;
        }

        public void SetShowEstimatedWaitTime(bool show)
        {
            _viewModel.ShowEstimatedWaitTime = show;
        }

        public void SetShowSpectrum(bool show)
        {
            _viewModel.ShowSpectrum = show;
        }

        public void SetSpecialEventBanner(string? path)
        {
            _specialEventBannerPath = path;
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.UpdateSpecialEventBanner(_specialEventBannerPath));
            }
        }

        private readonly CastingService _casting;
        private DisplayTarget _rotationTarget = DisplayTarget.Monitor;

        public ChromecastDevice? SelectedDevice
        {
            get => _casting.SelectedDevice;
            set => _casting.SelectedDevice = value;
        }

        public DisplayWindowService()
        {
            _casting = new CastingService(
                new MiracastController(),
                new ChromecastSender(),
                new BrowserCastServer(),
                new RotationRenderer(() => _window));
        }

        public void SetSelectedMonitor(string deviceName)
        {
            _selectedMonitorDevice = deviceName ?? string.Empty;
        }

        public void SetScaryokeMonitor(string deviceName)
        {
            _scaryokeMonitorDevice = deviceName ?? string.Empty;
            if (_window != null && _viewModel.IsScaryokeActive)
            {
                _window.Dispatcher.InvokeAsync(() =>
                {
                    if (_window != null)
                    {
                        PositionWindowOnTargetMonitor(_window, GetEffectiveMonitorDevice());
                    }
                });
            }
        }

        private string GetEffectiveMonitorDevice()
        {
            if (_viewModel.IsScaryokeActive && !string.IsNullOrWhiteSpace(_scaryokeMonitorDevice))
            {
                return _scaryokeMonitorDevice;
            }
            return _selectedMonitorDevice;
        }

        public async Task<bool> MoveRotationTo(DisplayTarget target)
        {
            _rotationTarget = target;

            if (target != DisplayTarget.Monitor && target != DisplayTarget.WirelessHDMI)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    EnsureWindowOffScreen();
                }
                else
                {
                    await dispatcher.InvokeAsync(EnsureWindowOffScreen);
                }
            }
            else
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    RestoreLocalMonitorWindow();
                }
                else
                {
                    await dispatcher.InvokeAsync(RestoreLocalMonitorWindow);
                }
            }

            return await _casting.CastRotationAsync(target);
        }

        /// <summary>
        /// Stops any active cast (Miracast, Chromecast, BrowserCast, AirPlay) and, if the
        /// display window is still enabled, restores it to the locally selected monitor.
        /// </summary>
        public async Task StopCastingAsync()
        {
            _rotationTarget = DisplayTarget.Monitor;
            await _casting.StopCastingAsync();

            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                RestoreLocalMonitorWindow();
            }
            else
            {
                await dispatcher.InvokeAsync(RestoreLocalMonitorWindow);
            }
        }

        public void RepositionWindow()
        {
            if (_window == null)
            {
                return;
            }

            var previousState = _window.WindowState;
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;
            _window.Topmost = true;

            PositionWindowOnTargetMonitor(_window);

            _window.Dispatcher.InvokeAsync(() =>
            {
                _window.WindowState = WindowState.Normal;
                _window.WindowState = previousState;
            });
        }

        /// <summary>
        /// Opens the singer display popup and initializes its data.
        /// </summary>
        /// <param name="rotation">Ordered singer rotation entries.</param>
        public void Show(ObservableCollection<SingerEntry> rotation)
        {
            ArgumentNullException.ThrowIfNull(rotation);

            if (_window != null)
            {
                if (_rotationTarget == DisplayTarget.Monitor || _rotationTarget == DisplayTarget.WirelessHDMI)
                {
                    _window.Dispatcher.InvokeAsync(() =>
                    {
                        _window.WindowState = WindowState.Normal;
                        _window.WindowState = WindowState.Maximized;
                        _window.Activate();
                    });
                }
                return;
            }

            _window = new SingerDisplayWindow
            {
                DataContext = _viewModel,
            };

            _window.Closed += OnWindowClosed;

            _viewModel.ConnectionUrl = _connectionUrl;
            _viewModel.QrCodeImage = _qrCodeImage;
            _viewModel.UpdateFromRotation(rotation);

            _window.UpdateSpecialEventBanner(_specialEventBannerPath);
            _window.SetHolidayTheme(HolidayThemes.Resolve(_holidayChoice, DateTime.Now));
            UpdateAutoHolidayTimer();

            if (_rotationTarget != DisplayTarget.Monitor && _rotationTarget != DisplayTarget.WirelessHDMI)
            {
                EnsureWindowOffScreen();
            }
            else
            {
                _window.WindowStyle = WindowStyle.None;
                _window.ResizeMode = ResizeMode.NoResize;
                _window.Topmost = true;
                PositionWindowOnTargetMonitor(_window);
                _window.Show();
                _window.Dispatcher.InvokeAsync(() =>
                {
                    _window.WindowState = WindowState.Normal;
                    _window.WindowState = WindowState.Maximized;
                    _window.Activate();
                });
            }
        }

        /// <summary>
        /// Closes the singer display popup if it is open.
        /// </summary>
        public void Hide()
        {
            if (_window == null)
            {
                return;
            }

            _window.Close();
            _ = _casting.StopCastingAsync();
        }

        /// <summary>
        /// Closes the popup unconditionally when the main window is shutting down.
        /// </summary>
        public void Shutdown()
        {
            if (_holidayAutoTimer != null)
            {
                _holidayAutoTimer.Stop();
                _holidayAutoTimer.Tick -= OnHolidayAutoTimerTick;
                _holidayAutoTimer = null;
            }
            _ = _casting.StopCastingAsync();
            if (_window == null)
            {
                return;
            }

            _window.Closed -= OnWindowClosed;
            _window.Close();
            _window = null;
        }

        private void EnsureWindowOffScreen()
        {
            if (_window == null) return;

            _window.WindowStartupLocation = WindowStartupLocation.Manual;
            _window.Left = -20000;
            _window.Top = -20000;
            _window.Width = 1920;
            _window.Height = 1080;
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;
            if (!_window.IsVisible)
            {
                _window.Show();
            }
        }

        private void RestoreLocalMonitorWindow()
        {
            if (_window == null) return;
            _window.WindowStyle = WindowStyle.None;
            _window.ResizeMode = ResizeMode.NoResize;
            _window.Topmost = true;
            PositionWindowOnTargetMonitor(_window);

            _window.Dispatcher.InvokeAsync(() =>
            {
                _window.WindowState = WindowState.Normal;
                _window.WindowState = WindowState.Maximized;
            });
        }

        /// <summary>
        /// Refreshes display data based on the latest singer rotation.
        /// </summary>
        /// <param name="rotation">Ordered singer rotation entries.</param>
        public void Update(ObservableCollection<SingerEntry> rotation)
        {
            ArgumentNullException.ThrowIfNull(rotation);
            _viewModel.UpdateFromRotation(rotation);
        }

        /// <summary>
        /// Toggles the Star Wars crawl in place of the singer list.
        /// </summary>
        public void SetShowCrawl(bool value)
        {
            _viewModel.ShowCrawl = value;
        }

        /// <summary>
        /// Sets the current projection display view mode.
        /// </summary>
        public void SetProjectionView(string viewMode)
        {
            _viewModel.SelectedProjectionView = viewMode;
        }

        /// <summary>
        /// Sets the marquee scroll speed in pixels per second.
        /// </summary>
        public void SetMarqueeSpeed(double pixelsPerSecond)
        {
            _viewModel.MarqueeSpeed = Math.Clamp(pixelsPerSecond, 20, 200);
        }

        /// <summary>
        /// Sets the connection url and QR code image for display.
        /// </summary>
        public void SetConnectionInfo(string url, ImageSource? qrCode)
        {
            _connectionUrl = url ?? string.Empty;
            _qrCodeImage = qrCode;
            _viewModel.ConnectionUrl = _connectionUrl;
            _viewModel.QrCodeImage = _qrCodeImage;
        }

        /// <summary>
        /// Sets the Wi-Fi credentials and QR code image for display.
        /// </summary>
        public void SetWifiInfo(string ssid, string passwordDisplay, ImageSource? wifiQrCode)
        {
            _viewModel.WifiSsid = ssid ?? string.Empty;
            _viewModel.WifiPasswordDisplay = passwordDisplay ?? string.Empty;
            _viewModel.WifiQrCodeImage = wifiQrCode;
        }

        /// <summary>
        /// Toggles or sets pre-show mode on the audience rotation display.
        /// </summary>
        public void SetPreShowMode(bool active)
        {
            _viewModel.IsPreShowModeActive = active;
            Lyracist.Shared.WelcomeScreenService.Instance.IsPreShowActive = active;
        }

        /// <summary>
        /// Controls the background watermark logo opacity.
        /// </summary>
        public void SetWatermarkOpacity(double value)
        {
            _viewModel.WatermarkOpacity = Math.Clamp(value, 0.0, 1.0);
        }

        /// <summary>
        /// Sets the DJ banner file (image or video) shown in the Stadium Jumbotron's sponsor box.
        /// </summary>
        public void SetJumbotronBanner(string? path)
        {
            _viewModel.JumbotronBannerPath = path ?? string.Empty;
        }

        /// <summary>
        /// Turns the "reduced projection effects" mode (fewer particles, no per-element blur) on or off.
        /// </summary>
        public void SetReducedEffects(bool enabled)
        {
            _viewModel.ReducedEffects = enabled;
        }

        /// <summary>
        /// Updates popup banner text, resolving the {venue} and {dj} variables.
        /// </summary>
        /// <param name="template">Raw banner text; may contain {venue} and {dj}.</param>
        /// <param name="venueName">Current venue name substituted for {venue}.</param>
        /// <param name="djName">Current DJ name substituted for {dj}.</param>
        public void SetBannerText(string template, string venueName, string djName)
        {
            string raw = string.IsNullOrWhiteSpace(template) ? "Welcome to Karaoke Night!" : template.Trim();
            string resolved = raw.Replace("{venue}", venueName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase)
                                 .Replace("{dj}", djName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
            _viewModel.BannerText = resolved;
        }

        /// <summary>
        /// Updates the Star Wars crawl banner text, resolving the {venue} and {dj} variables.
        /// </summary>
        /// <param name="template">Raw crawl banner text; may contain {venue} and {dj}.</param>
        /// <param name="venueName">Current venue name substituted for {venue}.</param>
        /// <param name="djName">Current DJ name substituted for {dj}.</param>
        public void SetCrawlBannerText(string template, string venueName, string djName)
        {
            string raw = string.IsNullOrWhiteSpace(template) ? AppSettings.DefaultCrawlBannerText : template.Trim();
            string resolved = raw.Replace("{venue}", venueName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase)
                                 .Replace("{dj}", djName ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
            _viewModel.CrawlBannerText = resolved;
        }

        /// <summary>Raised when the singer display closes while pre-show was on, so the host can clear its toggle.</summary>
        public event Action? PreShowReset;

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (_holidayAutoTimer != null)
            {
                _holidayAutoTimer.Stop();
                _holidayAutoTimer.Tick -= OnHolidayAutoTimerTick;
                _holidayAutoTimer = null;
            }
            // Pre-show lives on this window; leaving it on would keep welcome screens suppressed and queued forever.
            if (_viewModel.IsPreShowModeActive)
            {
                SetPreShowMode(false);
                PreShowReset?.Invoke();
            }
            _window?.Closed -= OnWindowClosed;
            _window = null;
        }

        private bool PositionWindowOnTargetMonitor(Window window, string? overrideDevice = null)
        {
            ArgumentNullException.ThrowIfNull(window);

            Screen[] screens = Screen.AllScreens;
            string targetDevice = !string.IsNullOrEmpty(overrideDevice) ? overrideDevice : GetEffectiveMonitorDevice();
            Screen? targetScreen = WindowPositioner.ResolveByDeviceName(screens, targetDevice);
            if (targetScreen == null)
            {
                return false;
            }

            WindowPositioner.FillArea(window, targetScreen.WorkingArea);

            return screens.Length > 1;
        }
    }
}