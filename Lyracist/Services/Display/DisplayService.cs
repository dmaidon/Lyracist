// Edited on Aug 1, 2026 @ 16:11:00 -> Import Lyracist.Shared in DisplayService
// Edited on Aug 1, 2026 @ 16:10:00 -> Update MoveRotationTo to discover Chromecast device when targeting Chromecast
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.ViewModels;
using Lyracist.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Drawing; // For Screen bounds if needed, or fallback
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Shared;

// Edited on Aug 1, 2026 @ 12:11:00 -> Add Casting support integration to DisplayService
namespace Lyracist.Services.Display;

public class DisplayService : IDisplayService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediaEngine _mediaEngine;
    private readonly ICastingService _casting;
    private RotationWindow? _rotationWindow;
    private LyricsWindow? _lyricsWindow;
    private DjBannerWindow? _djBannerWindow;
    private readonly DisplayPreferences _preferences;
    private bool _rotationHadSingers;
    private int? _autoDisabledDjBannerScreenIndex;


    public event Action? RotationCompleted;

    public event Action? RotationResumed;

    public event Action? ScreenAssignmentsChanged;

    public DisplayService(IServiceProvider serviceProvider, IMediaEngine mediaEngine, ICastingService casting)
    {
        _serviceProvider = serviceProvider;
        _mediaEngine = mediaEngine;
        _casting = casting;
        _preferences = DisplayPreferencesStore.Load();

        // Auto-subscribe to the MediaEngine's frame tick events to sync with the LyricsWindow VM
        _mediaEngine.FrameReady += OnMediaFrameReady;
    }

    private void OnMediaFrameReady(System.Windows.Media.ImageSource frame)
    {
        UpdateLyricsFrame(frame);
    }

    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var list = new List<ScreenInfo>();

        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            list.Add(new ScreenInfo
            {
                Index = i,
                DeviceName = s.DeviceName,
                IsPrimary = s.Primary,
                Bounds = new Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)
            });
        }

        return list;
    }

    public void ShowRotationWindow()
    {
        if (_preferences.RotationTarget != DisplayTarget.Monitor && _preferences.RotationTarget != DisplayTarget.WirelessHDMI)
        {
            return;
        }
        if (!_preferences.RotationScreenIndex.HasValue)
        {
            return;
        }
        bool isNewWindow = _rotationWindow == null || !_rotationWindow.IsLoaded;
        if (isNewWindow)
        {
            _rotationWindow = _serviceProvider.GetRequiredService<RotationWindow>();
        }
        _rotationWindow!.Show();

        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        if (vm != null)
        {
            vm.SelectedProjectionView = _preferences.RotationViewMode ?? "Normal List";
            vm.CrawlBannerText = Core.Helpers.AppSettings.GetActiveCrawlBannerTemplate();
        }

        if (isNewWindow)
        {
            MoveWindowToScreen(_rotationWindow, _preferences.RotationScreenIndex.Value);
        }
    }

    public void HideRotationWindow()
    {
        if (_rotationWindow != null && _rotationWindow.IsLoaded)
        {
            _rotationWindow.Hide();
        }
    }

    public void ShowLyricsWindow()
    {
        if (!_preferences.LyricsScreenIndex.HasValue)
        {
            return;
        }
        bool isNewWindow = _lyricsWindow == null || !_lyricsWindow.IsLoaded;
        if (isNewWindow)
        {
            _lyricsWindow = _serviceProvider.GetRequiredService<LyricsWindow>();
        }
        _lyricsWindow!.Show();

        if (isNewWindow)
        {
            var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
            vm?.IsMirrored = _preferences.IsLyricsMirrored;

            MoveWindowToScreen(_lyricsWindow, _preferences.LyricsScreenIndex.Value);
        }
    }

    public void HideLyricsWindow()
    {
        if (_lyricsWindow != null && _lyricsWindow.IsLoaded)
        {
            _lyricsWindow.Hide();
        }
    }

    public void MoveRotationToScreen(int? screenIndex)
    {
        if (screenIndex.HasValue && screenIndex.Value >= 0)
        {
            _preferences.RotationScreenIndex = screenIndex;
            ShowRotationWindow();
            MoveWindowToScreen(_rotationWindow!, screenIndex.Value);

            if (_preferences.DjBannerScreenIndex == screenIndex)
            {
                _autoDisabledDjBannerScreenIndex = screenIndex;
                _preferences.DjBannerScreenIndex = null;
                HideDjBannerWindow();
            }
        }
        else
        {
            _preferences.RotationScreenIndex = null;
            HideRotationWindow();
        }

        // Restore DJ Banner if conflict is resolved
        if (_autoDisabledDjBannerScreenIndex.HasValue && _preferences.RotationScreenIndex != _autoDisabledDjBannerScreenIndex)
        {
            int restoreIndex = _autoDisabledDjBannerScreenIndex.Value;
            _autoDisabledDjBannerScreenIndex = null;
            MoveDjBannerToScreen(restoreIndex);
        }

        DisplayPreferencesStore.Save(_preferences);
        ScreenAssignmentsChanged?.Invoke();
    }

    public void MoveLyricsToScreen(int? screenIndex)
    {
        if (screenIndex.HasValue && screenIndex.Value >= 0)
        {
            _preferences.LyricsScreenIndex = screenIndex;
            ShowLyricsWindow();
            MoveWindowToScreen(_lyricsWindow!, screenIndex.Value);
        }
        else
        {
            _preferences.LyricsScreenIndex = null;
            HideLyricsWindow();
        }
        DisplayPreferencesStore.Save(_preferences);
        ScreenAssignmentsChanged?.Invoke();
    }

    public void RestoreAssignments()
    {
        var target = _preferences.RotationTarget;
        if (target != DisplayTarget.Monitor && target != DisplayTarget.WirelessHDMI)
        {
            Task.Run(async () => await MoveRotationTo(target));
        }
        else
        {
            if (_preferences.RotationScreenIndex.HasValue)
            {
                MoveRotationToScreen(_preferences.RotationScreenIndex.Value);
            }
            else
            {
                HideRotationWindow();
            }
        }

        if (_preferences.LyricsScreenIndex.HasValue)
        {
            MoveLyricsToScreen(_preferences.LyricsScreenIndex.Value);
        }
        else
        {
            HideLyricsWindow();
        }

        if (_preferences.DjBannerScreenIndex.HasValue)
        {
            if (_preferences.RotationScreenIndex == _preferences.DjBannerScreenIndex)
            {
                _autoDisabledDjBannerScreenIndex = _preferences.DjBannerScreenIndex;
                _preferences.DjBannerScreenIndex = null;
                HideDjBannerWindow();
                DisplayPreferencesStore.Save(_preferences);
            }
            else
            {
                _autoDisabledDjBannerScreenIndex = null;
                MoveDjBannerToScreen(_preferences.DjBannerScreenIndex.Value);
            }
        }
        else
        {
            _autoDisabledDjBannerScreenIndex = null;
            HideDjBannerWindow();
        }

        SetLyricsMirror(_preferences.IsLyricsMirrored);
        ScreenAssignmentsChanged?.Invoke();
    }

    public void ShowDjBannerWindow()
    {
        if (!_preferences.DjBannerScreenIndex.HasValue)
        {
            return;
        }
        bool isNewWindow = _djBannerWindow == null || !_djBannerWindow.IsLoaded;
        if (isNewWindow)
        {
            _djBannerWindow = _serviceProvider.GetRequiredService<DjBannerWindow>();
        }
        _djBannerWindow!.Show();

        if (isNewWindow)
        {
            MoveWindowToScreen(_djBannerWindow, _preferences.DjBannerScreenIndex.Value);
        }

        UpdateDjBanner(_preferences.SelectedDjBannerPath);
    }

    public void HideDjBannerWindow()
    {
        if (_djBannerWindow != null && _djBannerWindow.IsLoaded)
        {
            _djBannerWindow.Hide();
        }
    }

    public void MoveDjBannerToScreen(int? screenIndex)
    {
        if (screenIndex.HasValue && screenIndex.Value >= 0)
        {
            if (_preferences.RotationScreenIndex == screenIndex)
            {
                _autoDisabledDjBannerScreenIndex = screenIndex;
                _preferences.DjBannerScreenIndex = null;
                HideDjBannerWindow();
            }
            else
            {
                _autoDisabledDjBannerScreenIndex = null;
                _preferences.DjBannerScreenIndex = screenIndex;
                ShowDjBannerWindow();
                MoveWindowToScreen(_djBannerWindow!, screenIndex.Value);
            }
        }
        else
        {
            _autoDisabledDjBannerScreenIndex = null;
            _preferences.DjBannerScreenIndex = null;
            HideDjBannerWindow();
        }
        DisplayPreferencesStore.Save(_preferences);
        ScreenAssignmentsChanged?.Invoke();
    }

    public void UpdateDjBanner(string path)
    {
        _preferences.SelectedDjBannerPath = path;
        DisplayPreferencesStore.Save(_preferences);

        var vm = _serviceProvider.GetService<DjBannerWindowViewModel>();
        vm?.UpdateBanner(path);
    }

    public DisplayPreferences GetPreferences() => _preferences;

    public void FullscreenRotation()
    {
        if (_rotationWindow != null && _rotationWindow.IsLoaded)
        {
            _rotationWindow.WindowState = WindowState.Maximized;
        }
    }

    public void FullscreenLyrics()
    {
        if (_lyricsWindow != null && _lyricsWindow.IsLoaded)
        {
            _lyricsWindow.WindowState = WindowState.Maximized;
        }
    }

    private void MoveWindowToScreen(Window window, int screenIndex)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screenIndex < 0 || screenIndex >= screens.Length) return;

        var screen = screens[screenIndex];

        // Apply placement settings
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = screen.Bounds.X;
        window.Top = screen.Bounds.Y;
        window.Width = screen.Bounds.Width;
        window.Height = screen.Bounds.Height;

        // Apply borderless and projection settings
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.Topmost = true;

        // Refresh/maximize window asynchronously on UI thread
        window.Dispatcher.InvokeAsync(() =>
        {
            window.WindowState = WindowState.Normal;
            window.WindowState = WindowState.Maximized;
        });
    }

    public void UpdateRotation(List<Singer> singers)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        vm?.UpdateRotation(singers);

        bool hasSingers = singers.Count > 0;
        if (_rotationHadSingers && !hasSingers)
        {
            RotationCompleted?.Invoke();
        }
        else if (!_rotationHadSingers && hasSingers)
        {
            RotationResumed?.Invoke();
        }
        _rotationHadSingers = hasSingers;
    }

    public void HighlightSinger(Singer singer)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        vm?.HighlightSinger(singer);
    }

    public void UpdateLyricsFrame(System.Windows.Media.ImageSource frame)
    {
        var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
        vm?.UpdateFrame(frame);
    }

    public void SetLyricsMirror(bool mirrored)
    {
        var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
        vm?.IsMirrored = mirrored;

        _preferences.IsLyricsMirrored = mirrored;
        DisplayPreferencesStore.Save(_preferences);
    }

    public void SetLyricsFallbackText(string text)
    {
        var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
        vm?.ShowFallback(text);
    }

    public void ShowLyricsOverlay(string text, int seconds = 8)
    {
        ShowLyricsWindow();
        var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
        vm?.ShowOverlay(text, seconds);
    }

    public void SetRotationAnnouncement(string message, bool visible)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        if (vm != null)
        {
            vm.AnnouncementBanner = message;
            vm.IsAnnouncementVisible = visible;
        }
    }

    public void SetRotationViewMode(string mode)
    {
        _preferences.RotationViewMode = mode;
        DisplayPreferencesStore.Save(_preferences);

        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        vm?.SelectedProjectionView = mode;
    }

    public void SetCrawlBannerText(string text)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        vm?.CrawlBannerText = text;
    }

    public async Task<bool> MoveRotationTo(DisplayTarget target, ChromecastDevice? device = null)
    {
        _preferences.RotationTarget = target;
        DisplayPreferencesStore.Save(_preferences);

        if (target != DisplayTarget.Monitor && target != DisplayTarget.WirelessHDMI)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                EnsureRotationWindowOffScreen();
            }
            else
            {
                await dispatcher.InvokeAsync(EnsureRotationWindowOffScreen);
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

        if (target == DisplayTarget.Chromecast)
        {
            var tv = device;
            if (tv == null)
            {
                var discovery = _serviceProvider.GetRequiredService<IChromecastDiscoveryService>();
                var devices = await discovery.DiscoverAsync();
                tv = devices.FirstOrDefault();
            }
            if (tv == null) return false;

            return await _casting.CastRotationAsync(target, tv);
        }

        return await _casting.CastRotationAsync(target);
    }

    private void EnsureRotationWindowOffScreen()
    {
        bool isNewWindow = _rotationWindow == null || !_rotationWindow.IsLoaded;
        if (isNewWindow)
        {
            _rotationWindow = _serviceProvider.GetRequiredService<RotationWindow>();
        }
        
        var window = _rotationWindow!;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        window.Width = 1920;
        window.Height = 1080;
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.Show();
    }

    private void RestoreLocalMonitorWindow()
    {
        if (_preferences.RotationScreenIndex.HasValue)
        {
            MoveRotationToScreen(_preferences.RotationScreenIndex.Value);
        }
        else
        {
            HideRotationWindow();
        }
    }

    public async Task StopRotationCasting()
    {
        await _casting.StopCastingAsync();
        
        _preferences.RotationTarget = DisplayTarget.Monitor;
        DisplayPreferencesStore.Save(_preferences);

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
}