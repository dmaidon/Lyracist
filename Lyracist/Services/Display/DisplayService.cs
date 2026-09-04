// Edited on Sep 3, 2026 @ 23:55:00 -> Add SetLastRound method to DisplayService
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
using Lyracist.Trivia.Core.Services;

namespace Lyracist.Services.Display;

public class DisplayService : IDisplayService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediaEngine _mediaEngine;
    private readonly ICastingService _casting;
    private RotationWindow? _rotationWindow;
    private LyricsWindow? _lyricsWindow;
    private DjBannerWindow? _djBannerWindow;
    private TriviaGameEngine? _triviaEngine;
    private readonly DisplayPreferences _preferences;
    private bool _rotationHadSingers;

    public void SetTriviaGameEngine(TriviaGameEngine? engine) => _triviaEngine = engine;
    public TriviaGameEngine? GetTriviaGameEngine() => _triviaEngine;


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
        var list = new List<ScreenInfo>();

        foreach (var monitor in MonitorEnumerator.GetMonitors())
        {
            list.Add(new ScreenInfo
            {
                Index = monitor.Index,
                DeviceName = monitor.DeviceName,
                IsPrimary = monitor.IsPrimary,
                Bounds = new Rect(monitor.X, monitor.Y, monitor.Width, monitor.Height)
            });
        }

        return list;
    }

    public bool IsLyricsActive
    {
        get => _preferences.IsLyricsActive;
        set
        {
            if (_preferences.IsLyricsActive != value)
            {
                _preferences.IsLyricsActive = value;
                DisplayPreferencesStore.Save(_preferences);
                UpdateWindowVisibilities();
                ScreenAssignmentsChanged?.Invoke();
            }
        }
    }

    public bool IsRotationActive
    {
        get => _preferences.IsRotationActive;
        set
        {
            if (_preferences.IsRotationActive != value)
            {
                _preferences.IsRotationActive = value;
                DisplayPreferencesStore.Save(_preferences);
                UpdateWindowVisibilities();
                ScreenAssignmentsChanged?.Invoke();
            }
        }
    }

    public bool IsDjBannerActive
    {
        get => _preferences.IsDjBannerActive;
        set
        {
            if (_preferences.IsDjBannerActive != value)
            {
                _preferences.IsDjBannerActive = value;
                DisplayPreferencesStore.Save(_preferences);
                UpdateWindowVisibilities();
                ScreenAssignmentsChanged?.Invoke();
            }
        }
    }

    public void ShowLyricsWindow() => IsLyricsActive = true;
    public void ShowRotationWindow() => IsRotationActive = true;
    public void ShowDjBannerWindow() => IsDjBannerActive = true;
    public void HideDjBannerWindow() => IsDjBannerActive = false;

    public void HideLyricsWindow() => IsLyricsActive = false;
    public void HideRotationWindow() => IsRotationActive = false;

    private void ShowLyricsWindowInternal()
    {
        bool isNewWindow = _lyricsWindow == null || !_lyricsWindow.IsLoaded;
        if (isNewWindow)
        {
            _lyricsWindow = _serviceProvider.GetRequiredService<LyricsWindow>();
            var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
            if (vm != null)
            {
                vm.IsMirrored = _preferences.IsLyricsMirrored;
            }
        }
        _lyricsWindow!.Show();
    }

    private void HideLyricsWindowInternal()
    {
        if (_lyricsWindow != null && _lyricsWindow.IsLoaded)
        {
            _lyricsWindow.Hide();
        }
    }

    private void ShowRotationWindowInternal()
    {
        bool isNewWindow = _rotationWindow == null || !_rotationWindow.IsLoaded;
        if (isNewWindow)
        {
            _rotationWindow = _serviceProvider.GetRequiredService<RotationWindow>();
            var vm = _serviceProvider.GetService<RotationWindowViewModel>();
            if (vm != null)
            {
                vm.SelectedProjectionView = _preferences.RotationViewMode ?? "Normal List";
                vm.CrawlBannerText = Core.Helpers.AppSettings.GetActiveCrawlBannerTemplate();
            }
        }
        _rotationWindow!.Show();
        UpdateRotationLastSongBanner();
    }

    private void HideRotationWindowInternal()
    {
        if (_rotationWindow != null && _rotationWindow.IsLoaded)
        {
            _rotationWindow.Hide();
        }
    }

    private void ShowDjBannerWindowInternal()
    {
        bool isNewWindow = _djBannerWindow == null || !_djBannerWindow.IsLoaded;
        if (isNewWindow)
        {
            _djBannerWindow = _serviceProvider.GetRequiredService<DjBannerWindow>();
        }
        _djBannerWindow!.Show();
        UpdateDjBanner(_preferences.SelectedDjBannerPath);
    }

    private void HideDjBannerWindowInternal()
    {
        if (_djBannerWindow != null && _djBannerWindow.IsLoaded)
        {
            _djBannerWindow.Hide();
        }
    }

    public void MoveRotationToScreen(int? screenIndex)
    {
        _preferences.RotationScreenIndex = screenIndex;
        DisplayPreferencesStore.Save(_preferences);
        UpdateWindowVisibilities();
        ScreenAssignmentsChanged?.Invoke();
    }

    public void MoveLyricsToScreen(int? screenIndex)
    {
        _preferences.LyricsScreenIndex = screenIndex;
        DisplayPreferencesStore.Save(_preferences);
        UpdateWindowVisibilities();
        ScreenAssignmentsChanged?.Invoke();
    }

    public void MoveDjBannerToScreen(int? screenIndex)
    {
        _preferences.DjBannerScreenIndex = screenIndex;
        DisplayPreferencesStore.Save(_preferences);
        UpdateWindowVisibilities();
        ScreenAssignmentsChanged?.Invoke();
    }

    private string ResolveActiveBannerPath()
    {
        if (!string.IsNullOrEmpty(_preferences.SelectedSpecialEvent) && 
            !_preferences.SelectedSpecialEvent.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            if (_preferences.SelectedSpecialEvent.Equals("Last Song", StringComparison.OrdinalIgnoreCase))
            {
                string lastSongPath = System.IO.Path.Combine(Globals.EventBannersDir, "LastSong.png");
                if (System.IO.File.Exists(lastSongPath))
                {
                    return lastSongPath;
                }
            }

            var eventConfig = Lyracist.Core.Helpers.AppSettings.SpecialEvents.FirstOrDefault(e => e.EventName.Equals(_preferences.SelectedSpecialEvent, StringComparison.OrdinalIgnoreCase));
            if (eventConfig != null)
            {
                string fullPath = System.IO.Path.Combine(Globals.EventBannersDir, eventConfig.BannerFileName);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        return _preferences.SelectedDjBannerPath;
    }

    private void UpdateRotationLastSongBanner()
    {
        if (_rotationWindow != null && _rotationWindow.IsLoaded)
        {
            bool isLastSong = !string.IsNullOrEmpty(_preferences.SelectedSpecialEvent) &&
                              _preferences.SelectedSpecialEvent.Equals("Last Song", StringComparison.OrdinalIgnoreCase);

            string lastSongPath = System.IO.Path.Combine(Globals.EventBannersDir, "LastSong.png");
            string? activePath = (isLastSong && System.IO.File.Exists(lastSongPath)) ? lastSongPath : null;

            _rotationWindow.Dispatcher.InvokeAsync(() => _rotationWindow.UpdateLastSongBanner(activePath));
        }
    }

    public void RestoreAssignments()
    {
        var target = _preferences.RotationTarget;
        if (target != DisplayTarget.Monitor && target != DisplayTarget.WirelessHDMI)
        {
            Task.Run(async () => await MoveRotationTo(target));
        }

        SetLyricsMirror(_preferences.IsLyricsMirrored);

        string activePath = ResolveActiveBannerPath();
        var vm = _serviceProvider.GetService<DjBannerWindowViewModel>();
        vm?.UpdateBanner(activePath);
        UpdateRotationLastSongBanner();

        UpdateWindowVisibilities();
        ScreenAssignmentsChanged?.Invoke();
    }

    public void UpdateDjBanner(string path)
    {
        _preferences.SelectedDjBannerPath = path;
        DisplayPreferencesStore.Save(_preferences);

        var vm = _serviceProvider.GetService<DjBannerWindowViewModel>();
        vm?.UpdateBanner(ResolveActiveBannerPath());
    }

    public void UpdateSpecialEvent(string eventName)
    {
        _preferences.SelectedSpecialEvent = eventName;
        DisplayPreferencesStore.Save(_preferences);

        var vm = _serviceProvider.GetService<DjBannerWindowViewModel>();
        vm?.UpdateBanner(ResolveActiveBannerPath());
        UpdateRotationLastSongBanner();
        ScreenAssignmentsChanged?.Invoke();
    }

    public void RefreshActiveBanner()
    {
        var vm = _serviceProvider.GetService<DjBannerWindowViewModel>();
        vm?.UpdateBanner(ResolveActiveBannerPath());
        UpdateRotationLastSongBanner();
    }

    public DisplayPreferences GetPreferences() => _preferences;

    public void UpdateWindowVisibilities()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        if (!dispatcher.CheckAccess())
        {
            dispatcher.Invoke(UpdateWindowVisibilities);
            return;
        }

        int? lyricsScreen = _preferences.LyricsScreenIndex;
        int? rotationScreen = _preferences.RotationScreenIndex;
        int? djBannerScreen = _preferences.DjBannerScreenIndex;

        bool lyricsActive = _preferences.IsLyricsActive;
        bool rotationActive = _preferences.IsRotationActive;
        bool djBannerActive = _preferences.IsDjBannerActive;

        // Lyrics projection: priority 1
        bool showLyrics = lyricsActive && lyricsScreen.HasValue && lyricsScreen.Value >= 0;

        // Rotation queue: priority 2
        bool showRotation = rotationActive;
        bool isLocalRotation = _preferences.RotationTarget == DisplayTarget.Monitor || _preferences.RotationTarget == DisplayTarget.WirelessHDMI;

        if (showRotation)
        {
            if (isLocalRotation)
            {
                if (!rotationScreen.HasValue || rotationScreen.Value < 0)
                {
                    showRotation = false;
                }
                else if (showLyrics && lyricsScreen == rotationScreen)
                {
                    showRotation = false;
                }
            }
        }

        // DJ Banner: priority 3
        bool showDjBanner = djBannerActive && djBannerScreen.HasValue && djBannerScreen.Value >= 0;
        if (showDjBanner)
        {
            if (showLyrics && lyricsScreen == djBannerScreen)
            {
                showDjBanner = false;
            }
            else if (showRotation && isLocalRotation && rotationScreen == djBannerScreen)
            {
                showDjBanner = false;
            }
        }

        // Apply Lyrics Display
        if (showLyrics)
        {
            ShowLyricsWindowInternal();
            MoveWindowToScreen(_lyricsWindow!, lyricsScreen!.Value);
        }
        else
        {
            HideLyricsWindowInternal();
        }

        // Apply Rotation Display
        if (showRotation)
        {
            if (isLocalRotation)
            {
                ShowRotationWindowInternal();
                MoveWindowToScreen(_rotationWindow!, rotationScreen!.Value);
            }
            else
            {
                EnsureRotationWindowOffScreen();
            }
        }
        else
        {
            HideRotationWindowInternal();
        }

        // Apply DJ Banner Display
        if (showDjBanner)
        {
            ShowDjBannerWindowInternal();
            MoveWindowToScreen(_djBannerWindow!, djBannerScreen!.Value);
        }
        else
        {
            HideDjBannerWindowInternal();
        }

        // Auto-pause Trivia Game when special events, DJ banner, lyrics, or rotation take over the screen
        bool isScreenOccupied = showLyrics || showRotation || showDjBanner ||
            (!string.IsNullOrEmpty(_preferences.SelectedSpecialEvent) && !_preferences.SelectedSpecialEvent.Equals("None", StringComparison.OrdinalIgnoreCase));

        if (isScreenOccupied)
        {
            string reason = showLyrics ? "Karaoke Performance" :
                            showDjBanner ? "DJ Banner Display" :
                            showRotation ? "Rotation Display" : "Special Event";
            _triviaEngine?.PauseGame(reason);
        }
        else
        {
            _triviaEngine?.ResumeGame();
        }
    }

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

        WindowPositioner.FillArea(window, screen.Bounds);

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

    public void SetLastRound(bool isLastRound)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        if (vm != null)
        {
            vm.IsLastRound = isLastRound;
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