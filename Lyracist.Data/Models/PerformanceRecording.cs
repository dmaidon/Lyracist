// Created on Oct 7, 2026 @ 19:44:00 -> PerformanceRecording entity for captured singer performances
using System;

namespace Lyracist.Data.Models
{
    public class PerformanceRecording
    {
        public int Id { get; set; }
        public string SingerName { get; set; } = string.Empty;
        public int? SingerId { get; set; }
        public string SongTitle { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
        public double DurationSeconds { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
