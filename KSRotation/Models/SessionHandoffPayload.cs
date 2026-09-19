// Created on Sep 19, 2026 @ 17:40:00 -> Model representing full karaoke session handoff payload
using System;
using System.Collections.Generic;

namespace KSRotation.Models
{
    public class SessionHandoffPayload
    {
        public int Version { get; set; } = 1;
        public DateTime ExportedAt { get; set; } = DateTime.Now;
        public string SourceDevice { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public string DjName { get; set; } = string.Empty;
        public string DjPin { get; set; } = string.Empty;
        public bool IsLastRound { get; set; }
        public bool EnableSessionSchedule { get; set; }
        public string SessionStartTime { get; set; } = "8:00 PM";
        public string SessionStopTime { get; set; } = "2:00 AM";
        public bool EnableLastRequestTime { get; set; }
        public string LastRequestTime { get; set; } = "1:30 AM";
        public bool BlockDuplicateSongsInSession { get; set; }
        public bool FloatCurrentSingerToTop { get; set; } = true;
        public bool ShowEstimatedWaitTime { get; set; } = true;
        public double DefaultSongLengthMinutes { get; set; } = 4.75;
        public string ActiveSpecialEvent { get; set; } = "None";
        public List<SingerEntry> Singers { get; set; } = [];
        public List<SongPerformance> PerformanceHistory { get; set; } = [];
        public List<PatronRequest> IncomingRequests { get; set; } = [];
    }

    public class DiscoveredPeer
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 5000;
        public string AppName { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public string DjName { get; set; } = string.Empty;
        public int SingerCount { get; set; }
        public string CurrentSinger { get; set; } = string.Empty;

        public string DisplayText => $"{VenueName} ({Host}:{Port}) - {SingerCount} singers";
    }
}
