using System;
using System.Collections.Generic;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces;

public interface IOccasionService
{
    /// <summary>Raised after any category or item is added, removed, or edited.</summary>
    event EventHandler? OccasionsChanged;

    /// <summary>Full category tree (with nested subcategories and playable items).</summary>
    List<OccasionNode> GetMenuTree();

    /// <summary>All categories flattened depth-first, for editor list binding.</summary>
    List<OccasionNode> GetCategoriesFlat();

    /// <summary>Playable items belonging directly to one category.</summary>
    List<OccasionNode> GetItems(int categoryId);

    void AddCategory(string name, int? parentCategoryId);
    void RemoveCategory(int categoryId);

    void AddItem(int categoryId, string name, string filePath);
    void RemoveItem(int itemId);
    void UpdateItemAudio(int itemId, double bass, double treble, double gain);
}
