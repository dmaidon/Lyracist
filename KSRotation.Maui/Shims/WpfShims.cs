// Edited on Sep 17, 2026 @ 12:06:45 -> Add Invoke and InvokeAsync shims to Dispatcher for shared MainViewModel compatibility
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KSRotation.Models;
using Lyracist.Shared;
using KSRotation.Maui.Services;

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

        public Task InvokeAsync(Action action)
        {
            return Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(action);
        }

        public void Invoke(Action action)
        {
            Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronous WPF-style Dispatcher.Invoke, needed so code shared with the WPF apps (e.g.
        /// MainViewModel.IsSongInCurrentSession's thread-safe collection reads) compiles here too.
        /// MainThread.InvokeOnMainThreadAsync already runs the callback inline with no dispatch when
        /// called from the main thread itself, matching WPF's own "no-op passthrough" behavior for
        /// Invoke called on the UI thread - GetAwaiter().GetResult() only actually blocks when called
        /// from a background thread.
        /// </summary>
        public T Invoke<T>(Func<T> func)
        {
            return Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(func).GetAwaiter().GetResult();
        }

        /// <summary>WPF-style CheckAccess, so shared code can skip marshaling entirely when already on
        /// the main thread instead of always paying for a dispatch (or hanging, if some caller ever
        /// invoked this from a thread MAUI has no pump running for - MainThread's is always live).</summary>
        public bool CheckAccess() => Microsoft.Maui.ApplicationModel.MainThread.IsMainThread;
    }
}

namespace KSRotation.Services
{
    /// <summary>
    /// MAUI-side counterpart to KSRotation/Services/ConfirmationService.cs (that file is WPF-only and
    /// not linked into this project) - same signature so shared code (e.g. MainViewModel's session
    /// handoff import) can call KSRotation.Services.ConfirmationService.ShowYesNoAsync on either platform.
    /// </summary>
    public static class ConfirmationService
    {
        public static Task<bool> ShowYesNoAsync(string message, string caption) =>
            System.Windows.MessageBox.ShowConfirmAsync(message, caption);
    }

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
        public Task StopCastingAsync() => MauiCastingService.Instance.StopCastingAsync();
        public Lyracist.Shared.ChromecastDevice? SelectedDevice
        {
            get => MauiCastingService.Instance.ActiveDevice;
            set { }
        }
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
