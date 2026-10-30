// Edited on Oct 30, 2026 @ 09:20:00 -> Freeze default SegmentBrush on WheelSegment
using System.Windows.Media;

namespace KnockoutTrivia.Models;

public class WheelSegment
{
    private static readonly Brush DefaultBrush = CreateDefaultBrush();

    private static Brush CreateDefaultBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        brush.Freeze();
        return brush;
    }

    public int Index { get; set; }
    public KnockoutPlayer? Player { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string StrikeColorHex { get; set; } = "#10B981";
    public Brush SegmentBrush { get; set; } = DefaultBrush;
    public double StartAngle { get; set; }
    public double SweepAngle { get; set; }
    public double MidAngle => StartAngle + (SweepAngle / 2.0);

    public string DisplayText => Player != null 
        ? $"{Player.Name} ({TokenCount} 🛡️)" 
        : "Bonus Reward";
}
