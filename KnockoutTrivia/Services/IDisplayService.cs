// Edited on Aug 27, 2026 @ 15:26:35 -> Added multi-monitor option formatting and window positioning methods
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public interface IDisplayService
{
    List<MonitorInfo> GetAvailableMonitors();
    List<DisplayMonitorOption> GetDisplayOptions();
    MonitorInfo? GetMonitor(int index);
    void PositionWindow(Window window, int monitorIndex, bool maximize = false);
}

public class DisplayService : IDisplayService
{
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    private const uint MonitorInfoFlagsPrimary = 0x00000001;

    public List<MonitorInfo> GetAvailableMonitors()
    {
        var monitors = new List<MonitorInfo>();
        int index = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
        {
            var mi = new MonitorInfoEx();
            mi.Size = Marshal.SizeOf(typeof(MonitorInfoEx));
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                bool isPrimary = (mi.Flags & MonitorInfoFlagsPrimary) != 0;
                int width = mi.Monitor.Right - mi.Monitor.Left;
                int height = mi.Monitor.Bottom - mi.Monitor.Top;

                monitors.Add(new MonitorInfo(
                    Index: index++,
                    DeviceName: string.IsNullOrWhiteSpace(mi.DeviceName) ? $"Display {index}" : mi.DeviceName,
                    IsPrimary: isPrimary,
                    X: mi.Monitor.Left,
                    Y: mi.Monitor.Top,
                    Width: width,
                    Height: height
                ));
            }
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
        {
            // Fallback primary display
            monitors.Add(new MonitorInfo(
                Index: 0,
                DeviceName: "Primary Display",
                IsPrimary: true,
                X: 0,
                Y: 0,
                Width: 1920,
                Height: 1080
            ));
        }

        return monitors;
    }

    public List<DisplayMonitorOption> GetDisplayOptions()
    {
        return GetAvailableMonitors().Select(m => new DisplayMonitorOption
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

    public void PositionWindow(Window window, int monitorIndex, bool maximize = false)
    {
        ArgumentNullException.ThrowIfNull(window);

        var monitors = GetAvailableMonitors();
        if (monitors.Count == 0) return;

        var target = (monitorIndex >= 0 && monitorIndex < monitors.Count)
            ? monitors[monitorIndex]
            : (monitors.FirstOrDefault(m => !m.IsPrimary) ?? monitors[0]);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = target.X;
        window.Top = target.Y;
        window.Width = target.Width;
        window.Height = target.Height;

        if (maximize)
        {
            window.WindowState = WindowState.Maximized;
        }
    }
}
