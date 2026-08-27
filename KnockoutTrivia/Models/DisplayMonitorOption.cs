// Created on Aug 27, 2026 @ 15:26:30 -> DisplayMonitorOption model for multi-monitor selection and formatting
namespace KnockoutTrivia.Models;

public class DisplayMonitorOption
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public string DisplayLabel =>
        $"Display {Index + 1}: {(IsPrimary ? "Primary (Host Screen)" : "Secondary (TV / Projector)")} — {Width}x{Height}";

    public string ShortLabel =>
        $"Screen {Index + 1} ({(IsPrimary ? "Host" : "Audience")})";
}
