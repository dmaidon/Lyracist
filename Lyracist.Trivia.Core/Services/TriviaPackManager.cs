// Edited on Aug 17, 2026 @ 13:21:45 -> Added JsonStringEnumConverter for string-based enum deserialization
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
                if (pack != null && pack.Questions.Count > 0)
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
}
