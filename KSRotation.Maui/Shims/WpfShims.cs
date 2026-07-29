// Edited on Jul 27, 2026 @ 22:45:00 -> Add SetSelectedMonitor and RepositionWindow shims to DisplayWindowService
// Created on Jul 27, 2026 @ 13:50:00 -> Update MessageBox shim to support YesNo buttons and MessageBoxResult for compilation compatibility
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KSRotation.Models;

namespace System.Windows.Threading
{
    public class DispatcherTimer
    {
        private readonly System.Timers.Timer _timer;

        public DispatcherTimer()
        {
            _timer = new System.Timers.Timer();
            _timer.AutoReset = false;
            _timer.Elapsed += (s, e) =>
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    Tick?.Invoke(this, EventArgs.Empty);
                });
            };
        }

        public TimeSpan Interval
        {
            get => TimeSpan.FromMilliseconds(_timer.Interval);
            set => _timer.Interval = value.TotalMilliseconds;
        }

        public event EventHandler? Tick;

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
    }
}

namespace System.Windows
{
    public static class MessageBox
    {
        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Microsoft.Maui.Controls.Shell.Current?.CurrentPage != null)
                {
                    await Microsoft.Maui.Controls.Shell.Current.CurrentPage.DisplayAlertAsync(caption, messageBoxText, "OK");
                }
            });
            return MessageBoxResult.Yes;
        }
    }

    public enum MessageBoxButton
    {
        OK,
        YesNo
    }

    public enum MessageBoxImage
    {
        Information,
        Warning,
        Error,
        Question
    }

    public enum MessageBoxResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    public class Application
    {
        public static Application Current { get; } = new Application();
        public Dispatcher Dispatcher { get; } = new Dispatcher();
    }

    public class Dispatcher
    {
        public void BeginInvoke(Action action)
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(action);
        }

        public void BeginInvoke(Delegate method)
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                method.DynamicInvoke();
            });
        }
    }
}

namespace KSRotation.Services
{
    public class DisplayWindowService
    {
        public void Show(ObservableCollection<SingerEntry> rotation) { }
        public void Hide() { }
        public void Shutdown() { }
        public void Update(ObservableCollection<SingerEntry> rotation) { }
        public void SetShowCrawl(bool value) { }
        public void SetProjectionView(string viewMode) { }
        public void SetMarqueeSpeed(double pixelsPerSecond) { }
        public void SetConnectionInfo(string url, Microsoft.Maui.Controls.ImageSource? qrCode) { }
        public void SetWatermarkOpacity(double value) { }
        public void SetBannerText(string template, string venueName, string djName) { }
        public void SetCrawlBannerText(string template, string venueName, string djName) { }
        public void SetSelectedMonitor(string deviceName) { }
        public void RepositionWindow() { }
    }

    public static class ThemeService
    {
        public static void Apply(string theme)
        {
            // Optional: Map dynamic app theme mapping if needed.
        }
    }
}
