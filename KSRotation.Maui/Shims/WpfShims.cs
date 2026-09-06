// Edited on Sep 3, 2026 @ 23:56:00 -> Add SetLastRound shim to DisplayWindowService
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KSRotation.Models;
using Lyracist.Shared;

namespace System.Windows.Threading
{
    public class DispatcherTimer
    {
        private readonly System.Timers.Timer _timer;

        public DispatcherTimer()
        {
            _timer = new System.Timers.Timer();
            _timer.AutoReset = false;
            _timer.Elapsed += (_, _) =>
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
            _ = button;
            _ = icon;
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Microsoft.Maui.Controls.Shell.Current?.CurrentPage != null)
                {
                    await Microsoft.Maui.Controls.Shell.Current.CurrentPage.DisplayAlertAsync(caption, messageBoxText, "OK");
                }
            });
            return MessageBoxResult.Yes;
        }

        /// <summary>
        /// Shows a real Yes/No confirmation and awaits the user's actual answer, unlike <see cref="Show"/>
        /// (which fires the alert asynchronously but returns Yes immediately, before the user responds).
        /// Returns false if there's no current page to host the alert on.
        /// </summary>
        public static async Task<bool> ShowConfirmAsync(string messageBoxText, string caption)
        {
            var page = Microsoft.Maui.Controls.Shell.Current?.CurrentPage;
            if (page == null)
            {
                return false;
            }

            return await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(
                () => page.DisplayAlertAsync(caption, messageBoxText, "Yes", "No"));
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
        public void SetLastSongBanner(string? path) { }
        public void SetLastRound(bool isLastRound) { }
        public void SetShowQrCode(bool show) { }
        public void SetShowEstimatedWaitTime(bool show) { }
        public void RepositionWindow() { }
        public Task<bool> MoveRotationTo(DisplayTarget target) => Task.FromResult(true);
        public Task StopCastingAsync() => Task.CompletedTask;
        public Lyracist.Shared.ChromecastDevice? SelectedDevice { get; set; }
    }

    public static class ThemeService
    {
        public static void Apply(string theme)
        {
            // Optional: Map dynamic app theme mapping if needed.
        }
    }

    public class DjBannerWindowService
    {
        public void SetSelectedMonitor(string deviceName) { }
        public void SetBannerPath(string path) { }
        public void Show(object? dataContext = null) { }
        public void Hide() { }
        public void Shutdown() { }
        public void RepositionWindow() { }
    }
}
