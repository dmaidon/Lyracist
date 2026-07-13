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
        public static void Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Microsoft.Maui.Controls.Shell.Current?.CurrentPage != null)
                {
                    await Microsoft.Maui.Controls.Shell.Current.CurrentPage.DisplayAlertAsync(caption, messageBoxText, "OK");
                }
            });
        }
    }

    public enum MessageBoxButton
    {
        OK
    }

    public enum MessageBoxImage
    {
        Information,
        Warning,
        Error
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
    }

    public static class ThemeService
    {
        public static void Apply(string theme)
        {
            // Optional: Map dynamic app theme mapping if needed.
        }
    }

    public static class RotationReportService
    {
        // Not implemented on this platform yet. Throwing (rather than returning fake success paths)
        // means SaveRotation()'s existing catch block reports a clear error instead of telling the
        // operator their PDF/CSV report was saved when nothing was written.
        public static Task<(string pdfPath, string csvPath)> SaveAsync(
            System.Collections.Generic.List<SingerEntry> singers,
            System.Collections.Generic.List<SongPerformance> history,
            string venueName,
            string emailRecipient,
            bool sendEmail)
        {
            throw new PlatformNotSupportedException("Saving rotation reports (PDF/CSV) is not yet implemented on this platform.");
        }
    }
}
