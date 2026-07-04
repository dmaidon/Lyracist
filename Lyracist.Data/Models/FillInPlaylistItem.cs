namespace Lyracist.Data.Models
{
    public class FillInPlaylistItem
    {
        public int FillInPlaylistItemId { get; set; }
        public int SongId { get; set; }
        public int Order { get; set; }

        // Navigation Properties
        public Song? Song { get; set; }
    }
}
