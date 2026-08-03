using System.Collections.Generic;
using System.Windows.Forms;

namespace Lyracist.Shared;

/// <summary>Enumerates connected monitors. Each app formats/selects from these as it sees fit.</summary>
public static class MonitorEnumerator
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
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
}
