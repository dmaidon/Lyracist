// Created on Oct 25, 2026 @ 23:53:00 -> Models for Scaryoke Wheel segments and tablet synchronization
using System.Collections.Generic;

namespace KSRotation.Models
{
    public class ScaryokeWheelSegment
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF6D00";
        public string TextColor { get; set; } = "#FFFFFF";
        public double Weight { get; set; } = 1.0;
        public double Sweep { get; set; }
    }

    public class ScaryokeStateDto
    {
        public bool IsActive { get; set; }
        public bool IsSpinning { get; set; }
        public double TargetAngle { get; set; }
        public double SpinDuration { get; set; }
        public string ResultText { get; set; } = string.Empty;
        public string ResultDetail { get; set; } = string.Empty;
        public bool PreventConsecutiveRepeats { get; set; } = true;
        public string CurrentSingerName { get; set; } = string.Empty;
        public List<ScaryokeWheelSegment> Segments { get; set; } = [];
    }
}
