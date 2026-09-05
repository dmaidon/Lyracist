// Edited on Sep 3, 2026 @ 23:50:55 -> Add SetLastRound method to DisplayWindowService
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
        private string? _lastSongBannerPath;

        public void SetLastRound(bool isLastRound)
        {
            _viewModel.IsLastRound = isLastRound;
        }

        public void SetShowQrCode(bool show)
        {
            _viewModel.ShowQrCode = show;
        }

        public void SetLastSongBanner(string? path)
        {
            _lastSongBannerPath = path;
            if (_window != null)
            {
                _window.Dispatcher.InvokeAsync(() => _window.UpdateLastSongBanner(_lastSongBannerPath));
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

            _window.UpdateLastSongBanner(_lastSongBannerPath);

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
        /// Controls the background watermark logo opacity.
        /// </summary>
        public void SetWatermarkOpacity(double value)
        {
            _viewModel.WatermarkOpacity = Math.Clamp(value, 0.0, 1.0);
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

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _window?.Closed -= OnWindowClosed;
            _window = null;
        }

        private bool PositionWindowOnTargetMonitor(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            Screen[] screens = Screen.AllScreens;
            Screen? targetScreen = WindowPositioner.ResolveByDeviceName(screens, _selectedMonitorDevice);
            if (targetScreen == null)
            {
                return false;
            }

            WindowPositioner.FillArea(window, targetScreen.WorkingArea);

            return screens.Length > 1;
        }
    }
}