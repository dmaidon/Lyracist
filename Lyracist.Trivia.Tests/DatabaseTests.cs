// Edited on Aug 19, 2026 @ 09:11:30 -> Update tests to verify 14 packs including 150-question biker-trivia pack
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
    public void SaveQuestion_PersistsAndRetrievesQuestion()
    {
        var q = new TriviaQuestion
        {
            Id = "TEST-01",
            Category = "Movies",
            Difficulty = TriviaDifficulty.Easy,
            Prompt = "What was the name of the ship in Alien?",
            Options = ["Nostromo", "Sulaco", "Prometheus", "Covenant"],
            CorrectAnswerIndex = 0,
            Explanation = "The USCSS Nostromo was a commercial towing spaceship."
        };

        _db.SaveQuestion(q);

        var retrieved = _db.GetAllQuestions("Movies");
        Assert.Single(retrieved);
        Assert.Equal("TEST-01", retrieved[0].Id);
        Assert.Equal("What was the name of the ship in Alien?", retrieved[0].Prompt);
        Assert.Equal(4, retrieved[0].Options.Count);
        Assert.Equal("Nostromo", retrieved[0].CorrectAnswerText);
    }

    [Fact]
    public void GetCategories_ReturnsDistinctCategories()
    {
        _db.SaveQuestion(new TriviaQuestion { Id = "Q1", Category = "Science" });
        _db.SaveQuestion(new TriviaQuestion { Id = "Q2", Category = "History" });
        _db.SaveQuestion(new TriviaQuestion { Id = "Q3", Category = "Science" });

        var categories = _db.GetCategories();

        Assert.Equal(2, categories.Count);
        Assert.Contains("Science", categories);
        Assert.Contains("History", categories);
    }

    [Fact]
    public void LoadAllPacks_ContainsAll14FleshedOutCategoryPacks()
    {
        string dir = TriviaStorageHelper.GetPacksDirectory();
        Assert.True(Directory.Exists(dir), $"Packs directory does not exist: {dir}");

        var files = Directory.GetFiles(dir, "*.json");
        Assert.Equal(14, files.Length);

        var packs = TriviaPackManager.LoadAllPacks();
        Assert.Equal(14, packs.Count);

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
            "biker-trivia"
        ];

        foreach (string reqId in requiredPackIds)
        {
            var pack = packs.Find(p => p.PackId.Equals(reqId, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(pack);
            Assert.True(pack.Questions.Count >= 100, $"Pack {reqId} should have at least 100 questions but had {pack.Questions.Count}");
            if (reqId == "biker-trivia")
            {
                Assert.Equal(150, pack.Questions.Count);
            }
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
