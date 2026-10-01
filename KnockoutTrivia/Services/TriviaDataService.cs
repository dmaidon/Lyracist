// Edited on Oct 1, 2026 @ 08:47:00 -> Add robust question validation, DB null safety, and cached pack counts
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using KnockoutTrivia.Models;
using Lyracist.Shared;

namespace KnockoutTrivia.Services;

public class TriviaDataService : ITriviaDataService
{
    private static readonly string BaseDataDir = Globals.DataDir;
    private static readonly string BasePacksDir = Globals.PacksDir;

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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime LastWrite, int Count)> _packCountCache = new(StringComparer.OrdinalIgnoreCase);

    private static async Task<int> GetQuestionCountFromPackAsync(string packPath)
    {
        try
        {
            if (!File.Exists(packPath)) return 0;
            var lastWrite = File.GetLastWriteTimeUtc(packPath);
            if (_packCountCache.TryGetValue(packPath, out var cached) && cached.LastWrite == lastWrite)
            {
                return cached.Count;
            }

            var questions = await LoadQuestionsFromPackFileAsync(packPath);
            int count = questions.Count;
            _packCountCache[packPath] = (lastWrite, count);
            return count;
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
            string id = reader.IsDBNull(0) ? Guid.NewGuid().ToString("N")[..8] : reader.GetString(0);
            string category = reader.IsDBNull(1) ? "General" : reader.GetString(1);
            var difficulty = reader.IsDBNull(2) ? TriviaDifficulty.Medium : (TriviaDifficulty)reader.GetInt32(2);
            var questionType = reader.IsDBNull(3) ? TriviaQuestionType.MultipleChoice : (TriviaQuestionType)reader.GetInt32(3);
            string prompt = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            int correctIndex = reader.IsDBNull(6) ? -1 : reader.GetInt32(6);
            string explanation = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
            string? audioPath = reader.IsDBNull(8) ? null : reader.GetString(8);
            int timeLimit = reader.IsDBNull(9) ? 15 : reader.GetInt32(9);

            List<string> options = [];
            if (!reader.IsDBNull(5))
            {
                try
                {
                    options = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? [];
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(prompt) || options.Count < 2 || options.Count > 4 || correctIndex < 0 || correctIndex >= options.Count)
            {
                continue;
            }

            questions.Add(new KnockoutQuestion
            {
                Id = id,
                Category = category,
                Difficulty = difficulty,
                QuestionType = questionType,
                Prompt = prompt,
                Options = options,
                CorrectAnswerIndex = correctIndex,
                Explanation = explanation,
                AudioSnippetPath = audioPath,
                TimeLimitSeconds = timeLimit > 0 ? timeLimit : 15
            });
        }

        return questions;
    }

    public Task<List<KnockoutQuestion>> LoadQuestionsFromPackAsync(string packFilePath)
    {
        return LoadQuestionsFromPackFileAsync(packFilePath);
    }

    private static async Task<List<KnockoutQuestion>> LoadQuestionsFromPackFileAsync(string packFilePath)
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
                return parsed.Where(q =>
                    !string.IsNullOrWhiteSpace(q.Prompt) &&
                    q.Options != null && q.Options.Count >= 2 && q.Options.Count <= 4 &&
                    q.CorrectAnswerIndex >= 0 && q.CorrectAnswerIndex < q.Options.Count).ToList();
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
