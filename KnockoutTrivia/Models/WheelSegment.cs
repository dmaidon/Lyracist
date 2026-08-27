// Created on Aug 27, 2026 @ 14:35:25 -> WheelSegment model for Super Streak target wheel rendering
using System.Windows.Media;

namespace KnockoutTrivia.Models;

public class WheelSegment
{
    public int Index { get; set; }
    public KnockoutPlayer? Player { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string StrikeColorHex { get; set; } = "#10B981";
    public Brush SegmentBrush { get; set; } = new SolidColorBrush(Color.FromRgb(16, 185, 129));
    public double StartAngle { get; set; }
    public double SweepAngle { get; set; }
    public double MidAngle => StartAngle + (SweepAngle / 2.0);

    public string DisplayText => Player != null 
        ? $"{Player.Name} ({TokenCount} 🛡️)" 
        : "Bonus Reward";
}
