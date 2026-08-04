// Edited on Aug 4, 2026 @ 09:55:00 -> Pass dataContext to Show() and set it on the window to support data binding to QR code and connection info
// Edited on Aug 2, 2026 @ 10:14:00 -> Delegate banner updating logic to the window UpdateBanner method
using KSRotation.Windows;
using Lyracist.Shared;
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
            _window.UpdateBanner(_bannerPath);
        }

        public void Show(object dataContext)
        {
            if (_window != null)
            {
                _window.DataContext = dataContext;
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
            _window.DataContext = dataContext;
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
            Screen? targetScreen = WindowPositioner.ResolveByDeviceName(screens, _selectedMonitorDevice);
            if (targetScreen == null)
            {
                return false;
            }

            double windowWidth = window.Width > 0 ? window.Width : 450;
            double windowHeight = window.Height > 0 ? window.Height : 300;
            WindowPositioner.CenterInArea(window, targetScreen.WorkingArea, windowWidth, windowHeight);

            return screens.Length > 1;
        }
    }
}
