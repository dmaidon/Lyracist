namespace Lyracist.Models
{
    public class ExternalTrack
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = "YouTube"; // YouTube, Spotify, Amazon
    }
}
