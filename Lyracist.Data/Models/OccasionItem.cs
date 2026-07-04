namespace Lyracist.Data.Models
{
    public class OccasionItem
    {
        public int OccasionItemId { get; set; }
        public int OccasionCategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        public double Treble { get; set; }
        public double Mid { get; set; }
        public double Bass { get; set; }
        public double Gain { get; set; }
        public int Key { get; set; }
        public double Tempo { get; set; }
        public double Compressor { get; set; }
        public double Limiter { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        // Navigation Properties
        public OccasionCategory? OccasionCategory { get; set; }
    }
}
