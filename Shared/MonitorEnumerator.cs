// Edited on Oct 22, 2026 @ 08:06:00 -> Add cached monitor enumeration and SystemEvents.DisplaySettingsChanged listener with debounce
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Lyracist.Shared;

/// <summary>Enumerates and caches connected monitors with automatic refresh via Windows display settings change events.</summary>
public static class MonitorEnumerator
{
    private static readonly object _syncLock = new();
    private static System.Threading.Timer? _debounceTimer;
    private static IReadOnlyList<MonitorInfo>? _cachedMonitors;

    static MonitorEnumerator()
    {
        try
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }
        catch
        {
            // SystemEvents may be unavailable in non-interactive/headless environments
        }
    }

    /// <summary>
    /// Raised (debounced and marshaled to the UI dispatcher when available) whenever Windows reports
    /// display settings, monitor topology, or connected displays have changed.
    /// </summary>
    public static event EventHandler? MonitorsChanged;

    /// <summary>
    /// Returns the cached list of connected monitors, or enumerates them if not yet cached or forced.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> GetMonitors(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedMonitors != null)
        {
            return _cachedMonitors;
        }

        lock (_syncLock)
        {
            if (!forceRefresh && _cachedMonitors != null)
            {
                return _cachedMonitors;
            }

            _cachedMonitors = EnumerateMonitors();
            return _cachedMonitors;
        }
    }

    /// <summary>
    /// Explicitly invalidates the monitor cache so the next call queries the OS.
    /// </summary>
    public static void InvalidateCache()
    {
        lock (_syncLock)
        {
            _cachedMonitors = null;
        }
    }

    private static List<MonitorInfo> EnumerateMonitors()
    {
        Screen[] screens = Screen.AllScreens;
        var monitors = new List<MonitorInfo>(screens.Length);

        for (int i = 0; i < screens.Length; i++)
        {
            Screen screen = screens[i];
            monitors.Add(new MonitorInfo(
                Index: i,
                DeviceName: screen.DeviceName,
                IsPrimary: screen.Primary,
                X: screen.Bounds.X,
                Y: screen.Bounds.Y,
                Width: screen.Bounds.Width,
                Height: screen.Bounds.Height));
        }

        return monitors;
    }

    private static void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (_syncLock)
        {
            _cachedMonitors = null;
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(OnDebounceElapsed, null, 250, System.Threading.Timeout.Infinite);
        }
    }

    private static void OnDebounceElapsed(object? state)
    {
        lock (_syncLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            // Pre-warm cache with stable displays
            _cachedMonitors = EnumerateMonitors();
        }

        NotifyMonitorsChanged();
    }

    private static void NotifyMonitorsChanged()
    {
        var handler = MonitorsChanged;
        if (handler == null) return;

        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(() => handler.Invoke(null, EventArgs.Empty));
        }
        else
        {
            handler.Invoke(null, EventArgs.Empty);
        }
    }
}
