using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Lyracist.Core.Interfaces;
using Lyracist.Models;

namespace Lyracist.Services.Integration
{
    public class PartyTymeService : IPartyTymeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private string _token = string.Empty;

        public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
        public string ApiKey { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        private readonly List<PartyTymeTrack> _mockTracks =
        [
            new PartyTymeTrack { TrackId = "pt_hello", Title = "Hello", Artist = "Adele", DurationSeconds = 295, StreamUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3" },
            new PartyTymeTrack { TrackId = "pt_bohemian", Title = "Bohemian Rhapsody", Artist = "Queen", DurationSeconds = 354, StreamUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-2.mp3" },
            new PartyTymeTrack { TrackId = "pt_shapeofyou", Title = "Shape of You", Artist = "Ed Sheeran", DurationSeconds = 233, StreamUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-3.mp3" },
            new PartyTymeTrack { TrackId = "pt_badguy", Title = "Bad Guy", Artist = "Billie Eilish", DurationSeconds = 194, StreamUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3" },
            new PartyTymeTrack { TrackId = "pt_flyme", Title = "Fly Me to the Moon", Artist = "Frank Sinatra", DurationSeconds = 147, StreamUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-5.mp3" },
            new PartyTymeTrack { TrackId = "pt_dontstop", Title = "Don't Stop Believin'", Artist = "Journey", DurationSeconds = 251, StreamUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-6.mp3" }
        ];

        public PartyTymeService()
        {
            _httpClient = new HttpClient();
            _cacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "PartyTyme");
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        public async Task<bool> AuthenticateAsync(string clientId, string clientSecret)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;

            await Task.Delay(500); // Simulate API latency

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _token = string.Empty;
                return false;
            }

            _token = "mock_access_token_" + Guid.NewGuid().ToString("N");
            return true;
        }

        public async Task<IEnumerable<PartyTymeTrack>> SearchCatalogAsync(string query)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated with Party Tyme API.");
            }

            await Task.Delay(400); // Simulate network lag

            if (string.IsNullOrWhiteSpace(query))
            {
                return _mockTracks.Select(t => UpdateCacheStatus(t)).ToList();
            }

            var results = _mockTracks
                .Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(t => UpdateCacheStatus(t))
                .ToList();

            return results;
        }

        public async Task<string> GetStreamUrlAsync(string trackId)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated with Party Tyme API.");
            }

            var track = _mockTracks.FirstOrDefault(t => t.TrackId == trackId);
            if (track == null) return string.Empty;

            var cachedPath = Path.Combine(_cacheDirectory, $"{trackId}.mp3");
            if (File.Exists(cachedPath))
            {
                return cachedPath;
            }

            return track.StreamUrl;
        }

        public async Task<string> DownloadTrackAsync(string trackId, string title, string artist)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated with Party Tyme API.");
            }

            var track = _mockTracks.FirstOrDefault(t => t.TrackId == trackId);
            if (track == null) return string.Empty;

            var cachedPath = Path.Combine(_cacheDirectory, $"{trackId}.mp3");
            if (File.Exists(cachedPath))
            {
                return cachedPath;
            }

            try
            {
                var response = await _httpClient.GetAsync(track.StreamUrl);
                if (response.IsSuccessStatusCode)
                {
                    using var fs = new FileStream(cachedPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                    return cachedPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cache Party Tyme track: {ex.Message}");
            }

            return string.Empty;
        }

        public void ClearCache()
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    foreach (var file in Directory.GetFiles(_cacheDirectory))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear Party Tyme cache: {ex.Message}");
            }
        }

        private PartyTymeTrack UpdateCacheStatus(PartyTymeTrack track)
        {
            var cachedPath = Path.Combine(_cacheDirectory, $"{track.TrackId}.mp3");
            track.IsCached = File.Exists(cachedPath);
            return track;
        }
    }
}
