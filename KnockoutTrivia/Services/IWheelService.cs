// Edited on Oct 30, 2026 @ 09:20:00 -> Freeze SolidColorBrush instances in WheelService
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface IWheelService
{
    List<WheelSegment> BuildWheelSegments(IEnumerable<KnockoutPlayer> allPlayers, KnockoutPlayer superStreakPlayer);
    WheelSegment? SelectRandomTarget(IReadOnlyList<WheelSegment> segments);
}

public class WheelService : IWheelService
{
    private readonly Random _random = new();

    private static readonly string[] Palette =
    [
        "#E11D48", "#2563EB", "#7C3AED", "#059669", "#D97706",
        "#0891B2", "#4F46E5", "#DC2626", "#16A34A", "#9333EA"
    ];

    private static readonly Brush BonusBrush = CreateFrozenBrush(Color.FromRgb(234, 179, 8));

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public List<WheelSegment> BuildWheelSegments(IEnumerable<KnockoutPlayer> allPlayers, KnockoutPlayer superStreakPlayer)
    {
        // Get all players with at least 1 token, excluding the super streak player
        var targetablePlayers = allPlayers
            .Where(p => p.Id != superStreakPlayer.Id && p.Tokens > 0 && !p.IsEliminated)
            .ToList();

        var segments = new List<WheelSegment>();

        if (targetablePlayers.Count == 0)
        {
            // Fallback reward segment
            segments.Add(new WheelSegment
            {
                Index = 0,
                Player = null,
                PlayerName = "BONUS REWARD (+50 Pts)",
                TokenCount = 0,
                StrikeColorHex = "#EAB308",
                SegmentBrush = BonusBrush,
                StartAngle = 0,
                SweepAngle = 360
            });
            return segments;
        }

        double sweep = 360.0 / targetablePlayers.Count;
        for (int i = 0; i < targetablePlayers.Count; i++)
        {
            var p = targetablePlayers[i];
            string hex = Palette[i % Palette.Length];
            Color col = (Color)ColorConverter.ConvertFromString(hex);

            segments.Add(new WheelSegment
            {
                Index = i,
                Player = p,
                PlayerName = p.Name,
                TokenCount = p.Tokens,
                StrikeColorHex = p.StrikeColorHex,
                SegmentBrush = CreateFrozenBrush(col),
                StartAngle = i * sweep,
                SweepAngle = sweep
            });
        }

        return segments;
    }

    public WheelSegment? SelectRandomTarget(IReadOnlyList<WheelSegment> segments)
    {
        if (segments.Count == 0) return null;
        int index = _random.Next(segments.Count);
        return segments[index];
    }
}
