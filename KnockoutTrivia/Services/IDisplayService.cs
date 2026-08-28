// Edited on Aug 28, 2026 @ 11:14:00 -> Fixed multi-monitor DPI scaling and window positioning using WindowPositioner and Screen.AllScreens
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public interface IDisplayService
{
    IReadOnlyList<MonitorInfo> GetAvailableMonitors();
    List<DisplayMonitorOption> GetDisplayOptions();
    MonitorInfo? GetMonitor(int index);
    bool PositionWindow(Window window, int monitorIndex, string? deviceName = null, bool fillArea = true);
}

public class DisplayService : IDisplayService
{
    public IReadOnlyList<MonitorInfo> GetAvailableMonitors()
    {
        return MonitorEnumerator.GetMonitors();
    }

    public List<DisplayMonitorOption> GetDisplayOptions()
    {
        var monitors = GetAvailableMonitors();
        return monitors.Select(m => new DisplayMonitorOption
        {
            Index = m.Index,
            DeviceName = m.DeviceName,
            IsPrimary = m.IsPrimary,
            Width = m.Width,
            Height = m.Height,
            X = m.X,
            Y = m.Y
        }).ToList();
    }

    public MonitorInfo? GetMonitor(int index)
    {
        var list = GetAvailableMonitors();
        if (index >= 0 && index < list.Count)
        {
            return list[index];
        }
        return list.FirstOrDefault(m => m.IsPrimary) ?? list.FirstOrDefault();
    }

    public bool PositionWindow(Window window, int monitorIndex, string? deviceName = null, bool fillArea = true)
    {
        ArgumentNullException.ThrowIfNull(window);

        Screen[] screens = Screen.AllScreens;
        if (screens.Length == 0) return false;

        // 1. Try matching by exact DeviceName first
        Screen? target = null;
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            target = screens.FirstOrDefault(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Fall back to monitorIndex
        if (target == null && monitorIndex >= 0 && monitorIndex < screens.Length)
        {
            target = screens[monitorIndex];
        }

        // 3. Fall back to secondary monitor if available, else primary
        target ??= screens.Length > 1 ? screens[1] : screens[0];

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;

        if (fillArea)
        {
            // Position and scale to exact target monitor area in DPI-aware DIPs
            WindowPositioner.FillArea(window, target.Bounds);
        }
        else
        {
            // Position window centered in working area
            double width = window.Width > 100 ? window.Width : 1280;
            double height = window.Height > 100 ? window.Height : 720;
            WindowPositioner.CenterInArea(window, target.WorkingArea, width, height);
        }

        return true;
    }
}

