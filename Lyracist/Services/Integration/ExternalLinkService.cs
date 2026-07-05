using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Models;

namespace Lyracist.Services.Integration
{
    public class ExternalLinkService
    {
        private readonly List<ExternalTrack> _mockDatabase = new()
        {
            // Spotify
            new ExternalTrack { Title = "Yellow", Artist = "Coldplay", Url = "https://open.spotify.com/track/3ee8J1Fw65u2hSuVv41u6J", Source = "Spotify" },
            new ExternalTrack { Title = "Blinding Lights", Artist = "The Weeknd", Url = "https://open.spotify.com/track/0VjIjW4GlUZg4qZJZ34A30", Source = "Spotify" },
            new ExternalTrack { Title = "Stay", Artist = "Kid LAROI & Justin Bieber", Url = "https://open.spotify.com/track/5HCyWj6zoCBStrokeGqW0T", Source = "Spotify" },

            // YouTube
            new ExternalTrack { Title = "My Way (Karaoke Version)", Artist = "Frank Sinatra", Url = "https://www.youtube.com/watch?v=qQzdAsjWGPg", Source = "YouTube" },
            new ExternalTrack { Title = "Bohemian Rhapsody (Backing Track)", Artist = "Queen", Url = "https://www.youtube.com/watch?v=fJ9rUzIMcZQ", Source = "YouTube" },
            new ExternalTrack { Title = "Sweet Caroline (Sing-Along)", Artist = "Neil Diamond", Url = "https://www.youtube.com/watch?v=1vhFnTjia_I", Source = "YouTube" },

            // Amazon Music
            new ExternalTrack { Title = "Stairway to Heaven", Artist = "Led Zeppelin", Url = "https://music.amazon.com/tracks/B00F3T4EBA", Source = "Amazon" },
            new ExternalTrack { Title = "Hotel California", Artist = "Eagles", Url = "https://music.amazon.com/tracks/B002Q1AGF4", Source = "Amazon" },
            new ExternalTrack { Title = "Billie Jean", Artist = "Michael Jackson", Url = "https://music.amazon.com/tracks/B00138GY1A", Source = "Amazon" }
        };

        public async Task<IEnumerable<ExternalTrack>> SearchAsync(string query, string service = "All")
        {
            // If YouTube is queried and a YouTube API Key is supplied, perform a real YouTube search
            if ((service.Equals("YouTube", StringComparison.OrdinalIgnoreCase) || service.Equals("All", StringComparison.OrdinalIgnoreCase)) 
                && !string.IsNullOrWhiteSpace(AppSettings.YouTubeApiKey) && !string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient();
                    string url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&q={Uri.EscapeDataString(query + " karaoke")}&type=video&maxResults=10&key={AppSettings.YouTubeApiKey}";
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var list = new List<ExternalTrack>();
                        if (doc.RootElement.TryGetProperty("items", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("id", out var idObj) && idObj.TryGetProperty("videoId", out var vIdProp))
                                {
                                    string videoId = vIdProp.GetString() ?? "";
                                    string fullTitle = item.GetProperty("snippet").GetProperty("title").GetString() ?? "";
                                    string channel = item.GetProperty("snippet").GetProperty("channelTitle").GetString() ?? "";
                                    
                                    // Basic parsing to split "Artist - Title"
                                    string artist = channel;
                                    string title = fullTitle;
                                    int dashIdx = fullTitle.IndexOf(" - ");
                                    if (dashIdx > 0)
                                    {
                                        artist = fullTitle.Substring(0, dashIdx).Trim();
                                        title = fullTitle.Substring(dashIdx + 3).Trim();
                                    }
                                    
                                    title = System.Net.WebUtility.HtmlDecode(title);
                                    artist = System.Net.WebUtility.HtmlDecode(artist);

                                    list.Add(new ExternalTrack
                                    {
                                        Title = title,
                                        Artist = artist,
                                        Url = $"https://www.youtube.com/watch?v={videoId}",
                                        Source = "YouTube"
                                    });
                                }
                            }
                        }
                        
                        // If we only wanted YouTube results, we can return these directly
                        if (service.Equals("YouTube", StringComparison.OrdinalIgnoreCase))
                        {
                            return list;
                        }
                        
                        // Otherwise, if service == "All", we prepend them to the rest of the mock results
                        var finalResults = new List<ExternalTrack>(list);
                        var mockRest = _mockDatabase.Where(t => !t.Source.Equals("YouTube", StringComparison.OrdinalIgnoreCase)
                                                               && (t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) 
                                                                   || t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)));
                        finalResults.AddRange(mockRest);
                        return finalResults;
                    }
                }
                catch 
                {
                    // Fallback to mock search on network error
                }
            }

            await Task.Delay(300); // Simulate network query latency

            var results = _mockDatabase.AsEnumerable();

            if (service != "All")
            {
                results = results.Where(t => t.Source.Equals(service, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                results = results.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                                             t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return results.ToList();
        }

        public ExternalTrack? ParseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            string source = "YouTube";
            string title = "External Track";
            string artist = "Various Artists";

            if (url.Contains("spotify.com", StringComparison.OrdinalIgnoreCase))
            {
                source = "Spotify";
                title = "Spotify Requested Song";
                
                var match = Regex.Match(url, @"/track/([^/?]+)");
                if (match.Success)
                {
                    title = $"Spotify Track ({match.Groups[1].Value.Substring(0, Math.Min(6, match.Groups[1].Value.Length))})";
                }
            }
            else if (url.Contains("amazon.com", StringComparison.OrdinalIgnoreCase) || url.Contains("media-amazon.com", StringComparison.OrdinalIgnoreCase))
            {
                source = "Amazon";
                title = "Amazon Music Requested Song";
            }
            else if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                source = "YouTube";
                title = "YouTube Video Request";

                var match = Regex.Match(url, @"(?:v=|\/embed\/|\/1.1\/|youtu\.be\/)([^?&\s]+)");
                if (match.Success)
                {
                    title = $"YouTube Video ({match.Groups[1].Value})";
                }
            }

            return new ExternalTrack
            {
                Title = title,
                Artist = artist,
                Url = url,
                Source = source
            };
        }
    }
}
