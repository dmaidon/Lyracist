using System;
using System.Collections.Generic;
using System.Linq;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Models;

namespace Lyracist.Services.Occasions;

public class OccasionService : IOccasionService
{
    // Nested Holiday submenu entries, inserted at runtime on first use so no
    // EF migration is needed on top of the seeded top-level categories.
    private static readonly string[] DefaultHolidays =
    [
        "Christmas", "Halloween", "New Year's Eve", "Valentine's Day",
        "St. Patrick's Day", "Thanksgiving", "Independence Day"
    ];

    public event EventHandler? OccasionsChanged;

    public OccasionService()
    {
        EnsureHolidayDefaults();
    }

    private void EnsureHolidayDefaults()
    {
        try
        {
            using var context = new LyracistDbContext();
            var holiday = context.OccasionCategories.FirstOrDefault(c => c.Name == "Holiday" && c.ParentCategoryId == null);
            if (holiday == null) return;

            bool hasChildren = context.OccasionCategories.Any(c => c.ParentCategoryId == holiday.OccasionCategoryId);
            if (hasChildren) return;

            foreach (string name in DefaultHolidays)
            {
                context.OccasionCategories.Add(new OccasionCategory { Name = name, ParentCategoryId = holiday.OccasionCategoryId });
            }
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            Core.Helpers.AppLogger.LogError(ex, "OccasionService.EnsureHolidayDefaults");
        }
    }

    public List<OccasionNode> GetMenuTree()
    {
        using var context = new LyracistDbContext();
        var categories = context.OccasionCategories.ToList();
        var items = context.OccasionItems.ToList();

        List<OccasionNode> Build(int? parentId, int depth) =>
            [.. categories.Where(c => c.ParentCategoryId == parentId)
                .OrderBy(c => c.Name)
                .Select(c =>
                {
                    var node = new OccasionNode { Id = c.OccasionCategoryId, Name = c.Name, IsItem = false, Depth = depth };
                    node.Children.AddRange(Build(c.OccasionCategoryId, depth + 1));
                    node.Children.AddRange(items
                        .Where(i => i.OccasionCategoryId == c.OccasionCategoryId)
                        .OrderBy(i => i.Name)
                        .Select(i => MapItem(i, depth + 1)));
                    return node;
                })];

        return Build(null, 0);
    }

    public List<OccasionNode> GetCategoriesFlat()
    {
        var flat = new List<OccasionNode>();
        void Walk(List<OccasionNode> nodes)
        {
            foreach (var node in nodes)
            {
                var children = node.Children.ToList();
                node.Children.Clear();
                flat.Add(node);
                Walk([.. children.Where(c => !c.IsItem)]);
            }
        }
        Walk(GetMenuTree());
        return flat;
    }

    public List<OccasionNode> GetItems(int categoryId)
    {
        using var context = new LyracistDbContext();
        return [.. context.OccasionItems
            .Where(i => i.OccasionCategoryId == categoryId)
            .OrderBy(i => i.Name)
            .ToList()
            .Select(i => MapItem(i, 0))];
    }

    public void AddCategory(string name, int? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name)) return;

        using var context = new LyracistDbContext();
        context.OccasionCategories.Add(new OccasionCategory { Name = name.Trim(), ParentCategoryId = parentCategoryId });
        context.SaveChanges();
        OccasionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveCategory(int categoryId)
    {
        using var context = new LyracistDbContext();
        var category = context.OccasionCategories.Find(categoryId);
        if (category == null) return;

        // Cascade delete handles subcategories and items via the FK config.
        context.OccasionCategories.Remove(category);
        context.SaveChanges();
        OccasionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddItem(int categoryId, string name, string filePath)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(filePath)) return;

        using var context = new LyracistDbContext();
        context.OccasionItems.Add(new OccasionItem
        {
            OccasionCategoryId = categoryId,
            Name = name.Trim(),
            FilePath = filePath,
            Tempo = 1.0
        });
        context.SaveChanges();
        OccasionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveItem(int itemId)
    {
        using var context = new LyracistDbContext();
        var item = context.OccasionItems.Find(itemId);
        if (item == null) return;

        context.OccasionItems.Remove(item);
        context.SaveChanges();
        OccasionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateItemAudio(int itemId, double bass, double treble, double gain)
    {
        using var context = new LyracistDbContext();
        var item = context.OccasionItems.Find(itemId);
        if (item == null) return;

        item.Bass = bass;
        item.Treble = treble;
        item.Gain = gain;
        context.SaveChanges();
        OccasionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static OccasionNode MapItem(OccasionItem item, int depth) => new()
    {
        Id = item.OccasionItemId,
        Name = item.Name,
        IsItem = true,
        FilePath = item.FilePath,
        Bass = item.Bass,
        Treble = item.Treble,
        Gain = item.Gain,
        Depth = depth
    };
}
