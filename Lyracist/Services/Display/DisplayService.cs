using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.ViewModels;
using Lyracist.Windows;

namespace Lyracist.Services.Display;

public class DisplayService : IDisplayService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediaEngine _mediaEngine;
    private RotationWindow? _rotationWindow;
    private LyricsWindow? _lyricsWindow;
    private readonly DisplayPreferences _preferences;
    private bool _rotationHadSingers;

    public event Action? RotationCompleted;

    public event Action? RotationResumed;

    public DisplayService(IServiceProvider serviceProvider, IMediaEngine mediaEngine)
    {
        _serviceProvider = serviceProvider;
        _mediaEngine = mediaEngine;
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
        }
        else
        {
            _preferences.RotationScreenIndex = null;
            HideRotationWindow();
        }
        DisplayPreferencesStore.Save(_preferences);
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
    }

    public void RestoreAssignments()
    {
        if (_preferences.RotationScreenIndex.HasValue)
        {
            MoveRotationToScreen(_preferences.RotationScreenIndex.Value);
        }
        else
        {
            HideRotationWindow();
        }

        if (_preferences.LyricsScreenIndex.HasValue)
        {
            MoveLyricsToScreen(_preferences.LyricsScreenIndex.Value);
        }
        else
        {
            HideLyricsWindow();
        }

        SetLyricsMirror(_preferences.IsLyricsMirrored);
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
        if (vm != null)
        {
            vm.UpdateRotation(singers);
        }

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
        if (vm != null)
        {
            vm.HighlightSinger(singer);
        }
    }

    public void UpdateLyricsFrame(System.Windows.Media.ImageSource frame)
    {
        var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
        if (vm != null)
        {
            vm.UpdateFrame(frame);
        }
    }

    public void SetLyricsMirror(bool mirrored)
    {
        var vm = _serviceProvider.GetService<LyricsWindowViewModel>();
        if (vm != null)
        {
            vm.IsMirrored = mirrored;
        }

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
        if (vm != null)
        {
            vm.SelectedProjectionView = mode;
        }
    }

    public void SetCrawlBannerText(string text)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        if (vm != null)
        {
            vm.CrawlBannerText = text;
        }
    }
}