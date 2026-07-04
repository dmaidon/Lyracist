namespace Lyracist.Data.Models
{
    public class EndRotationPlaylistItem
    {
        public int EndRotationPlaylistItemId { get; set; }
        public int SongId { get; set; }
        public int Order { get; set; }

        // Navigation Properties
        public Song? Song { get; set; }
    }
}
