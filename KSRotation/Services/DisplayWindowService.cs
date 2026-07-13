// Last Edit: Jul 02, 2026 11:50 - Reused shared default crawl banner text constant for template fallback.
using KSRotation.Models;
using KSRotation.ViewModels;
using KSRotation.Windows;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Screen = System.Windows.Forms.Screen;

namespace KSRotation.Services
{
    public class DisplayWindowService
    {
        private SingerDisplayWindow? _window;
        private readonly DisplayViewModel _viewModel = new();
        private string _connectionUrl = string.Empty;
        private ImageSource? _qrCodeImage;

        /// <summary>
        /// Opens the singer display popup and initializes its data.
        /// </summary>
        /// <param name="rotation">Ordered singer rotation entries.</param>
        public void Show(ObservableCollection<SingerEntry> rotation)
        {
            ArgumentNullException.ThrowIfNull(rotation);

            if (_window != null)
            {
                _window.WindowState = WindowState.Maximized;
                _window.Activate();
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
            PositionWindowOnSecondMonitor(_window);
            _window.Show();
            _window.WindowState = WindowState.Maximized;
            _window.Activate();
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
        }

        /// <summary>
        /// Closes the popup unconditionally when the main window is shutting down.
        /// </summary>
        public void Shutdown()
        {
            if (_window == null)
            {
                return;
            }

            _window.Closed -= OnWindowClosed;
            _window.Close();
            _window = null;
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

        private static bool PositionWindowOnSecondMonitor(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0)
            {
                return false;
            }

            bool isSecondMonitor = screens.Length > 1;
            Screen targetScreen = isSecondMonitor ? screens[1] : screens[0];
            System.Drawing.Rectangle workArea = targetScreen.WorkingArea;

            DpiScale dpi = VisualTreeHelper.GetDpi(window);
            double scaleX = 1.0 / dpi.DpiScaleX;
            double scaleY = 1.0 / dpi.DpiScaleY;

            double windowWidth = window.Width > 0 ? window.Width : 450;
            double windowHeight = window.Height > 0 ? window.Height : 300;
            double workAreaLeft = workArea.Left * scaleX;
            double workAreaTop = workArea.Top * scaleY;
            double workAreaWidth = workArea.Width * scaleX;
            double workAreaHeight = workArea.Height * scaleY;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = workAreaLeft + ((workAreaWidth - windowWidth) / 2);
            window.Top = workAreaTop + ((workAreaHeight - windowHeight) / 2);

            return isSecondMonitor;
        }
    }
}