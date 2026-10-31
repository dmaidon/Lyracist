// Edited on Oct 31, 2026 @ 13:22:00 -> Fix banner file lock with unlocked BitmapSource, sync deleted/renamed question IDs, add unsaved-changes guard, difficulty filter, and CSV import
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Trivia.Core.Models;
using Lyracist.Trivia.Core.Services;
using Lyracist.Trivia.Services;
using Microsoft.Win32;

namespace TriviaDbCreator.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly HashSet<string> _removedQuestionIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSuppressingDirtyTracking;
    private bool _isRevertingPackSelection;
    private TriviaQuestionPack? _previousPack;

    public ObservableCollection<TriviaQuestionPack> Packs { get; } = [];
    public ObservableCollection<TriviaQuestionPack> FilteredPacks { get; } = [];
    public ObservableCollection<TriviaQuestion> PackQuestions { get; } = [];
    public ObservableCollection<TriviaQuestion> FilteredQuestions { get; } = [];

    [ObservableProperty]
    private string _packSearchText = string.Empty;

    [ObservableProperty]
    private string _questionSearchText = string.Empty;

    [ObservableProperty]
    private string _selectedDifficultyFilter = "All";

    [ObservableProperty]
    private TriviaQuestionPack? _selectedPack;

    [ObservableProperty]
    private string _selectedPackFilePath = string.Empty;

    [ObservableProperty]
    private string _packTitle = string.Empty;

    [ObservableProperty]
    private string _packId = string.Empty;

    [ObservableProperty]
    private string _packCategory = string.Empty;

    [ObservableProperty]
    private string _packDescription = string.Empty;

    [ObservableProperty]
    private string? _bannerPath;

    [ObservableProperty]
    private BitmapSource? _bannerImage;

    [ObservableProperty]
    private bool _hasBanner;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private TriviaQuestion? _selectedQuestion;

    // Active Editor Fields
    [ObservableProperty]
    private string _editorId = string.Empty;

    [ObservableProperty]
    private string _editorPrompt = string.Empty;

    [ObservableProperty]
    private string _editorOptionA = string.Empty;

    [ObservableProperty]
    private string _editorOptionB = string.Empty;

    [ObservableProperty]
    private string _editorOptionC = string.Empty;

    [ObservableProperty]
    private string _editorOptionD = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrectA))]
    [NotifyPropertyChangedFor(nameof(IsCorrectB))]
    [NotifyPropertyChangedFor(nameof(IsCorrectC))]
    [NotifyPropertyChangedFor(nameof(IsCorrectD))]
    private int _editorCorrectIndex;

    public bool IsCorrectA
    {
        get => EditorCorrectIndex == 0;
        set { if (value) EditorCorrectIndex = 0; }
    }

    public bool IsCorrectB
    {
        get => EditorCorrectIndex == 1;
        set { if (value) EditorCorrectIndex = 1; }
    }

    public bool IsCorrectC
    {
        get => EditorCorrectIndex == 2;
        set { if (value) EditorCorrectIndex = 2; }
    }

    public bool IsCorrectD
    {
        get => EditorCorrectIndex == 3;
        set { if (value) EditorCorrectIndex = 3; }
    }

    [ObservableProperty]
    private string _editorDifficulty = "Easy";

    [ObservableProperty]
    private string _editorExplanation = string.Empty;

    [ObservableProperty]
    private int _editorTimeLimit = 15;

    [ObservableProperty]
    private bool _isNewQuestionMode;

    [ObservableProperty]
    private string _statusMessage = "Ready. Select or create a trivia category database.";

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private string _distributionText = "A: 0 | B: 0 | C: 0 | D: 0";

    [ObservableProperty]
    private string _difficultySummaryText = "Easy: 0 | Med: 0 | Hard: 0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDistributionWarning))]
    private string? _distributionWarning;

    public bool HasDistributionWarning => !string.IsNullOrEmpty(DistributionWarning);

    public ObservableCollection<TriviaHelpTopic> HelpTopics { get; } = [];

    [ObservableProperty]
    private TriviaHelpTopic? _selectedHelpTopic;

    private static readonly string s_appVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    public string AppName => "Lyracist Trivia DB Creator";
    public string AppVersion => s_appVersion;
    public string Company => "PAROLE Software";
    public string Author => "Dennis N. Maidon";
    public string Copyright => "Copyright © 2026 PAROLE Software. Licensed under GPL-3.0-or-later.";
    public string AppDescription => "Interactive authoring studio and management tool for Lyracist Live Trivia databases. Create custom category packs, write and edit 4-option questions with explanations, balance answer choices evenly across A/B/C/D, generate matching 16:9 announcement banners, and sync directly into SQLite trivia.db.";

    public string[] DifficultyOptions { get; } = ["Easy", "Medium", "Hard"];
    public string[] DifficultyFilterOptions { get; } = ["All", "Easy", "Medium", "Hard"];

    public MainViewModel()
    {
        InitializeHelpTopics();
        LoadPacks();
    }

    partial void OnPackTitleChanged(string value) { if (!_isSuppressingDirtyTracking) HasUnsavedChanges = true; }
    partial void OnPackIdChanged(string value) { if (!_isSuppressingDirtyTracking) HasUnsavedChanges = true; }
    partial void OnPackCategoryChanged(string value) { if (!_isSuppressingDirtyTracking) HasUnsavedChanges = true; }
    partial void OnPackDescriptionChanged(string value) { if (!_isSuppressingDirtyTracking) HasUnsavedChanges = true; }

    partial void OnPackSearchTextChanged(string value)
    {
        FilterPacks();
    }

    partial void OnQuestionSearchTextChanged(string value)
    {
        FilterQuestions();
    }

    partial void OnSelectedDifficultyFilterChanged(string value)
    {
        FilterQuestions();
    }

    public bool ConfirmSaveOrDiscardChanges()
    {
        if (!HasUnsavedChanges || _previousPack == null) return true;

        var res = MessageBox.Show(
            $"You have unsaved changes in '{(string.IsNullOrWhiteSpace(PackTitle) ? _previousPack.Title : PackTitle)}'.\n\nSave changes before continuing?",
            "Unsaved Pack Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (res == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (res == MessageBoxResult.Yes)
        {
            SavePackInternal(_previousPack);
        }
        else
        {
            HasUnsavedChanges = false;
            _removedQuestionIds.Clear();
        }

        return true;
    }

    partial void OnSelectedPackChanged(TriviaQuestionPack? value)
    {
        if (_isRevertingPackSelection) return;

        if (HasUnsavedChanges && _previousPack != null && !ReferenceEquals(_previousPack, value))
        {
            if (!ConfirmSaveOrDiscardChanges())
            {
                _isRevertingPackSelection = true;
                SelectedPack = _previousPack;
                _isRevertingPackSelection = false;
                return;
            }
        }

        _previousPack = value;
        _removedQuestionIds.Clear();

        if (value == null)
        {
            _isSuppressingDirtyTracking = true;
            PackTitle = string.Empty;
            PackId = string.Empty;
            PackCategory = string.Empty;
            PackDescription = string.Empty;
            _isSuppressingDirtyTracking = false;
            HasUnsavedChanges = false;
            PackQuestions.Clear();
            FilteredQuestions.Clear();
            SelectedQuestion = null;
            SelectedPackFilePath = string.Empty;
            BannerPath = null;
            BannerImage = null;
            HasBanner = false;
            DistributionText = "A: 0 | B: 0 | C: 0 | D: 0";
            DifficultySummaryText = "Easy: 0 | Med: 0 | Hard: 0";
            DistributionWarning = null;
            return;
        }

        _isSuppressingDirtyTracking = true;
        PackTitle = value.Title;
        PackId = value.PackId;
        PackCategory = value.Category;
        PackDescription = value.Description;
        _isSuppressingDirtyTracking = false;
        HasUnsavedChanges = false;

        string packsDir = TriviaStorageHelper.GetPacksDirectory();
        string cleanName = CleanFileName(string.IsNullOrWhiteSpace(value.PackId) ? value.Title : value.PackId);
        SelectedPackFilePath = Path.Combine(packsDir, $"{cleanName}.json");

        PackQuestions.Clear();
        foreach (var q in value.Questions)
        {
            PackQuestions.Add(q);
        }

        FilterQuestions();
        RecalculateDistribution();
        UpdateBannerInfo();

        SelectedQuestion = PackQuestions.FirstOrDefault();
    }

    partial void OnSelectedQuestionChanged(TriviaQuestion? value)
    {
        if (value == null)
        {
            if (!IsNewQuestionMode)
            {
                ClearEditorFields();
            }
            return;
        }

        IsNewQuestionMode = false;
        EditorId = value.Id;
        EditorPrompt = value.Prompt;
        EditorOptionA = value.Options.Count > 0 ? value.Options[0] : string.Empty;
        EditorOptionB = value.Options.Count > 1 ? value.Options[1] : string.Empty;
        EditorOptionC = value.Options.Count > 2 ? value.Options[2] : string.Empty;
        EditorOptionD = value.Options.Count > 3 ? value.Options[3] : string.Empty;
        EditorCorrectIndex = Math.Clamp(value.CorrectAnswerIndex, 0, 3);
        EditorDifficulty = value.Difficulty.ToString();
        EditorExplanation = value.Explanation;
        EditorTimeLimit = value.TimeLimitSeconds > 0 ? value.TimeLimitSeconds : 15;
    }

    private void ClearEditorFields()
    {
        EditorId = string.Empty;
        EditorPrompt = string.Empty;
        EditorOptionA = string.Empty;
        EditorOptionB = string.Empty;
        EditorOptionC = string.Empty;
        EditorOptionD = string.Empty;
        EditorCorrectIndex = 0;
        EditorDifficulty = "Easy";
        EditorExplanation = string.Empty;
        EditorTimeLimit = 15;
    }

    public void LoadPacks()
    {
        try
        {
            string dir = TriviaStorageHelper.GetPacksDirectory();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var loaded = TriviaPackManager.LoadAllPacks(dir);
            Packs.Clear();
            foreach (var pack in loaded.OrderBy(p => p.Title))
            {
                Packs.Add(pack);
            }

            FilterPacks();

            if (Packs.Count > 0)
            {
                SelectedPack = Packs.FirstOrDefault();
                SetStatus($"Loaded {Packs.Count} trivia category packs.");
            }
            else
            {
                SetStatus("No category databases found. Click '+ New Pack' to create one.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading packs: {ex.Message}", isError: true);
        }
    }

    public void FilterPacks()
    {
        FilteredPacks.Clear();
        string query = PackSearchText.Trim();
        foreach (var p in Packs)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.PackId.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                FilteredPacks.Add(p);
            }
        }
    }

    public void FilterQuestions()
    {
        FilteredQuestions.Clear();
        string query = QuestionSearchText.Trim();
        string diffFilter = SelectedDifficultyFilter;
        bool filterByDiff = !string.IsNullOrWhiteSpace(diffFilter) && !string.Equals(diffFilter, "All", StringComparison.OrdinalIgnoreCase);

        foreach (var q in PackQuestions)
        {
            if (filterByDiff && !string.Equals(q.Difficulty.ToString(), diffFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(query) ||
                q.Prompt.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                q.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                q.Explanation.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                q.Options.Any(opt => opt.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredQuestions.Add(q);
            }
        }
    }

    public void RecalculateDistribution()
    {
        if (PackQuestions.Count == 0)
        {
            DistributionText = "A: 0 (0%) | B: 0 (0%) | C: 0 (0%) | D: 0 (0%)";
            DifficultySummaryText = "Easy: 0 | Med: 0 | Hard: 0";
            DistributionWarning = null;
            return;
        }

        var counts = new int[4];
        int easyCount = 0, medCount = 0, hardCount = 0;
        foreach (var q in PackQuestions)
        {
            int idx = Math.Clamp(q.CorrectAnswerIndex, 0, 3);
            counts[idx]++;
            switch (q.Difficulty)
            {
                case TriviaDifficulty.Easy: easyCount++; break;
                case TriviaDifficulty.Medium: medCount++; break;
                case TriviaDifficulty.Hard: hardCount++; break;
            }
        }

        int total = PackQuestions.Count;
        double pctA = counts[0] * 100.0 / total;
        double pctB = counts[1] * 100.0 / total;
        double pctC = counts[2] * 100.0 / total;
        double pctD = counts[3] * 100.0 / total;

        DistributionText = $"▲ A: {counts[0]} ({pctA:F0}%) | ◆ B: {counts[1]} ({pctB:F0}%) | ● C: {counts[2]} ({pctC:F0}%) | ■ D: {counts[3]} ({pctD:F0}%)";
        DifficultySummaryText = $"Easy: {easyCount} | Med: {medCount} | Hard: {hardCount}";

        // Check if any option is skewed heavily (> 38%)
        if (pctA > 38 || pctB > 38 || pctC > 38 || pctD > 38 || (total >= 4 && counts.Any(c => c == 0)))
        {
            DistributionWarning = "⚠️ Answer options are unevenly distributed. Click '🔀 Balance Options' to evenly spread correct answers across A/B/C/D.";
        }
        else
        {
            DistributionWarning = null;
        }
    }

    public void UpdateBannerInfo()
    {
        if (SelectedPack == null)
        {
            BannerPath = null;
            BannerImage = null;
            HasBanner = false;
            return;
        }

        string lookupId = string.IsNullOrWhiteSpace(PackId) ? SelectedPack.PackId : PackId;
        string lookupTitle = string.IsNullOrWhiteSpace(PackTitle) ? SelectedPack.Title : PackTitle;

        string? banner = TriviaStorageHelper.GetBannerPathForPack(lookupId);
        if (string.IsNullOrEmpty(banner))
        {
            banner = TriviaStorageHelper.GetBannerPathForPack(lookupTitle);
        }

        BannerPath = banner;
        HasBanner = !string.IsNullOrEmpty(banner) && File.Exists(banner);
        BannerImage = LoadBannerBitmap(banner);
    }

    private static BitmapSource? LoadBannerBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.StreamSource = stream;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private void NewPack()
    {
        if (!ConfirmSaveOrDiscardChanges()) return;

        int suffix = 1;
        string candidateId = "new_category_pack";
        string candidateTitle = "New Category Database";
        while (Packs.Any(p => string.Equals(p.PackId, candidateId, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(p.Title, candidateTitle, StringComparison.OrdinalIgnoreCase)))
        {
            suffix++;
            candidateId = $"new_category_pack_{suffix}";
            candidateTitle = $"New Category Database {suffix}";
        }

        var newPack = new TriviaQuestionPack
        {
            PackId = candidateId,
            Title = candidateTitle,
            Category = "General Trivia",
            Description = "Custom trivia category database.",
            Questions = []
        };

        Packs.Add(newPack);
        FilterPacks();
        SelectedPack = newPack;
        NewQuestion();
        HasUnsavedChanges = true;
        SetStatus("Created new category pack. Enter metadata and questions, then click 'Save Pack'.");
    }

    [RelayCommand]
    private void SavePack()
    {
        if (SelectedPack == null) return;
        SavePackInternal(SelectedPack);
    }

    private void SavePackInternal(TriviaQuestionPack targetPack)
    {
        if (string.IsNullOrWhiteSpace(PackTitle))
        {
            SetStatus("Pack Title cannot be empty.", isError: true);
            return;
        }

        targetPack.Title = PackTitle.Trim();
        targetPack.PackId = string.IsNullOrWhiteSpace(PackId) ? CleanFileName(PackTitle) : CleanFileName(PackId);
        targetPack.Category = string.IsNullOrWhiteSpace(PackCategory) ? "General" : PackCategory.Trim();
        targetPack.Description = PackDescription.Trim();
        targetPack.Questions = PackQuestions.ToList();

        string packsDir = TriviaStorageHelper.GetPacksDirectory();
        string cleanName = targetPack.PackId.Replace("-", "_");
        string filePath = Path.Combine(packsDir, $"{cleanName}.json");

        try
        {
            string json = JsonSerializer.Serialize(targetPack, JsonOptions);
            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);

            // If the pack file was renamed, clean up the previous file
            if (!string.IsNullOrEmpty(SelectedPackFilePath) &&
                File.Exists(SelectedPackFilePath) &&
                !string.Equals(SelectedPackFilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(SelectedPackFilePath); } catch { }
            }

            SelectedPackFilePath = filePath;

            // Remove any deleted/renamed question IDs from SQLite before syncing
            if (_removedQuestionIds.Count > 0)
            {
                try
                {
                    string dbPath = TriviaStorageHelper.GetDatabasePath();
                    using var db = new TriviaDatabaseService(dbPath);
                    db.DeleteQuestionsByIds(_removedQuestionIds);
                }
                catch { }
                _removedQuestionIds.Clear();
            }

            // Sync to database
            SyncDatabaseQuiet();
            HasUnsavedChanges = false;

            SetStatus($"Saved '{targetPack.Title}' ({targetPack.Questions.Count} questions) to {Path.GetFileName(filePath)}.");
            FilterPacks();
        }
        catch (Exception ex)
        {
            SetStatus($"Error saving pack: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    private void NewQuestion()
    {
        IsNewQuestionMode = true;
        SelectedQuestion = null;

        string prefix = "Q";
        if (!string.IsNullOrWhiteSpace(PackTitle))
        {
            var words = PackTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            prefix = words.Length >= 2 
                ? $"{words[0][0]}{words[1][0]}".ToUpperInvariant() 
                : PackTitle.Substring(0, Math.Min(3, PackTitle.Length)).ToUpperInvariant();
        }

        int maxNum = 0;
        foreach (var q in PackQuestions)
        {
            if (q.Id.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = q.Id[(prefix.Length + 1)..];
                if (int.TryParse(suffix, out int parsed) && parsed > maxNum)
                {
                    maxNum = parsed;
                }
            }
        }
        int nextNum = Math.Max(PackQuestions.Count + 1, maxNum + 1);
        EditorId = $"{prefix}-{nextNum:D3}";
        EditorPrompt = string.Empty;
        EditorOptionA = string.Empty;
        EditorOptionB = string.Empty;
        EditorOptionC = string.Empty;
        EditorOptionD = string.Empty;
        EditorCorrectIndex = 0;
        EditorDifficulty = "Easy";
        EditorExplanation = string.Empty;
        EditorTimeLimit = 15;

        SetStatus($"Adding new question #{nextNum}. Fill out fields and click 'Save Question'.");
    }

    [RelayCommand]
    private void SaveQuestion()
    {
        if (string.IsNullOrWhiteSpace(EditorPrompt))
        {
            SetStatus("Question Prompt cannot be empty.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(EditorOptionA) ||
            string.IsNullOrWhiteSpace(EditorOptionB) ||
            string.IsNullOrWhiteSpace(EditorOptionC) ||
            string.IsNullOrWhiteSpace(EditorOptionD))
        {
            SetStatus("All 4 multiple-choice options (A, B, C, D) are required.", isError: true);
            return;
        }

        Enum.TryParse<TriviaDifficulty>(EditorDifficulty, true, out var diff);

        string candidateId = string.IsNullOrWhiteSpace(EditorId) ? $"Q-{PackQuestions.Count + 1:D3}" : EditorId.Trim();

        // Check for duplicate ID within the pack
        var duplicate = PackQuestions.FirstOrDefault(q =>
            !ReferenceEquals(q, SelectedQuestion) &&
            string.Equals(q.Id, candidateId, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            SetStatus($"Question ID '{candidateId}' is already used in this pack. Please choose a unique ID.", isError: true);
            return;
        }

        var question = new TriviaQuestion
        {
            Id = candidateId,
            Category = string.IsNullOrWhiteSpace(PackCategory) ? "General" : PackCategory.Trim(),
            Difficulty = diff,
            QuestionType = TriviaQuestionType.MultipleChoice,
            Prompt = EditorPrompt.Trim(),
            Options = [EditorOptionA.Trim(), EditorOptionB.Trim(), EditorOptionC.Trim(), EditorOptionD.Trim()],
            CorrectAnswerIndex = Math.Clamp(EditorCorrectIndex, 0, 3),
            Explanation = EditorExplanation.Trim(),
            TimeLimitSeconds = EditorTimeLimit > 0 ? EditorTimeLimit : 15
        };

        if (IsNewQuestionMode || SelectedQuestion == null)
        {
            PackQuestions.Add(question);
            if (SelectedPack != null) SelectedPack.Questions = PackQuestions.ToList();
            IsNewQuestionMode = false;
            SelectedQuestion = question;
            HasUnsavedChanges = true;
            SetStatus($"Added Question {question.Id} ({PackQuestions.Count} total). Click 'Save Pack' to persist.");
        }
        else
        {
            string oldId = SelectedQuestion.Id;
            int idx = PackQuestions.IndexOf(SelectedQuestion);
            if (idx >= 0)
            {
                if (!string.Equals(oldId, question.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _removedQuestionIds.Add(oldId);
                }
                PackQuestions[idx] = question;
                if (SelectedPack != null) SelectedPack.Questions = PackQuestions.ToList();
                SelectedQuestion = question;
                HasUnsavedChanges = true;
                SetStatus($"Updated Question {question.Id}. Click 'Save Pack' to persist.");
            }
        }

        FilterQuestions();
        RecalculateDistribution();
    }

    [RelayCommand]
    private void DuplicateQuestion()
    {
        if (SelectedQuestion == null) return;

        string baseCopyId = $"{SelectedQuestion.Id}-COPY";
        string copyId = baseCopyId;
        int copySuffix = 1;
        while (PackQuestions.Any(q => string.Equals(q.Id, copyId, StringComparison.OrdinalIgnoreCase)))
        {
            copySuffix++;
            copyId = $"{baseCopyId}{copySuffix}";
        }

        var dup = new TriviaQuestion
        {
            Id = copyId,
            Category = SelectedQuestion.Category,
            Difficulty = SelectedQuestion.Difficulty,
            QuestionType = SelectedQuestion.QuestionType,
            Prompt = $"{SelectedQuestion.Prompt} (Copy)",
            Options = new List<string>(SelectedQuestion.Options),
            CorrectAnswerIndex = SelectedQuestion.CorrectAnswerIndex,
            Explanation = SelectedQuestion.Explanation,
            TimeLimitSeconds = SelectedQuestion.TimeLimitSeconds
        };

        PackQuestions.Add(dup);
        if (SelectedPack != null) SelectedPack.Questions = PackQuestions.ToList();
        FilterQuestions();
        SelectedQuestion = dup;
        RecalculateDistribution();
        HasUnsavedChanges = true;
        SetStatus($"Duplicated Question as {dup.Id}. Click 'Save Pack' to persist.");
    }

    [RelayCommand]
    private void DeleteQuestion()
    {
        if (SelectedQuestion == null) return;

        var result = MessageBox.Show($"Are you sure you want to delete question '{SelectedQuestion.Id}'?", "Delete Question", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        string id = SelectedQuestion.Id;
        PackQuestions.Remove(SelectedQuestion);
        _removedQuestionIds.Add(id);
        if (SelectedPack != null) SelectedPack.Questions = PackQuestions.ToList();

        try
        {
            string dbPath = TriviaStorageHelper.GetDatabasePath();
            using var db = new TriviaDatabaseService(dbPath);
            db.DeleteQuestion(id);
        }
        catch { }

        FilterQuestions();
        SelectedQuestion = PackQuestions.FirstOrDefault();
        RecalculateDistribution();
        HasUnsavedChanges = true;
        SetStatus($"Deleted Question {id} ({PackQuestions.Count} remaining). Click 'Save Pack' to persist.");
    }

    [RelayCommand]
    private void BalanceOptions()
    {
        if (PackQuestions.Count == 0)
        {
            SetStatus("No questions to balance.", isError: true);
            return;
        }

        var prng = new Random();
        var targetIndices = new List<int>();
        for (int i = 0; i < PackQuestions.Count; i++)
        {
            targetIndices.Add(i % 4);
        }

        // Shuffle targetIndices
        for (int i = targetIndices.Count - 1; i > 0; i--)
        {
            int j = prng.Next(i + 1);
            (targetIndices[i], targetIndices[j]) = (targetIndices[j], targetIndices[i]);
        }

        for (int i = 0; i < PackQuestions.Count; i++)
        {
            var q = PackQuestions[i];
            if (q.Options.Count < 4) continue;

            string correctOption = q.Options[Math.Clamp(q.CorrectAnswerIndex, 0, q.Options.Count - 1)];
            var wrongOptions = q.Options.Where((_, idx) => idx != q.CorrectAnswerIndex).ToList();

            // Shuffle wrong options
            for (int w = wrongOptions.Count - 1; w > 0; w--)
            {
                int r = prng.Next(w + 1);
                (wrongOptions[w], wrongOptions[r]) = (wrongOptions[r], wrongOptions[w]);
            }

            int targetIndex = targetIndices[i];
            var newOptions = new List<string>(4);
            int wrongIdx = 0;
            for (int pos = 0; pos < 4; pos++)
            {
                if (pos == targetIndex)
                {
                    newOptions.Add(correctOption);
                }
                else
                {
                    newOptions.Add(wrongOptions[wrongIdx++]);
                }
            }

            q.Options = newOptions;
            q.CorrectAnswerIndex = targetIndex;
        }

        // Refresh UI
        var currentSel = SelectedQuestion;
        FilterQuestions();
        SelectedQuestion = currentSel;
        OnSelectedQuestionChanged(currentSel);
        RecalculateDistribution();
        HasUnsavedChanges = true;
        SetStatus($"Balanced option distributions across all {PackQuestions.Count} questions (~25% for A, B, C, D). Click 'Save Pack' to commit.");
    }

    [RelayCommand]
    private void GenerateBanner()
    {
        if (SelectedPack == null) return;

        try
        {
            string bannersDir = TriviaStorageHelper.GetBannersDirectory();
            if (!Directory.Exists(bannersDir))
            {
                Directory.CreateDirectory(bannersDir);
            }

            string clean = CleanFileName(string.IsNullOrWhiteSpace(PackId) ? PackTitle : PackId).Replace("-", "_");
            string bannerFile = Path.Combine(bannersDir, $"{clean}.png");

            // Release any in-memory reference before regenerating
            BannerImage = null;

            string titleToRender = PackTitle;
            string descToRender = PackDescription;
            Exception? renderError = null;

            // Execute banner generation on STA thread
            var thread = new Thread(() =>
            {
                try
                {
                    TriviaBannerGenerator.GenerateBannerForPack(bannersDir, $"{clean}.png", titleToRender, descToRender);
                }
                catch (Exception ex)
                {
                    renderError = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(5000);

            if (renderError != null)
            {
                SetStatus($"Error generating banner: {renderError.Message}", isError: true);
                return;
            }

            BannerPath = bannerFile;
            HasBanner = File.Exists(bannerFile);
            BannerImage = LoadBannerBitmap(bannerFile);
            SetStatus($"Banner generated: {Path.GetFileName(bannerFile)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error generating banner: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    private void SyncDatabase()
    {
        try
        {
            string dbPath = TriviaStorageHelper.GetDatabasePath();
            using var db = new TriviaDatabaseService(dbPath);
            TriviaPackDatabaseSeeder.SyncAllPacksToDatabase(db);
            SetStatus($"Synced all categories & questions into SQLite database: {Path.GetFileName(dbPath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error syncing database: {ex.Message}", isError: true);
        }
    }

    private void SyncDatabaseQuiet()
    {
        try
        {
            string dbPath = TriviaStorageHelper.GetDatabasePath();
            using var db = new TriviaDatabaseService(dbPath);
            TriviaPackDatabaseSeeder.SyncAllPacksToDatabase(db);
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void ImportPack()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Import Trivia JSON Pack",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                string json = File.ReadAllText(ofd.FileName);
                ReadOnlySpan<char> trimmed = json.AsSpan().TrimStart();
                if (trimmed.IsEmpty)
                {
                    SetStatus("Selected JSON file is empty.", isError: true);
                    return;
                }

                TriviaQuestionPack? pack = null;
                if (trimmed[0] == '[')
                {
                    var rawQuestions = JsonSerializer.Deserialize<List<TriviaQuestion>>(json, JsonOptions);
                    if (rawQuestions != null)
                    {
                        string baseName = Path.GetFileNameWithoutExtension(ofd.FileName);
                        pack = new TriviaQuestionPack
                        {
                            PackId = CleanFileName(baseName),
                            Title = baseName.Replace('_', ' ').Replace('-', ' '),
                            Category = rawQuestions.FirstOrDefault()?.Category ?? "General",
                            Description = $"Imported from {Path.GetFileName(ofd.FileName)}",
                            Questions = rawQuestions
                        };
                    }
                }
                else
                {
                    pack = JsonSerializer.Deserialize<TriviaQuestionPack>(json, JsonOptions);
                }

                if (pack != null && pack.Questions.Count > 0)
                {
                    // Validate questions in imported pack
                    var validQuestions = pack.Questions.Where(q => !string.IsNullOrWhiteSpace(q.Prompt) && q.Options != null && q.Options.Count >= 4).ToList();
                    if (validQuestions.Count == 0)
                    {
                        SetStatus("Pack contains no valid 4-option questions.", isError: true);
                        return;
                    }
                    pack.Questions = validQuestions;

                    string packsDir = TriviaStorageHelper.GetPacksDirectory();
                    string destFile = Path.Combine(packsDir, Path.GetFileName(ofd.FileName));

                    if (File.Exists(destFile))
                    {
                        var confirm = MessageBox.Show($"A pack named '{Path.GetFileName(destFile)}' already exists. Overwrite it?", "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (confirm != MessageBoxResult.Yes) return;
                    }

                    string tempPath = destFile + ".tmp";
                    File.WriteAllText(tempPath, JsonSerializer.Serialize(pack, JsonOptions));
                    File.Move(tempPath, destFile, overwrite: true);

                    SyncDatabaseQuiet();
                    LoadPacks();
                    SelectedPack = Packs.FirstOrDefault(p => p.Title == pack.Title || p.PackId == pack.PackId);
                    SetStatus($"Imported '{pack.Title}' with {pack.Questions.Count} questions and synced to SQLite.");
                }
                else
                {
                    SetStatus("Invalid or empty trivia JSON pack file.", isError: true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error importing pack: {ex.Message}", isError: true);
            }
        }
    }

    [RelayCommand]
    private void ImportCsv()
    {
        if (SelectedPack == null)
        {
            SetStatus("Select or create a category pack first before importing CSV questions.", isError: true);
            return;
        }

        var ofd = new OpenFileDialog
        {
            Title = "Import Questions from CSV (Prompt, OptionA, OptionB, OptionC, OptionD, CorrectAnswer, Difficulty, Explanation)",
            Filter = "CSV / Text Files (*.csv;*.txt)|*.csv;*.txt|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() != true) return;

        try
        {
            string[] lines = File.ReadAllLines(ofd.FileName);
            int imported = 0;

            string prefix = "Q";
            if (!string.IsNullOrWhiteSpace(PackTitle))
            {
                var words = PackTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                prefix = words.Length >= 2
                    ? $"{words[0][0]}{words[1][0]}".ToUpperInvariant()
                    : PackTitle[..Math.Min(3, PackTitle.Length)].ToUpperInvariant();
            }

            foreach (string rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;
                var cols = ParseCsvRow(rawLine);
                if (cols.Count < 6) continue;

                // Skip header row if first column is "Prompt" or "Question"
                if (imported == 0 && (cols[0].Equals("Prompt", StringComparison.OrdinalIgnoreCase) ||
                                      cols[0].Equals("Question", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string prompt = cols[0].Trim();
                string optA = cols[1].Trim();
                string optB = cols[2].Trim();
                string optC = cols[3].Trim();
                string optD = cols[4].Trim();
                string correctRaw = cols[5].Trim();

                if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(optA) ||
                    string.IsNullOrWhiteSpace(optB) || string.IsNullOrWhiteSpace(optC) || string.IsNullOrWhiteSpace(optD))
                {
                    continue;
                }

                int correctIndex = correctRaw.ToUpperInvariant() switch
                {
                    "A" or "0" => 0,
                    "B" or "1" => 1,
                    "C" or "2" => 2,
                    "D" or "3" => 3,
                    _ => string.Equals(correctRaw, optB, StringComparison.OrdinalIgnoreCase) ? 1
                       : string.Equals(correctRaw, optC, StringComparison.OrdinalIgnoreCase) ? 2
                       : string.Equals(correctRaw, optD, StringComparison.OrdinalIgnoreCase) ? 3
                       : 0
                };

                var diff = TriviaDifficulty.Easy;
                if (cols.Count >= 7 && Enum.TryParse<TriviaDifficulty>(cols[6].Trim(), true, out var parsedDiff))
                {
                    diff = parsedDiff;
                }

                string explanation = cols.Count >= 8 ? cols[7].Trim() : string.Empty;

                int nextNum = PackQuestions.Count + 1;
                string qId = $"{prefix}-{nextNum:D3}";
                while (PackQuestions.Any(q => string.Equals(q.Id, qId, StringComparison.OrdinalIgnoreCase)))
                {
                    nextNum++;
                    qId = $"{prefix}-{nextNum:D3}";
                }

                PackQuestions.Add(new TriviaQuestion
                {
                    Id = qId,
                    Category = string.IsNullOrWhiteSpace(PackCategory) ? "General" : PackCategory.Trim(),
                    Difficulty = diff,
                    QuestionType = TriviaQuestionType.MultipleChoice,
                    Prompt = prompt,
                    Options = [optA, optB, optC, optD],
                    CorrectAnswerIndex = correctIndex,
                    Explanation = explanation,
                    TimeLimitSeconds = 15
                });
                imported++;
            }

            if (imported > 0)
            {
                SelectedPack.Questions = PackQuestions.ToList();
                FilterQuestions();
                RecalculateDistribution();
                SelectedQuestion = PackQuestions.LastOrDefault();
                HasUnsavedChanges = true;
                SetStatus($"Imported {imported} question(s) from '{Path.GetFileName(ofd.FileName)}'. Click 'Save Pack' to commit.");
            }
            else
            {
                SetStatus("No valid 6+ column rows (Prompt, OptionA, OptionB, OptionC, OptionD, Correct) found in CSV.", isError: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error importing CSV: {ex.Message}", isError: true);
        }
    }

    private static List<string> ParseCsvRow(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if ((c == ',' || c == '\t') && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    [RelayCommand]
    private void ExportPack()
    {
        if (SelectedPack == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Trivia JSON Pack",
            Filter = "JSON Files (*.json)|*.json",
            FileName = $"{CleanFileName(SelectedPack.PackId)}.json"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                string json = JsonSerializer.Serialize(SelectedPack, JsonOptions);
                File.WriteAllText(sfd.FileName, json);
                SetStatus($"Exported '{SelectedPack.Title}' to {sfd.FileName}.");
            }
            catch (Exception ex)
            {
                SetStatus($"Error exporting pack: {ex.Message}", isError: true);
            }
        }
    }

    [RelayCommand]
    private void DeletePack()
    {
        if (SelectedPack == null) return;

        var res = MessageBox.Show($"Are you sure you want to permanently delete the pack '{SelectedPack.Title}'?", "Delete Category Pack", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            if (!string.IsNullOrEmpty(SelectedPackFilePath) && File.Exists(SelectedPackFilePath))
            {
                File.Delete(SelectedPackFilePath);
            }

            try
            {
                string dbPath = TriviaStorageHelper.GetDatabasePath();
                using var db = new TriviaDatabaseService(dbPath);
                if (SelectedPack.Questions.Count > 0)
                {
                    db.DeleteQuestionsByIds(SelectedPack.Questions.Select(q => q.Id));
                }
            }
            catch { }

            HasUnsavedChanges = false;
            _removedQuestionIds.Clear();
            Packs.Remove(SelectedPack);
            FilterPacks();
            SelectedPack = Packs.FirstOrDefault();
            SetStatus("Category pack deleted.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error deleting pack: {ex.Message}", isError: true);
        }
    }

    private void InitializeHelpTopics()
    {
        HelpTopics.Clear();

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "1. Quick Start & Overview",
            Icon = "📚",
            AccentColor = "#38BDF8",
            DescriptionHeader = "Overview of Lyracist Trivia Database Creator",
            DescriptionContent = 
@"Welcome to the **Lyracist Trivia Database Creator & Pack Studio**!

This application allows you to:
• **Author Custom Databases**: Create new category databases and write 4-choice trivia questions with rich explanations.
• **Edit Existing Packs**: Modify questions, update category titles/descriptions, and add new questions.
• **1-Click Option Balancing**: Automatically shuffle multiple-choice answers so that correct answers are evenly spread across choices A, B, C, and D (~25% each).
• **16:9 Banner Studio**: Generate high-resolution category announcement banners for big-screen projection during the pre-game lobby.
• **SQLite Database Sync**: Seamlessly sync your packs to `Data/trivia.db` so `LyracistTrivia`, `Lyracist`, and `KSRotation` can host them immediately.

**Workspace Layout**:
1. **Left Sidebar**: Browse, search, and manage category packs stored in `Packs/`.
2. **Center Panel**: Edit pack metadata (Title, ID, Category Tag, Description) and review the questions table.
3. **Right Panel**: Real-time Question Editor form with colored option cards, difficulty rating, and answer selection."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "2. Creating & Managing Databases",
            Icon = "➕",
            AccentColor = "#8B5CF6",
            DescriptionHeader = "How to Create and Configure Category Databases",
            DescriptionContent = 
@"### How to Create a New Trivia Category Database:
1. Click the **'➕ New Pack'** button in the top right of the Left Sidebar.
2. A blank pack template is created with a default category and ID.
3. Fill out the **Pack Metadata** fields in the top center card:
   • **Pack Title**: The human-readable name displayed on venue TV screens and mobile buzzers (e.g. *'Disney Classics & Fairy Tales'*).
   • **Pack ID (Slug)**: The file-safe identifier used for JSON filenames and banners (e.g. *'disney_classics'*).
   • **Category Tag**: The broad genre group (e.g. *'Movies & Animation'* or *'General'*).
   • **Description & Themes**: Subtitle summary of topics covered in the pack (e.g. *'Animated classics, Pixar, theme songs, villains, and trivia lore'*).
4. Add your questions (see Topic 3).
5. Click **'💾 Save Pack'** in the top header bar. The file is saved directly to `Packs/{slug}.json` and automatically indexed."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "3. Adding, Editing & Duplicating Questions",
            Icon = "✍️",
            AccentColor = "#10B981",
            DescriptionHeader = "Writing Questions, Setting Choices & Correct Answers",
            DescriptionContent = 
@"### Adding & Editing Questions:
1. **Create a New Question**: Click the **'➕ Add Question'** button above the questions table.
2. **Question Prompt**: Enter the main question text. You can use multi-line prompts for lyrics or longer scenarios.
3. **4 Multiple-Choice Options**:
   • Enter choices for **Option A (▲ Purple)**, **Option B (◆ Cyan)**, **Option C (● Amber)**, and **Option D (■ Rose)**.
   • All 4 choices are required for multiple-choice trivia rounds.
4. **Select Correct Answer**: Click the **Radio Button** on the right side of the option card that represents the correct answer. The selected card will be designated as the winning answer.
5. **Difficulty & Time Limit**: Select `Easy`, `Medium`, or `Hard` and set the per-question **Time Limit (sec)** (default `15`).
6. **Explanation / Fun Fact**: Enter interesting background trivia or context. This text is displayed on venue TVs during the 5-second post-reveal buffer after the answer is revealed.
7. **Commit**: Click **'💾 Apply / Save Question to Pack'** to update the list.

### Filtering & Duplicating Questions:
• Use the **Difficulty Filter** dropdown (`All`, `Easy`, `Medium`, `Hard`) next to the search box to filter the question list by difficulty.
• To quickly create similar questions or series, select a question in the table and click **'📋 Duplicate'** in the Question Editor header."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "4. Balancing Answer Distributions (25% Rule)",
            Icon = "🔀",
            AccentColor = "#F59E0B",
            DescriptionHeader = "Eliminating Answer Bias with the 1-Click Shuffler",
            DescriptionContent = 
@"### Why Answer Balancing is Essential:
When humans write multiple-choice questions, there is a natural cognitive bias where the correct answer is frequently placed in Option A or Option B. If all correct answers are 'A', savvy players will notice and always press 'A' to win.

In professional pub trivia, correct answers should be evenly distributed:
• **▲ Option A**: ~25% (e.g. 37-38 out of 150 questions)
• **◆ Option B**: ~25% (e.g. 37-38 out of 150 questions)
• **● Option C**: ~25% (e.g. 37-38 out of 150 questions)
• **■ Option D**: ~25% (e.g. 37-38 out of 150 questions)

### Using the 1-Click Option Balancer:
1. Look at the **Option Distribution** status bar below the questions table.
2. If any option exceeds 35% or an orange warning appears, click the **'🔀 Balance Options (25% each)'** button.
3. The balancer automatically shuffles the visual order of the 4 choices for all questions across the pack so that correct answers are perfectly balanced at ~25% each, while preserving the exact correct text!
4. Click **'💾 Save Pack'** to write the balanced database to disk."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "5. 16:9 Category Announcement Banners",
            Icon = "🎨",
            AccentColor = "#EC4899",
            DescriptionHeader = "Rendering High-Resolution Widescreen TV Banners",
            DescriptionContent = 
@"### 16:9 Category Banners Studio:
During the **70:30 Pre-Game Lobby** in `LyracistTrivia` and `KSRotation`, the left 70% of venue TVs displays a high-resolution 16:9 Category Announcement Graphic (`Banners/CategoryBanners/{pack}.png`).

### Generating a Banner:
1. Select your pack from the Left Sidebar.
2. In the top metadata card, look at the **Banner Preview** box on the right.
3. Click **'🎨 Generate 16:9 Banner'**.
4. The application renders a 1920x1080 graphic with modern ambient illumination, pack title, and topic subtitle directly into `Banners/CategoryBanners/{slug}.png`.
5. When this pack is selected in `LyracistTrivia`, the big screen TV will immediately project the new graphic."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "6. SQLite Database Sync & Live Hosting",
            Icon = "⚡",
            AccentColor = "#06B6D4",
            DescriptionHeader = "Synchronizing Packs to SQLite trivia.db",
            DescriptionContent = 
@"### How Lyracist Shares Trivia Data:
All applications in the Lyracist suite share centralized folders:
• `Packs/*.json`: Raw human-readable question pack files.
• `Banners/CategoryBanners/*.png`: 16:9 category showcase graphics.
• `Data/trivia.db`: High-performance SQLite database for fast queries and runtime indexing.
• `Settings/lyracist_trivia_settings.json`: Game Master timers and scoring preferences.

### Syncing to SQLite:
• Whenever you save a pack, TriviaDbCreator automatically syncs changes quietly.
• You can also click the top header button **'⚡ Sync to SQLite (trivia.db)'** at any time to re-index all packs into SQLite.
• Any new pack is immediately available in the category dropdowns of `LyracistTrivia`, `Lyracist`, and `KSRotation`!"
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "7. Importing & Exporting Databases",
            Icon = "📂",
            AccentColor = "#A78BFA",
            DescriptionHeader = "Sharing & Backing Up JSON Question Packs",
            DescriptionContent = 
@"### Importing External Packs & CSV Spreadsheets:
1. Click **'📂 Import JSON'** in the top header bar to import any `.json` trivia pack (or raw question array) into `Packs/` and automatically sync it to `Data/trivia.db`.
2. Click **'📥 Import CSV'** in the top header bar to bulk-import questions from a `.csv` or tab-delimited `.txt` spreadsheet (`Prompt, OptionA, OptionB, OptionC, OptionD, CorrectAnswer, Difficulty, Explanation`) directly into the currently selected pack.

### Exporting Databases:
1. Select the pack you wish to export in the Left Sidebar.
2. Click **'💾 Export JSON'** in the top header bar.
3. Choose a destination folder to save a standalone `.json` copy for sharing with other KJs, backing up to cloud storage, or printing."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "8. JSON Database Format & Schema Reference",
            Icon = "📝",
            AccentColor = "#38BDF8",
            DescriptionHeader = "Full Specification of the Trivia JSON Schema",
            DescriptionContent = 
@"### Trivia Question Pack JSON Schema:
```json
{
  ""PackId"": ""disney_classics"",
  ""Title"": ""Disney Classics & Animation"",
  ""Category"": ""Movies & Animation"",
  ""Description"": ""Animated classics, Pixar hits, theme songs, and Disney lore"",
  ""Questions"": [
    {
      ""Id"": ""DIS-001"",
      ""Category"": ""Movies & Animation"",
      ""Difficulty"": ""Easy"",
      ""QuestionType"": ""MultipleChoice"",
      ""Prompt"": ""In The Lion King (1994), what is the name of Simba's father?"",
      ""Options"": [
        ""Scar"",
        ""Mufasa"",
        ""Rafiki"",
        ""Zazu""
      ],
      ""CorrectAnswerIndex"": 1,
      ""Explanation"": ""Mufasa was the noble king of the Pride Lands before his untimely death."",
      ""TimeLimitSeconds"": 15
    }
  ]
}
```

### Key Field Rules:
• `CorrectAnswerIndex`: 0-based integer pointing to the correct choice (0 = A, 1 = B, 2 = C, 3 = D).
• `Options`: Exactly 4 non-empty string elements.
• `TimeLimitSeconds`: Default 15 seconds (can be overridden globally in Game Master settings)."
        });

        HelpTopics.Add(new TriviaHelpTopic
        {
            Title = "9. Fast Editing & Textbox Auto-Selection",
            Icon = "⚡",
            AccentColor = "#F59E0B",
            DescriptionHeader = "Automatic Textbox Highlighting Across the Entire Application",
            DescriptionContent = 
@"### Fast Editing & Auto-Selection:
• **Global Select-All on Focus**: Clicking or tabbing into any text input box (Question Prompt, Choice A/B/C/D, Explanation, Pack Metadata, Search Filters) automatically highlights and selects all existing text.
• **Instant Overwrite**: You can immediately type new prompts or options without having to manually backspace or double-click first.
• **Precision Caret Placement**: Clicking again inside an already-focused text box places the cursor exactly where you click for fine adjustments."
        });

        SelectedHelpTopic = HelpTopics.FirstOrDefault();
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }

    private static string CleanFileName(string name)
    {
        string invalid = new string(Path.GetInvalidFileNameChars());
        string clean = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return clean.Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
    }
}
