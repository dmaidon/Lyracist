// Created on Sep 6, 2026 @ 08:53:00 -> Session schedule and last request cutoff helper
using System;
using System.Collections.Generic;

namespace Lyracist.Shared;

/// <summary>
/// Provides utilities for managing DJ session schedules and last request cutoffs
/// across patron portals and kiosk interfaces.
/// </summary>
public static class SessionScheduleHelper
{
    /// <summary>
    /// Pre-populated list of standard 30-minute intervals throughout a 24-hour cycle formatted in 12-hour AM/PM.
    /// </summary>
    public static readonly IReadOnlyList<string> StandardTimeOptions = GenerateTimeOptions();

    private static List<string> GenerateTimeOptions()
    {
        var list = new List<string>(48);
        for (int i = 0; i < 48; i++)
        {
            int hour = i / 2;
            int min = (i % 2) * 30;
            var ts = new TimeSpan(hour, min, 0);
            list.Add(DateTime.Today.Add(ts).ToString("h:mm tt"));
        }
        return list;
    }

    /// <summary>
    /// Parses a time string that may be in 12-hour AM/PM format (e.g. "8:00 PM") or 24-hour format (e.g. "20:00").
    /// </summary>
    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string trimmed = text.Trim();

        if (TimeSpan.TryParse(trimmed, out time)) return true;
        if (DateTime.TryParse(trimmed, out var dt))
        {
            time = dt.TimeOfDay;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the specified current time falls within a given start and stop window,
    /// correctly handling overnight sessions that cross midnight (e.g. 20:00 to 02:00).
    /// </summary>
    public static bool IsTimeInWindow(TimeSpan current, TimeSpan start, TimeSpan stop)
    {
        if (start <= stop)
        {
            return current >= start && current <= stop;
        }
        else
        {
            // Overnight window crossing midnight
            return current >= start || current <= stop;
        }
    }

    /// <summary>
    /// Formats a TimeSpan as a friendly 12-hour time string (e.g. "8:00 PM", "1:30 AM").
    /// </summary>
    public static string FormatDisplayTime(TimeSpan time)
    {
        return DateTime.Today.Add(time).ToString("h:mm tt");
    }

    /// <summary>
    /// Evaluates whether song request submissions are currently permitted based on the session
    /// schedule and last request cutoff configurations.
    /// </summary>
    public static bool IsRequestSubmissionAllowed(
        bool enableSessionSchedule,
        string? sessionStartTime,
        string? sessionStopTime,
        bool enableLastRequestTime,
        string? lastRequestTime,
        out string reason,
        DateTime? now = null)
    {
        reason = string.Empty;
        var currentTime = (now ?? DateTime.Now).TimeOfDay;

        if (!TryParseTime(sessionStartTime, out var startTime))
            startTime = new TimeSpan(20, 0, 0); // 8:00 PM default

        if (!TryParseTime(sessionStopTime, out var stopTime))
            stopTime = new TimeSpan(2, 0, 0); // 2:00 AM default

        if (!TryParseTime(lastRequestTime, out var cutoffTime))
            cutoffTime = new TimeSpan(1, 30, 0); // 1:30 AM default

        // 1. Enforce session start / stop schedule if enabled
        if (enableSessionSchedule)
        {
            bool inSession = IsTimeInWindow(currentTime, startTime, stopTime);
            if (!inSession)
            {
                reason = $"Song requests are currently closed. Tonight's session is scheduled from {FormatDisplayTime(startTime)} to {FormatDisplayTime(stopTime)}.";
                return false;
            }
        }

        // 2. Enforce last request cutoff time if enabled
        if (enableLastRequestTime)
        {
            TimeSpan effectiveStart;
            if (enableSessionSchedule)
            {
                effectiveStart = startTime;
            }
            else
            {
                // When session schedule is not explicitly enabled, assume a 12-hour active window leading up to cutoff
                effectiveStart = cutoffTime.TotalHours >= 12
                    ? cutoffTime.Subtract(TimeSpan.FromHours(12))
                    : cutoffTime.Add(TimeSpan.FromHours(12));
            }

            bool beforeCutoff = IsTimeInWindow(currentTime, effectiveStart, cutoffTime);
            if (!beforeCutoff)
            {
                reason = $"Song requests are now closed for tonight. The cutoff time for requests was {FormatDisplayTime(cutoffTime)}.";
                return false;
            }
        }

        return true;
    }
}
