using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace Lyracist.Shared;

/// <summary>
/// Positions a WPF window onto a physical monitor, converting the monitor's physical-pixel
/// bounds into WPF DIPs using that monitor's own DPI (not the window's current DPI, which may
/// still reflect whichever monitor it was previously on - see MonitorDpiHelper for why that
/// matters on a mixed-DPI multi-monitor setup).
/// </summary>
public static class WindowPositioner
{
    /// <summary>
    /// Resolves the target monitor by device name (case-insensitive), falling back to the
    /// second monitor if present (else the first) when no name is given or it doesn't match
    /// any connected screen. Returns null only if there are no screens at all.
    /// </summary>
    public static Screen? ResolveByDeviceName(Screen[] screens, string? deviceName)
    {
        if (screens.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(deviceName))
        {
            var matched = screens.FirstOrDefault(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                return matched;
            }
        }

        return screens.Length > 1 ? screens[1] : screens[0];
    }

    /// <summary>Positions window to fill monitorArea (physical pixels).</summary>
    public static void FillArea(Window window, Rectangle monitorArea)
    {
        (double left, double top, double width, double height) = ToDips(monitorArea);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;
    }

    /// <summary>Centers a windowWidth x windowHeight (DIPs) window within monitorArea (physical pixels).</summary>
    public static void CenterInArea(Window window, Rectangle monitorArea, double windowWidth, double windowHeight)
    {
        (double areaLeft, double areaTop, double areaWidth, double areaHeight) = ToDips(monitorArea);

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = areaLeft + ((areaWidth - windowWidth) / 2);
        window.Top = areaTop + ((areaHeight - windowHeight) / 2);
    }

    private static (double Left, double Top, double Width, double Height) ToDips(Rectangle monitorArea)
    {
        (double scaleX, double scaleY) = MonitorDpiHelper.GetScaleForMonitor(monitorArea);
        double invScaleX = 1.0 / scaleX;
        double invScaleY = 1.0 / scaleY;

        return (
            monitorArea.Left * invScaleX,
            monitorArea.Top * invScaleY,
            monitorArea.Width * invScaleX,
            monitorArea.Height * invScaleY);
    }
}
