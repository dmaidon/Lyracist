// Created on Aug 1, 2026 @ 09:46:20 -> Added DJ Banner window service
using KSRotation.Windows;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Screen = System.Windows.Forms.Screen;

namespace KSRotation.Services
{
    public class DjBannerWindowService
    {
        private DjBannerWindow? _window;
        private string _selectedMonitorDevice = string.Empty;
        private string _bannerPath = string.Empty;

        public void SetSelectedMonitor(string deviceName)
        {
            _selectedMonitorDevice = deviceName ?? string.Empty;
        }

        public void SetBannerPath(string path)
        {
            _bannerPath = path ?? string.Empty;
            if (_window != null)
            {
                UpdateImage();
            }
        }

        private void UpdateImage()
        {
            if (_window == null) return;
            if (string.IsNullOrEmpty(_bannerPath) || !File.Exists(_bannerPath))
            {
                _window.BannerImage.Source = null;
                return;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(_bannerPath);
                image.EndInit();
                image.Freeze();
                _window.BannerImage.Source = image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load DJ banner: {ex.Message}");
                _window.BannerImage.Source = null;
            }
        }

        public void Show()
        {
            if (_window != null)
            {
                // DjBannerWindow.OnClosing cancels the close and calls its own Hide() instead
                // (so the singleton window survives the user clicking its own [X]) - which means
                // _window is never actually destroyed and Closed never fires, so this branch runs
                // on every re-show, not just the first. WindowState/Activate() alone cannot bring
                // a Visibility.Hidden window back - only Show() actually does that.
                _window.Show();
                _window.WindowState = WindowState.Maximized;
                _window.Activate();
                return;
            }

            _window = new DjBannerWindow();
            _window.Closed += OnWindowClosed;

            UpdateImage();
            PositionWindowOnTargetMonitor(_window);
            _window.Show();
            _window.WindowState = WindowState.Maximized;
            _window.Activate();
        }

        public void Hide()
        {
            if (_window == null) return;
            _window.Close();
        }

        public void Shutdown()
        {
            if (_window == null) return;
            _window.Closed -= OnWindowClosed;
            _window.Close();
            _window = null;
        }

        public void RepositionWindow()
        {
            if (_window == null) return;
            var previousState = _window.WindowState;
            _window.WindowState = WindowState.Normal;
            PositionWindowOnTargetMonitor(_window);
            _window.WindowState = previousState;
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (_window != null)
            {
                _window.Closed -= OnWindowClosed;
                _window = null;
            }
        }

        private bool PositionWindowOnTargetMonitor(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0) return false;

            Screen targetScreen = screens[0];
            if (!string.IsNullOrEmpty(_selectedMonitorDevice))
            {
                var matched = screens.FirstOrDefault(s => string.Equals(s.DeviceName, _selectedMonitorDevice, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    targetScreen = matched;
                }
                else
                {
                    bool isSecondMonitor = screens.Length > 1;
                    targetScreen = isSecondMonitor ? screens[1] : screens[0];
                }
            }
            else
            {
                bool isSecondMonitor = screens.Length > 1;
                targetScreen = isSecondMonitor ? screens[1] : screens[0];
            }

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

            return screens.Length > 1;
        }
    }
}
