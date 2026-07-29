// Edited on Jul 28, 2026 @ 18:37:00 -> Add RequestType property to distinguish Karaoke vs Music requests
// Last Edit: Jul 16, 2026 11:00 - Patron model request data
using System;
using System.Collections.Generic;
using System.Linq;

namespace KSRotation.Models
{
    public class RequestedSong
    {
        public string Song { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;

        public RequestedSong() { }
        public RequestedSong(string song, string artist)
        {
            Song = song;
            Artist = artist;
        }
    }

    public class PatronRequest
    {
        private string _song = string.Empty;
        private string _artist = string.Empty;
        private List<RequestedSong> _songs = [];

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;

        public string Song
        {
            get => _song;
            set
            {
                _song = value;
                UpdateSongsListIfSingle();
            }
        }

        public string Artist
        {
            get => _artist;
            set
            {
                _artist = value;
                UpdateSongsListIfSingle();
            }
        }

        public List<RequestedSong> Songs
        {
            get => _songs;
            set
            {
                _songs = value ?? [];
                if (_songs.Count > 0)
                {
                    _song = _songs[0].Song;
                    _artist = _songs[0].Artist;
                }
            }
        }

        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string RequestType { get; set; } = "Karaoke";

        private void UpdateSongsListIfSingle()
        {
            if (_songs == null || _songs.Count <= 1)
            {
                _songs = [new RequestedSong(_song, _artist)];
            }
        }
    }
}
