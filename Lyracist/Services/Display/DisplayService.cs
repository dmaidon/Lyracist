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

    public DisplayService(IServiceProvider serviceProvider, IMediaEngine mediaEngine)
    {
        _serviceProvider = serviceProvider;
        _mediaEngine = mediaEngine;

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
        if (_rotationWindow == null || !_rotationWindow.IsLoaded)
        {
            _rotationWindow = _serviceProvider.GetRequiredService<RotationWindow>();
        }
        _rotationWindow.Show();
    }

    public void ShowLyricsWindow()
    {
        if (_lyricsWindow == null || !_lyricsWindow.IsLoaded)
        {
            _lyricsWindow = _serviceProvider.GetRequiredService<LyricsWindow>();
        }
        _lyricsWindow.Show();
    }

    public void MoveRotationToScreen(int screenIndex)
    {
        ShowRotationWindow();
        MoveWindowToScreen(_rotationWindow!, screenIndex);
    }

    public void MoveLyricsToScreen(int screenIndex)
    {
        ShowLyricsWindow();
        MoveWindowToScreen(_lyricsWindow!, screenIndex);
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

    public void SetRotationAnnouncement(string message, bool visible)
    {
        var vm = _serviceProvider.GetService<RotationWindowViewModel>();
        if (vm != null)
        {
            vm.AnnouncementBanner = message;
            vm.IsAnnouncementVisible = visible;
        }
    }
}
