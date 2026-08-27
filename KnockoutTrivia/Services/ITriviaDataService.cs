// Edited on Aug 27, 2026 @ 15:07:15 -> Added multi-source database and pack loading methods
using System.Collections.Generic;
using System.Threading.Tasks;
using KnockoutTrivia.Models;

namespace KnockoutTrivia.Services;

public interface ITriviaDataService
{
    Task<List<KnockoutQuestion>> LoadQuestionsFromDatabaseAsync(string? dbPath = null);
    Task<List<KnockoutQuestion>> LoadQuestionsFromPackAsync(string packFilePath);
    Task<List<KnockoutQuestion>> LoadQuestionsFromMultipleSourcesAsync(IEnumerable<string> sourcePaths);
    Task<List<SelectableTriviaSource>> GetAvailableSourcesAsync();
    List<string> GetAvailablePacks();
    List<string> GetAvailableDatabases();
    Task<List<string>> GetCategoriesAsync(string? dbPath = null);
}
