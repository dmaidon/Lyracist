// Edited on Aug 30, 2026 @ 08:26:00 -> Update settings path to consolidated Settings/lyracist_settings.json
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Shared;

namespace Lyracist.Data.Services
{
    public class MetadataResult
    {
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }

    public static class MetadataFetchService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly SemaphoreSlim _musicBrainzLock = new SemaphoreSlim(1, 1);
        private static DateTime _lastMusicBrainzRequestUtc = DateTime.MinValue;
        private static string _spotifyToken = string.Empty;
        private static DateTime _spotifyTokenExpiry = DateTime.MinValue;

        static MetadataFetchService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Lyracist/1.0.0 (dmaidon@example.com)");
        }

        private static (string ClientId, string ClientSecret) LoadSpotifyCredentials()
        {
            try
            {
                string settingsPath = Path.Combine(Globals.SettingsDir, "lyracist_settings.json");
                if (!File.Exists(settingsPath))
                {
                    settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyracist", "settings.json");
                }

                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    string encryptedClientId = root.TryGetProperty("SpotifyClientId", out var idProp) ? (idProp.GetString() ?? "") : "";
                    string encryptedClientSecret = root.TryGetProperty("SpotifyClientSecret", out var secProp) ? (secProp.GetString() ?? "") : "";

                    string clientId = EncryptionHelper.Decrypt(encryptedClientId).Trim();
                    string clientSecret = EncryptionHelper.Decrypt(encryptedClientSecret).Trim();
                    return (clientId, clientSecret);
                }
            }
            catch
            {
                // Ignore
            }
            return ("", "");
        }

        private static async Task ThrottleMusicBrainzAsync()
        {
            await _musicBrainzLock.WaitAsync();
            try
            {
                var elapsed = DateTime.UtcNow - _lastMusicBrainzRequestUtc;
                if (elapsed < TimeSpan.FromMilliseconds(1100))
                {
                    int delay = 1100 - (int)elapsed.TotalMilliseconds;
                    await Task.Delay(delay);
                }
                _lastMusicBrainzRequestUtc = DateTime.UtcNow;
            }
            finally
            {
                _musicBrainzLock.Release();
            }
        }

        public static async Task<MetadataResult?> FetchMetadataAsync(string? title, string? artist, CancellationToken token = default)
        {
            title ??= string.Empty;
            artist ??= string.Empty;
            var result = new MetadataResult();
            bool hasFetchedAny = false;

            // 1. Try Spotify if credentials are configured
            var (spotifyId, spotifySecret) = LoadSpotifyCredentials();
            if (!string.IsNullOrEmpty(spotifyId) && !string.IsNullOrEmpty(spotifySecret))
            {
                try
                {
                    var spotifyResult = await FetchFromSpotifyAsync(title, artist, spotifyId, spotifySecret, token);
                    if (spotifyResult != null)
                    {
                        result.Artist = spotifyResult.Artist;
                        result.Title = spotifyResult.Title;
                        result.Tags.AddRange(spotifyResult.Tags);
                        hasFetchedAny = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Spotify metadata fetch failed: {ex.Message}");
                }
            }

            // 2. Try MusicBrainz
            try
            {
                var mbResult = await FetchFromMusicBrainzAsync(title, artist, token);
                if (mbResult != null)
                {
                    if (string.IsNullOrEmpty(result.Artist)) result.Artist = mbResult.Artist;
                    if (string.IsNullOrEmpty(result.Title)) result.Title = mbResult.Title;
                    result.Tags.AddRange(mbResult.Tags);
                    hasFetchedAny = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MusicBrainz metadata fetch failed: {ex.Message}");
            }

            if (!hasFetchedAny) return null;

            // De-duplicate and clean tags
            result.Tags = result.Tags
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();

            return result;
        }

        private static async Task<MetadataResult?> FetchFromMusicBrainzAsync(string title, string artist, CancellationToken token)
        {
            await ThrottleMusicBrainzAsync();

            string query = $"recording:\"{title}\"";
            if (!string.IsNullOrEmpty(artist) && artist != "Unknown Artist")
            {
                query += $" AND artist:\"{artist}\"";
            }

            string url = $"https://musicbrainz.org/ws/2/recording/?query={Uri.EscapeDataString(query)}&fmt=json";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("recordings", out var recordingsProp) && recordingsProp.ValueKind == JsonValueKind.Array && recordingsProp.GetArrayLength() > 0)
            {
                // Select the highest score/first result
                var recording = recordingsProp[0];

                var result = new MetadataResult();

                // Title
                if (recording.TryGetProperty("title", out var titleProp))
                {
                    result.Title = titleProp.GetString() ?? "";
                }

                // Artist
                if (recording.TryGetProperty("artist-credit", out var creditProp) && creditProp.ValueKind == JsonValueKind.Array && creditProp.GetArrayLength() > 0)
                {
                    var firstCredit = creditProp[0];
                    if (firstCredit.TryGetProperty("name", out var nameProp))
                    {
                        result.Artist = nameProp.GetString() ?? "";
                    }
                }

                // Tags
                if (recording.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsProp.EnumerateArray())
                    {
                        if (tag.TryGetProperty("name", out var tagNameProp))
                        {
                            string t = tagNameProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(t)) result.Tags.Add(t);
                        }
                    }
                }

                return result;
            }

            return null;
        }

        private static async Task<MetadataResult?> FetchFromSpotifyAsync(string title, string artist, string clientId, string clientSecret, CancellationToken token)
        {
            string tokenVal = await GetSpotifyTokenAsync(clientId, clientSecret, token);
            if (string.IsNullOrEmpty(tokenVal)) return null;

            string query = $"track:{title}";
            if (!string.IsNullOrEmpty(artist) && artist != "Unknown Artist")
            {
                query += $" artist:{artist}";
            }

            string url = $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(query)}&type=track&limit=1";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenVal);

            var response = await _httpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("tracks", out var tracksProp) &&
                tracksProp.TryGetProperty("items", out var itemsProp) &&
                itemsProp.ValueKind == JsonValueKind.Array &&
                itemsProp.GetArrayLength() > 0)
            {
                var track = itemsProp[0];
                var result = new MetadataResult();

                if (track.TryGetProperty("name", out var nameProp))
                {
                    result.Title = nameProp.GetString() ?? "";
                }

                string artistId = string.Empty;
                if (track.TryGetProperty("artists", out var artistsProp) && artistsProp.ValueKind == JsonValueKind.Array && artistsProp.GetArrayLength() > 0)
                {
                    var mainArtist = artistsProp[0];
                    if (mainArtist.TryGetProperty("name", out var artistNameProp))
                    {
                        result.Artist = artistNameProp.GetString() ?? "";
                    }
                    if (mainArtist.TryGetProperty("id", out var artistIdProp))
                    {
                        artistId = artistIdProp.GetString() ?? "";
                    }
                }

                // Spotify tracks do not have genres, but artists do! Fetch the artist profile
                if (!string.IsNullOrEmpty(artistId))
                {
                    try
                    {
                        var genres = await FetchArtistGenresFromSpotifyAsync(artistId, tokenVal, token);
                        if (genres != null)
                        {
                            result.Tags.AddRange(genres);
                        }
                    }
                    catch
                    {
                        // Non-blocking
                    }
                }

                return result;
            }

            return null;
        }

        private static async Task<List<string>?> FetchArtistGenresFromSpotifyAsync(string artistId, string tokenVal, CancellationToken token)
        {
            string url = $"https://api.spotify.com/v1/artists/{artistId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenVal);

            var response = await _httpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var genre in genresProp.EnumerateArray())
                {
                    string g = genre.GetString() ?? "";
                    if (!string.IsNullOrEmpty(g)) list.Add(g);
                }
                return list;
            }

            return null;
        }

        private static async Task<string> GetSpotifyTokenAsync(string clientId, string clientSecret, CancellationToken token)
        {
            if (!string.IsNullOrEmpty(_spotifyToken) && DateTime.UtcNow < _spotifyTokenExpiry)
            {
                return _spotifyToken;
            }

            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return string.Empty;

            string json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var tokenProp))
            {
                _spotifyToken = tokenProp.GetString() ?? "";
                int expiresSec = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;
                _spotifyTokenExpiry = DateTime.UtcNow.AddSeconds(expiresSec - 60);
                return _spotifyToken;
            }

            return string.Empty;
        }
    }
}
