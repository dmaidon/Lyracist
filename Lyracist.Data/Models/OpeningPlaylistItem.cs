namespace Lyracist.Data.Models
{
    public class OpeningPlaylistItem
    {
        public int OpeningPlaylistItemId { get; set; }
        public int SongId { get; set; }
        public int Order { get; set; }

        // Navigation Properties
        public Song? Song { get; set; }
    }
}
