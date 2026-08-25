// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1146 conditional access
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Lyracist.Trivia.Core.Models;

namespace Lyracist.Trivia.Core.Services;

public class TriviaQuestionPack
{
    public string PackId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public List<TriviaQuestion> Questions { get; set; } = [];
}

public static class TriviaPackManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static string GetDefaultPacksDirectory()
    {
        return TriviaStorageHelper.GetPacksDirectory();
    }

    public static List<TriviaQuestionPack> LoadAllPacks(string? packsDirectory = null)
    {
        string dir = packsDirectory ?? GetDefaultPacksDirectory();
        var packs = new List<TriviaQuestionPack>();

        if (!Directory.Exists(dir))
        {
            return packs;
        }

        foreach (string file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(file);
                var pack = JsonSerializer.Deserialize<TriviaQuestionPack>(json, JsonOptions);
                if (pack?.Questions.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(pack.PackId))
                    {
                        pack.PackId = Path.GetFileNameWithoutExtension(file);
                    }
                    if (string.IsNullOrWhiteSpace(pack.Title))
                    {
                        pack.Title = Path.GetFileNameWithoutExtension(file);
                    }
                    packs.Add(pack);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading question pack {file}: {ex.Message}");
            }
        }

        return packs;
    }

    public static void SavePack(TriviaQuestionPack pack, string filePath)
    {
        string dir = Path.GetDirectoryName(filePath) ?? GetDefaultPacksDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(pack, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Combines the questions from one or more selected packs into a single game's question set.
    /// Draws a random candidate pool of up to double the configured game length from across every
    /// selected pack, then rescrambles that candidate pool and takes the configured count. This
    /// runs fresh on every call (including unattended auto-restart between games), so the question
    /// set - and its order - is different every time even when the same packs are selected again.
    /// </summary>
    public static List<TriviaQuestion> BuildMixedQuestionSet(IEnumerable<TriviaQuestionPack> selectedPacks, int questionsPerGame)
    {
        var pool = selectedPacks.SelectMany(p => p.Questions).ToList();
        if (pool.Count == 0) return [];

        int gameSize = questionsPerGame > 0 ? questionsPerGame : 10;
        int candidateSize = Math.Min(pool.Count, gameSize * 2);

        var candidatePool = pool.OrderBy(_ => Random.Shared.Next()).Take(candidateSize).ToList();
        return candidatePool.OrderBy(_ => Random.Shared.Next()).Take(Math.Min(gameSize, candidatePool.Count)).ToList();
    }

    /// <summary>
    /// Preloads question sets for a whole run of back-to-back games at once (e.g. 3 games of 20
    /// questions), instead of drawing each game's set fresh via BuildMixedQuestionSet right
    /// before it starts. Shuffles the full eligible pool once and deals it out in consecutive
    /// chunks so no question repeats across the run - only wrapping around (reshuffling and
    /// continuing) if the pool is too small to cover every game without repeats.
    /// </summary>
    public static List<List<TriviaQuestion>> BuildMultiGameQuestionSets(IEnumerable<TriviaQuestionPack> selectedPacks, int questionsPerGame, int gameCount)
    {
        var result = new List<List<TriviaQuestion>>();
        if (gameCount <= 0) return result;

        var pool = selectedPacks.SelectMany(p => p.Questions).ToList();
        if (pool.Count == 0) return result;

        int gameSize = questionsPerGame > 0 ? questionsPerGame : 10;
        int totalNeeded = gameSize * gameCount;

        var drawn = new List<TriviaQuestion>();
        while (drawn.Count < totalNeeded)
        {
            drawn.AddRange(pool.OrderBy(_ => Random.Shared.Next()));
        }

        for (int i = 0; i < gameCount; i++)
        {
            result.Add(drawn.Skip(i * gameSize).Take(gameSize).ToList());
        }

        return result;
    }

    /// <summary>
    /// Builds the pre-game lobby's featured-category header, e.g. "TONIGHT'S GAME FEATURES 10
    /// QUESTIONS FROM THE FOLLOWING CATEGORY:" (singular pack) or "...CATEGORIES:" (2+ packs
    /// mixed together). Shared by every display ViewModel so the wording can't drift between apps.
    /// </summary>
    public static string BuildFeaturedCategoryHeader(int questionCount, int categoryCount)
    {
        string categoryWord = categoryCount == 1 ? "CATEGORY" : "CATEGORIES";
        return $"⭐ TONIGHT'S GAME FEATURES {questionCount} QUESTIONS FROM THE FOLLOWING {categoryWord}:";
    }
}
