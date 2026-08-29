// Edited on Aug 29, 2026 @ 10:34:30 -> Removed redundant data service shuffles to defer uniform randomization to GameStateService
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public class TriviaDataService : ITriviaDataService
{
    private static readonly string BaseDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KoTrivia", "Data");
    private static readonly string BasePacksDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KoTrivia", "Packs");

    public TriviaDataService()
    {
        Directory.CreateDirectory(BaseDataDir);
        Directory.CreateDirectory(BasePacksDir);
    }

    public List<string> GetAvailableDatabases()
    {
        if (!Directory.Exists(BaseDataDir)) return [];
        return [.. Directory.GetFiles(BaseDataDir, "*.db")];
    }

    public List<string> GetAvailablePacks()
    {
        if (!Directory.Exists(BasePacksDir)) return [];
        return [.. Directory.GetFiles(BasePacksDir, "*.json")];
    }

    public async Task<List<SelectableTriviaSource>> GetAvailableSourcesAsync()
    {
        var sources = new List<SelectableTriviaSource>();

        // Discover Databases in KoTrivia/Data
        foreach (var db in GetAvailableDatabases())
        {
            int count = await GetQuestionCountFromDbAsync(db);
            sources.Add(new SelectableTriviaSource
            {
                Name = Path.GetFileName(db),
                FilePath = db,
                SourceType = TriviaSourceType.Database,
                IsSelected = true,
                QuestionCount = count
            });
        }

        // Discover Packs in KoTrivia/Packs
        foreach (var pack in GetAvailablePacks())
        {
            int count = await GetQuestionCountFromPackAsync(pack);
            sources.Add(new SelectableTriviaSource
            {
                Name = Path.GetFileName(pack),
                FilePath = pack,
                SourceType = TriviaSourceType.Pack,
                IsSelected = false,
                QuestionCount = count
            });
        }

        return sources;
    }

    private static async Task<int> GetQuestionCountFromDbAsync(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath)) return 0;
            string connString = $"Data Source={dbPath};";
            await using var conn = new SqliteConnection(connString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Questions";
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<int> GetQuestionCountFromPackAsync(string packPath)
    {
        try
        {
            if (!File.Exists(packPath)) return 0;
            string json = await File.ReadAllTextAsync(packPath);
            var parsed = JsonSerializer.Deserialize<List<KnockoutQuestion>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return parsed?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<List<KnockoutQuestion>> LoadQuestionsFromMultipleSourcesAsync(IEnumerable<string> sourcePaths)
    {
        var allQuestions = new List<KnockoutQuestion>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in sourcePaths)
        {
            if (!File.Exists(path)) continue;

            List<KnockoutQuestion> list;
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                list = await LoadQuestionsFromPackAsync(path);
            }
            else
            {
                list = await LoadQuestionsFromDatabaseAsync(path);
            }

            foreach (var q in list)
            {
                if (seenIds.Add(q.Id))
                {
                    allQuestions.Add(q);
                }
            }
        }

        // If no sources specified or loaded, fallback to default trivia.db
        if (allQuestions.Count == 0)
        {
            allQuestions = await LoadQuestionsFromDatabaseAsync();
        }

        return allQuestions;
    }

    public async Task<List<KnockoutQuestion>> LoadQuestionsFromDatabaseAsync(string? dbPath = null)
    {
        string path = dbPath ?? Path.Combine(BaseDataDir, "trivia.db");
        if (!File.Exists(path))
        {
            return [];
        }

        var questions = new List<KnockoutQuestion>();
        string connString = $"Data Source={path};";

        await using var conn = new SqliteConnection(connString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Category, Difficulty, QuestionType, Prompt, OptionsJson, CorrectAnswerIndex, Explanation, AudioSnippetPath, TimeLimitSeconds
            FROM Questions";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var q = new KnockoutQuestion
            {
                Id = reader.GetString(0),
                Category = reader.GetString(1),
                Difficulty = (TriviaDifficulty)reader.GetInt32(2),
                QuestionType = (TriviaQuestionType)reader.GetInt32(3),
                Prompt = reader.GetString(4),
                CorrectAnswerIndex = reader.GetInt32(6),
                Explanation = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                AudioSnippetPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                TimeLimitSeconds = reader.IsDBNull(9) ? 15 : reader.GetInt32(9)
            };

            string optionsJson = reader.GetString(5);
            try
            {
                q.Options = JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
            }
            catch
            {
                q.Options = [];
            }

            questions.Add(q);
        }

        return questions;
    }

    public async Task<List<KnockoutQuestion>> LoadQuestionsFromPackAsync(string packFilePath)
    {
        if (!File.Exists(packFilePath)) return [];

        try
        {
            string json = await File.ReadAllTextAsync(packFilePath);
            var parsed = JsonSerializer.Deserialize<List<KnockoutQuestion>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed != null)
            {
                return parsed;
            }
            return [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<string>> GetCategoriesAsync(string? dbPath = null)
    {
        string path = dbPath ?? Path.Combine(BaseDataDir, "trivia.db");
        if (!File.Exists(path)) return [];

        var categories = new List<string>();
        string connString = $"Data Source={path};";

        await using var conn = new SqliteConnection(connString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category FROM Questions ORDER BY Category";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }
}
