using System;
using System.Runtime.InteropServices;

namespace Lyracist.Shared;

/// <summary>
/// Looks up the real DPI scale of a specific monitor via Win32, rather than relying on
/// VisualTreeHelper.GetDpi(window) - which only reflects whichever monitor a window's HWND
/// currently happens to be on, not the monitor it's about to be moved/sized onto. On mixed-DPI
/// setups (e.g. a 100% laptop panel plus a 125% external monitor) using the wrong monitor's
/// scale produces windows that are the wrong size and land in the wrong place. Shared by
/// KSRotation and Lyracist, which both position rotation/banner windows onto a chosen monitor.
/// </summary>
public static class MonitorDpiHelper
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Returns the WPF DIP scale factor (1.0 == 96 DPI) actually in effect for the monitor
    /// covering the given physical-pixel bounds. Falls back to 1.0 (no scaling) if the
    /// lookup fails for any reason.
    /// </summary>
    public static (double ScaleX, double ScaleY) GetScaleForMonitor(System.Drawing.Rectangle bounds)
    {
        try
        {
            var rect = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
            IntPtr hMonitor = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);

            if (hMonitor != IntPtr.Zero && GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch (Exception ex)
        {
            // No per-app logger available here (this file is linked into several host apps with
            // different names) - Trace.TraceError still isn't Debug-only, unlike Debug.WriteLine.
            System.Diagnostics.Trace.TraceError($"MonitorDpiHelper.GetScaleForMonitor failed: {ex}");
        }

        return (1.0, 1.0);
    }
}
