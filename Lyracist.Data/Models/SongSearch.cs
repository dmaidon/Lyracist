namespace Lyracist.Data.Models
{
    public class SongSearch
    {
        public int SongId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string NormalizedTitle { get; set; } = string.Empty;
        public string NormalizedArtist { get; set; } = string.Empty;
    }
}
