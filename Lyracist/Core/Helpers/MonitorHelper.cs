using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Lyracist.Core.Helpers;

public static class MonitorHelper
{
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lpRect, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    public class MonitorInfo
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lpRect, IntPtr dwData)
        {
            monitors.Add(new MonitorInfo
            {
                Left = lpRect.Left,
                Top = lpRect.Top,
                Right = lpRect.Right,
                Bottom = lpRect.Bottom
            });
            return true;
        }, IntPtr.Zero);

        return monitors;
    }
}
