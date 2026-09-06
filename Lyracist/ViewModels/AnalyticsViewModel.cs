// Created on Sep 6, 2026 @ 11:41:00 -> AnalyticsViewModel for Store tab statistical insights and charts
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Services.Store;
using Lyracist.Shared;

namespace Lyracist.ViewModels;

public partial class AnalyticsViewModel : BaseViewModel
{
    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalTracks;

    [ObservableProperty]
    private int _kvCount;

    [ObservableProperty]
    private int _ptCount;

    [ObservableProperty]
    private int _sfCount;

    [ObservableProperty]
    private int _kcCount;

    [ObservableProperty]
    private int _localCount;

    [ObservableProperty]
    private double _kvPercent;

    [ObservableProperty]
    private double _ptPercent;

    [ObservableProperty]
    private double _sfPercent;

    [ObservableProperty]
    private double _kcPercent;

    [ObservableProperty]
    private double _localPercent;

    [ObservableProperty]
    private string _mostFrequentProvider = "None";

    [ObservableProperty]
    private int _mp3gCount;

    [ObservableProperty]
    private int _mp4Count;

    [ObservableProperty]
    private int _zipCdgCount;

    [ObservableProperty]
    private int _audioOnlyCount;

    [ObservableProperty]
    private int _lyricsCount;

    [ObservableProperty]
    private int _normalizedCount;

    [ObservableProperty]
    private int _silenceTrimmedCount;

    [ObservableProperty]
    private int _waveformCount;

    [ObservableProperty]
    private double _avgProcessingTimeMs;

    [ObservableProperty]
    private double _maxProcessingTimeMs;

    [ObservableProperty]
    private double _minProcessingTimeMs;

    [ObservableProperty]
    private string _avgProcessingTimeText = "0 ms";

    [ObservableProperty]
    private string _maxProcessingTimeText = "0 ms";

    [ObservableProperty]
    private string _minProcessingTimeText = "0 ms";

    [ObservableProperty]
    private int _qualityLowCount;

    [ObservableProperty]
    private int _qualityMediumCount;

    [ObservableProperty]
    private int _qualityHighCount;

    [ObservableProperty]
    private int _difficultyEasyCount;

    [ObservableProperty]
    private int _difficultyMediumCount;

    [ObservableProperty]
    private int _difficultyHardCount;

    [ObservableProperty]
    private string _busiestDay = "No recent activity";

    [ObservableProperty]
    private string _busiestHour = "No recent activity";

    public ObservableCollection<KeyDistributionItem> KeyDistribution { get; } = [];
    public ObservableCollection<BpmBucketItem> BpmHistogram { get; } = [];
    public ObservableCollection<DayActivityItem> DailyActivity { get; } = [];
    public ObservableCollection<HourActivityItem> HourlyActivity { get; } = [];

    public AnalyticsViewModel()
    {
    }

    [RelayCommand]
    public void ToggleAnalytics()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    public async Task RefreshAnalyticsAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;

            using var context = new LyracistDbContext();
            var data = await context.GetStoreAnalyticsAsync();
            var (avgMs, maxMs, minMs) = PurchasedTrackWatcherService.GetProcessingStats();

            TotalTracks = data.TotalTracks;

            // Provider Stats
            KvCount = data.KvCount;
            PtCount = data.PtCount;
            SfCount = data.SfCount;
            KcCount = data.KcCount;
            LocalCount = data.LocalCount;
            KvPercent = data.KvPercent;
            PtPercent = data.PtPercent;
            SfPercent = data.SfPercent;
            KcPercent = data.KcPercent;
            LocalPercent = data.LocalPercent;
            MostFrequentProvider = data.MostFrequentProvider;

            // File Type Stats
            Mp3gCount = data.Mp3gCount;
            Mp4Count = data.Mp4Count;
            ZipCdgCount = data.ZipCdgCount;
            AudioOnlyCount = data.AudioOnlyCount;
            LyricsCount = data.LyricsCount;

            // Processing Stats
            NormalizedCount = data.NormalizedCount;
            SilenceTrimmedCount = data.SilenceTrimmedCount;
            WaveformCount = data.WaveformCount;

            AvgProcessingTimeMs = avgMs;
            MaxProcessingTimeMs = maxMs;
            MinProcessingTimeMs = minMs;
            AvgProcessingTimeText = FormatMs(avgMs);
            MaxProcessingTimeText = FormatMs(maxMs);
            MinProcessingTimeText = FormatMs(minMs);

            // Quality & Difficulty Stats
            QualityLowCount = data.QualityLowCount;
            QualityMediumCount = data.QualityMediumCount;
            QualityHighCount = data.QualityHighCount;
            DifficultyEasyCount = data.DifficultyEasyCount;
            DifficultyMediumCount = data.DifficultyMediumCount;
            DifficultyHardCount = data.DifficultyHardCount;

            // Activity Summary
            BusiestDay = data.BusiestDay;
            BusiestHour = data.BusiestHour;

            // Collections
            KeyDistribution.Clear();
            foreach (var item in data.KeyDistribution) KeyDistribution.Add(item);

            BpmHistogram.Clear();
            foreach (var item in data.BpmHistogram) BpmHistogram.Add(item);

            DailyActivity.Clear();
            foreach (var item in data.DailyActivity) DailyActivity.Add(item);

            HourlyActivity.Clear();
            foreach (var item in data.HourlyActivity) HourlyActivity.Add(item);
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "Failed to refresh Store Analytics", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatMs(double ms)
    {
        if (ms <= 0) return "0 ms";
        if (ms >= 1000) return $"{ms / 1000.0:F1} s";
        return $"{ms:F0} ms";
    }
}
