// Created on Oct 11, 2026 @ 13:28:00 -> Model for tracks without an artist persisted in noartist.db
using System;

namespace Lyracist.Data.Models
{
    public class UnresolvedTrack
    {
        public string FilePath { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public double Duration { get; set; }
        public string KaraokeType { get; set; } = string.Empty;
        public bool IsKaraoke { get; set; }
        public DateTime DateFlagged { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
    }
}
