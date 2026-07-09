using System;
using System.Collections.Generic;

namespace Lyracist.Core.Helpers;

public static class SingerXpHelper
{
    public static int CalculateXP(int totalSongsSung, int score)
    {
        return totalSongsSung * 100 + score;
    }

    public static int CalculateLevel(int xp)
    {
        if (xp <= 0) return 1;
        double l = (1.0 + Math.Sqrt(1.0 + 8.0 * xp / 50.0)) / 2.0;
        return Math.Max(1, (int)Math.Floor(l));
    }

    public static double CalculateXPProgress(int xp, int level)
    {
        int xpForCurrent = 50 * (level - 1) * level;
        int xpForNext = 50 * level * (level + 1);
        int range = xpForNext - xpForCurrent;
        if (range <= 0) return 0;
        double progress = (double)(xp - xpForCurrent) / range;
        return Math.Clamp(progress * 100, 0, 100);
    }

    public static string GetLevelName(int level)
    {
        return level switch
        {
            1 => "Shower Singer",
            2 => "Car Concert Lead",
            3 => "Pub Regular",
            4 => "Stage Whisperer",
            5 => "Rising Star",
            6 => "Crowd Pleaser",
            7 => "Vocal Powerhouse",
            8 => "Microphone Master",
            9 => "Karaoke Champion",
            _ => "Karaoke Legend 👑"
        };
    }

    public static List<string> GetBadges(int totalSongsSung, int score, double averageRating, int ratingCount)
    {
        var list = new List<string>();
        if (totalSongsSung >= 1) list.Add("🎤 Debut");
        if (totalSongsSung >= 3) list.Add("🔥 Rising Star");
        if (totalSongsSung >= 10) list.Add("👑 Legend");
        if (averageRating >= 4.5 && ratingCount >= 3) list.Add("⭐ Crowd Pleaser");
        if (score >= 200) list.Add("🎯 High Scorer");
        return list;
    }
}
