namespace Lyracist.Models
{
    public class PartyTymeTrack
    {
        public string TrackId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public int DurationSeconds { get; set; }
        public string PreviewUrl { get; set; } = string.Empty;
        public string StreamUrl { get; set; } = string.Empty;
        public bool IsCached { get; set; }
    }
}
