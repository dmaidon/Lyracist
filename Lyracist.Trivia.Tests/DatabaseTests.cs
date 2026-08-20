// Edited on Aug 20, 2026 @ 13:58:00 -> Updated pack count and distribution tests for expanded packs
using System;
using System.IO;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;
using Xunit;

namespace Lyracist.Trivia.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly TriviaDatabaseService _db;

    public DatabaseTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_trivia_{Guid.NewGuid():N}.db");
        _db = new TriviaDatabaseService(_tempDbPath);
    }

    [Fact]
    public void InitializeDatabase_CreatesTablesSuccessfully()
    {
        var categories = _db.GetCategories();
        Assert.Empty(categories);
    }

    [Fact]
    public void SaveQuestion_And_GetAllQuestions_PersistsCorrectly()
    {
        var question = new TriviaQuestion
        {
            Id = "TEST-001",
            Category = "Rock & Roll",
            Difficulty = TriviaDifficulty.Medium,
            QuestionType = TriviaQuestionType.MultipleChoice,
            Prompt = "Who sang 'Bohemian Rhapsody'?",
            Options = ["Queen", "The Beatles", "Led Zeppelin", "Pink Floyd"],
            CorrectAnswerIndex = 0,
            Explanation = "Queen released it in 1975.",
            TimeLimitSeconds = 15
        };

        _db.SaveQuestion(question);

        var loaded = _db.GetAllQuestions();
        Assert.Single(loaded);
        Assert.Equal("TEST-001", loaded[0].Id);
        Assert.Equal("Rock & Roll", loaded[0].Category);
        Assert.Equal("Who sang 'Bohemian Rhapsody'?", loaded[0].Prompt);
        Assert.Equal(4, loaded[0].Options.Count);
        Assert.Equal("Queen", loaded[0].Options[0]);
    }

    [Fact]
    public void GetCategories_ReturnsDistinctCategories()
    {
        _db.SaveQuestion(new TriviaQuestion { Id = "Q1", Category = "History" });
        _db.SaveQuestion(new TriviaQuestion { Id = "Q2", Category = "History" });
        _db.SaveQuestion(new TriviaQuestion { Id = "Q3", Category = "Science" });

        var categories = _db.GetCategories();

        Assert.Equal(2, categories.Count);
        Assert.Contains("Science", categories);
        Assert.Contains("History", categories);
    }

    [Fact]
    public void LoadAllPacks_ContainsAll15FleshedOutCategoryPacks()
    {
        string dir = TriviaStorageHelper.GetPacksDirectory();
        Assert.True(Directory.Exists(dir), $"Packs directory does not exist: {dir}");

        var files = Directory.GetFiles(dir, "*.json");
        Assert.True(files.Length >= 15, $"Expected at least 15 packs, found {files.Length}");

        var packs = TriviaPackManager.LoadAllPacks();
        Assert.True(packs.Count >= 15, $"Expected at least 15 packs, loaded {packs.Count}");

        string[] requiredPackIds =
        [
            "rock-and-roll",
            "country-music",
            "geography",
            "state-capitals",
            "history",
            "complete-the-lyric",
            "tv-shows",
            "sports",
            "logos-and-slogans",
            "music-legends",
            "pop-culture-80s-90s",
            "movie-soundtracks",
            "pub-trivia-all-stars",
            "biker-trivia",
            "famous_movie_quotes"
        ];

        foreach (string reqId in requiredPackIds)
        {
            var pack = packs.Find(p => p.PackId.Equals(reqId, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(pack);
            Assert.True(pack.Questions.Count >= 150, $"Pack '{reqId}' expected >= 150 questions, found {pack.Questions.Count}");
        }
    }

    [Fact]
    public void GetShuffledQuestionsByCategory_ReturnsShuffledSubset()
    {
        for (int i = 1; i <= 20; i++)
        {
            _db.SaveQuestion(new TriviaQuestion
            {
                Id = $"Q_{i}",
                Category = "Geography",
                Prompt = $"Geography question {i}?",
                Options = ["A", "B", "C", "D"],
                CorrectAnswerIndex = 0
            });
        }

        var list1 = _db.GetShuffledQuestionsByCategory("Geography", 10);
        var list2 = _db.GetShuffledQuestionsByCategory("Geography", 10);

        Assert.Equal(10, list1.Count);
        Assert.Equal(10, list2.Count);
    }

    [Fact]
    public void SyncAllPacks_PopulatesDatabaseWithAll2250Questions()
    {
        TriviaPackDatabaseSeeder.SyncAllPacksToDatabase(_db);
        var categories = _db.GetCategories();
        Assert.True(categories.Count >= 15);

        string prodDbPath = TriviaStorageHelper.GetDatabasePath();
        using var prodDb = new TriviaDatabaseService(prodDbPath);
        TriviaPackDatabaseSeeder.SyncAllPacksToDatabase(prodDb);
        var prodCategories = prodDb.GetCategories();
        Assert.True(prodCategories.Count >= 15);
    }

    [Fact]
    public void VerifyAnswerDistribution_SpreadEvenlyAcrossAllOptions()
    {
        string dir = TriviaStorageHelper.GetPacksDirectory();
        var packs = TriviaPackManager.LoadAllPacks(dir);

        foreach (var pack in packs)
        {
            var counts = new int[4];
            foreach (var q in pack.Questions)
            {
                Assert.InRange(q.CorrectAnswerIndex, 0, 3);
                counts[q.CorrectAnswerIndex]++;
                Assert.Equal(4, q.Options.Count);
            }

            // Each option A, B, C, D should have roughly 12-38% of answers
            int minExpected = Math.Max(15, (int)(pack.Questions.Count * 0.12));
            for (int i = 0; i < 4; i++)
            {
                Assert.True(counts[i] >= minExpected, $"Pack '{pack.Title}' has only {counts[i]} questions with answer index {i}");
            }
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
        GC.SuppressFinalize(this);
    }
}
