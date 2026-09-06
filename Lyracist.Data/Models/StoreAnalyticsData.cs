// Created on Sep 6, 2026 @ 11:35:00 -> Model classes for Store Analytics reporting
using System;
using System.Collections.Generic;

namespace Lyracist.Data.Models;

public class KeyDistributionItem
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public double BarWidth { get; set; }
}

public class BpmBucketItem
{
    public string RangeLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public double BarHeight { get; set; }
}

public class DayActivityItem
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public string DayOfWeek { get; set; } = string.Empty;
    public int Count { get; set; }
    public double BarHeight { get; set; }
}

public class HourActivityItem
{
    public int Hour { get; set; }
    public string HourLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public int IntensityLevel { get; set; } // 0 to 4
    public string Tooltip { get; set; } = string.Empty;
    public string ColorHex => IntensityLevel switch
    {
        0 => "#202538",
        1 => "#0369A1",
        2 => "#0284C7",
        3 => "#38BDF8",
        _ => "#34D399"
    };
}

public class StoreAnalyticsData
{
    // A) Provider Statistics
    public int TotalTracks { get; set; }
    public int KvCount { get; set; }
    public int PtCount { get; set; }
    public int SfCount { get; set; }
    public int KcCount { get; set; }
    public int LocalCount { get; set; }
    public double KvPercent { get; set; }
    public double PtPercent { get; set; }
    public double SfPercent { get; set; }
    public double KcPercent { get; set; }
    public double LocalPercent { get; set; }
    public string MostFrequentProvider { get; set; } = "None";

    // B) File Type Statistics
    public int Mp3gCount { get; set; }
    public int Mp4Count { get; set; }
    public int ZipCdgCount { get; set; }
    public int AudioOnlyCount { get; set; }
    public int LyricsCount { get; set; }

    // C) FFmpeg Processing Statistics
    public int NormalizedCount { get; set; }
    public int SilenceTrimmedCount { get; set; }
    public int WaveformCount { get; set; }
    public double AvgProcessingTimeMs { get; set; }
    public double MaxProcessingTimeMs { get; set; }
    public double MinProcessingTimeMs { get; set; }

    // D) Quality & Difficulty Statistics
    public int QualityLowCount { get; set; }
    public int QualityMediumCount { get; set; }
    public int QualityHighCount { get; set; }
    public int DifficultyEasyCount { get; set; }
    public int DifficultyMediumCount { get; set; }
    public int DifficultyHardCount { get; set; }
    public List<KeyDistributionItem> KeyDistribution { get; set; } = [];
    public List<BpmBucketItem> BpmHistogram { get; set; } = [];

    // E) Import Activity Summary
    public List<DayActivityItem> DailyActivity { get; set; } = [];
    public List<HourActivityItem> HourlyActivity { get; set; } = [];
    public string BusiestDay { get; set; } = "None";
    public string BusiestHour { get; set; } = "None";
}
