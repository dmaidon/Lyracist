// Created on Aug 17, 2026 @ 13:02:00 -> SQLite TriviaDatabaseService for persistence
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Lyracist.Trivia.Core.Models;

namespace Lyracist.Trivia.Core.Services;

public class TriviaDatabaseService : IDisposable
{
    private readonly string _connectionString;
    private readonly object _lock = new();
    private bool _disposed;

    public TriviaDatabaseService(string? customDbPath = null)
    {
        string dbPath = customDbPath ?? GetDefaultDbPath();
        string dir = Path.GetDirectoryName(dbPath)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={dbPath};";
        InitializeDatabase();
    }

    public static string GetDefaultDbPath()
    {
        return TriviaStorageHelper.GetDatabasePath();
    }

    private void InitializeDatabase()
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Questions (
                    Id TEXT PRIMARY KEY,
                    Category TEXT NOT NULL,
                    Difficulty INTEGER NOT NULL,
                    QuestionType INTEGER NOT NULL,
                    Prompt TEXT NOT NULL,
                    OptionsJson TEXT NOT NULL,
                    CorrectAnswerIndex INTEGER NOT NULL,
                    Explanation TEXT,
                    AudioSnippetPath TEXT,
                    TimeLimitSeconds INTEGER NOT NULL DEFAULT 15,
                    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS GameSessions (
                    SessionId TEXT PRIMARY KEY,
                    Title TEXT NOT NULL,
                    VenueName TEXT,
                    StartedAt DATETIME NOT NULL,
                    EndedAt DATETIME,
                    TotalQuestionsPlayed INTEGER NOT NULL DEFAULT 0,
                    WinningTeamName TEXT,
                    WinningScore INTEGER NOT NULL DEFAULT 0,
                    SummaryJson TEXT
                );

                CREATE TABLE IF NOT EXISTS PlayerLeaderboard (
                    PlayerName TEXT PRIMARY KEY,
                    TeamName TEXT,
                    TotalGamesPlayed INTEGER NOT NULL DEFAULT 0,
                    TotalWins INTEGER NOT NULL DEFAULT 0,
                    LifetimeScore INTEGER NOT NULL DEFAULT 0,
                    HighestSingleGameScore INTEGER NOT NULL DEFAULT 0,
                    LongestStreak INTEGER NOT NULL DEFAULT 0,
                    LastPlayedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public void SaveQuestion(TriviaQuestion question)
    {
        lock (_lock)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Questions (Id, Category, Difficulty, QuestionType, Prompt, OptionsJson, CorrectAnswerIndex, Explanation, AudioSnippetPath, TimeLimitSeconds)
                VALUES ($id, $category, $diff, $qtype, $prompt, $options, $correct, $expl, $audio, $time)
                ON CONFLICT(Id) DO UPDATE SET
                    Category = excluded.Category,
                    Difficulty = excluded.Difficulty,
                    QuestionType = excluded.QuestionType,
                    Prompt = excluded.Prompt,
                    OptionsJson = excluded.OptionsJson,
                    CorrectAnswerIndex = excluded.CorrectAnswerIndex,
                    Explanation = excluded.Explanation,
                    AudioSnippetPath = excluded.AudioSnippetPath,
                    TimeLimitSeconds = excluded.TimeLimitSeconds;
            ";
            cmd.Parameters.AddWithValue("$id", question.Id);
            cmd.Parameters.AddWithValue("$category", question.Category);
            cmd.Parameters.AddWithValue("$diff", (int)question.Difficulty);
            cmd.Parameters.AddWithValue("$qtype", (int)question.QuestionType);
            cmd.Parameters.AddWithValue("$prompt", question.Prompt);
            cmd.Parameters.AddWithValue("$options", JsonSerializer.Serialize(question.Options));
            cmd.Parameters.AddWithValue("$correct", question.CorrectAnswerIndex);
            cmd.Parameters.AddWithValue("$expl", (object?)question.Explanation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$audio", (object?)question.AudioSnippetPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$time", question.TimeLimitSeconds);

            cmd.ExecuteNonQuery();
        }
    }

    public List<TriviaQuestion> GetAllQuestions(string? categoryFilter = null)
    {
        var list = new List<TriviaQuestion>();
        lock (_lock)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            if (string.IsNullOrWhiteSpace(categoryFilter) || categoryFilter == "All")
            {
                cmd.CommandText = "SELECT Id, Category, Difficulty, QuestionType, Prompt, OptionsJson, CorrectAnswerIndex, Explanation, AudioSnippetPath, TimeLimitSeconds FROM Questions ORDER BY Category, Prompt;";
            }
            else
            {
                cmd.CommandText = "SELECT Id, Category, Difficulty, QuestionType, Prompt, OptionsJson, CorrectAnswerIndex, Explanation, AudioSnippetPath, TimeLimitSeconds FROM Questions WHERE Category = $category ORDER BY Prompt;";
                cmd.Parameters.AddWithValue("$category", categoryFilter);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string optionsJson = reader.GetString(5);
                var options = JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];

                list.Add(new TriviaQuestion
                {
                    Id = reader.GetString(0),
                    Category = reader.GetString(1),
                    Difficulty = (TriviaDifficulty)reader.GetInt32(2),
                    QuestionType = (TriviaQuestionType)reader.GetInt32(3),
                    Prompt = reader.GetString(4),
                    Options = options,
                    CorrectAnswerIndex = reader.GetInt32(6),
                    Explanation = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    AudioSnippetPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                    TimeLimitSeconds = reader.GetInt32(9)
                });
            }
        }
        return list;
    }

    public List<string> GetCategories()
    {
        var list = new List<string>();
        lock (_lock)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT Category FROM Questions ORDER BY Category;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(reader.GetString(0));
            }
        }
        return list;
    }

    public List<TriviaQuestion> GetShuffledQuestionsByCategory(string category, int? count = null)
    {
        var list = GetAllQuestions(category);
        var shuffled = list.OrderBy(_ => Random.Shared.Next()).ToList();
        return count.HasValue ? shuffled.Take(count.Value).ToList() : shuffled;
    }

    public void SeedPackIntoDatabase(TriviaQuestionPack pack)
    {
        // Seeding runs on every app startup for every pack (~150 questions each). Calling
        // SaveQuestion() per row used to open/close a fresh SqliteConnection per question, which
        // measurably slowed every launch. Reuse one connection, one prepared command, and one
        // transaction for the whole pack instead.
        if (pack.Questions.Count == 0) return;

        lock (_lock)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var transaction = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO Questions (Id, Category, Difficulty, QuestionType, Prompt, OptionsJson, CorrectAnswerIndex, Explanation, AudioSnippetPath, TimeLimitSeconds)
                VALUES ($id, $category, $diff, $qtype, $prompt, $options, $correct, $expl, $audio, $time)
                ON CONFLICT(Id) DO UPDATE SET
                    Category = excluded.Category,
                    Difficulty = excluded.Difficulty,
                    QuestionType = excluded.QuestionType,
                    Prompt = excluded.Prompt,
                    OptionsJson = excluded.OptionsJson,
                    CorrectAnswerIndex = excluded.CorrectAnswerIndex,
                    Explanation = excluded.Explanation,
                    AudioSnippetPath = excluded.AudioSnippetPath,
                    TimeLimitSeconds = excluded.TimeLimitSeconds;
            ";

            var pId = cmd.Parameters.Add("$id", SqliteType.Text);
            var pCategory = cmd.Parameters.Add("$category", SqliteType.Text);
            var pDiff = cmd.Parameters.Add("$diff", SqliteType.Integer);
            var pQtype = cmd.Parameters.Add("$qtype", SqliteType.Integer);
            var pPrompt = cmd.Parameters.Add("$prompt", SqliteType.Text);
            var pOptions = cmd.Parameters.Add("$options", SqliteType.Text);
            var pCorrect = cmd.Parameters.Add("$correct", SqliteType.Integer);
            var pExpl = cmd.Parameters.Add("$expl", SqliteType.Text);
            var pAudio = cmd.Parameters.Add("$audio", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$time", SqliteType.Integer);
            cmd.Prepare();

            foreach (var q in pack.Questions)
            {
                pId.Value = q.Id;
                pCategory.Value = q.Category;
                pDiff.Value = (int)q.Difficulty;
                pQtype.Value = (int)q.QuestionType;
                pPrompt.Value = q.Prompt;
                pOptions.Value = JsonSerializer.Serialize(q.Options);
                pCorrect.Value = q.CorrectAnswerIndex;
                pExpl.Value = (object?)q.Explanation ?? DBNull.Value;
                pAudio.Value = (object?)q.AudioSnippetPath ?? DBNull.Value;
                pTime.Value = q.TimeLimitSeconds;

                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
