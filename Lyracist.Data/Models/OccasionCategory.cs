using System.Collections.Generic;

namespace Lyracist.Data.Models
{
    public class OccasionCategory
    {
        public int OccasionCategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentCategoryId { get; set; }

        // Navigation Properties
        public OccasionCategory? ParentCategory { get; set; }
        public ICollection<OccasionCategory> SubCategories { get; set; } = [];
        public ICollection<OccasionItem> Items { get; set; } = [];
    }
}
