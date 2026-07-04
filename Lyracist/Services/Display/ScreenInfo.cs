using System.Windows;

namespace Lyracist.Services.Display;

public class ScreenInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Rect Bounds { get; set; }
}
