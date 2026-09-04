// Edited on Sep 3, 2026 @ 23:55:00 -> Sync hasSungInLastRound property from KSRotation API
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.ViewModels;
using Lyracist.Models;

namespace Lyracist.Services.Integration
{
    public class KSRotationSyncService : IKSRotationSyncService
    {
        private readonly RotationViewModel _rotationViewModel;
        private readonly KaraokeViewModel _karaokeViewModel;
        private readonly HttpClient _httpClient;
        private CancellationTokenSource? _cts;
        private Task? _syncTask;
        private bool _lastConnectSuccess = true;
        private readonly Lock _lock = new();

        public KSRotationSyncService(RotationViewModel rotationViewModel, KaraokeViewModel karaokeViewModel)
        {
            _rotationViewModel = rotationViewModel;
            _karaokeViewModel = karaokeViewModel;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_syncTask != null) return;

                _cts = new CancellationTokenSource();
                _syncTask = RunSyncLoopAsync(_cts.Token);
                AppLogger.LogInfo("KSRotationSyncService started.");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_syncTask == null) return;

                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _syncTask = null;
                AppLogger.LogInfo("KSRotationSyncService stopped.");
            }
        }

        public Task TriggerSettingsReloadAsync()
        {
            if (AppSettings.KSRotationSyncEnabled)
            {
                Start();
            }
            else
            {
                Stop();
            }
            return Task.CompletedTask;
        }

        private async Task RunSyncLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (AppSettings.KSRotationSyncEnabled)
                    {
                        await PerformSyncAsync(token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "KSRotationSyncService loop error");
                }

                try
                {
                    int delaySeconds = AppSettings.KSRotationSyncIntervalSeconds > 0 ? AppSettings.KSRotationSyncIntervalSeconds : 2;
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task PerformSyncAsync(CancellationToken token)
        {
            string url = $"http://{AppSettings.KSRotationIpAddress}:{AppSettings.KSRotationPort}/api/rotation";
            try
            {
                var response = await _httpClient.GetAsync(url, token);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(token);
                    var syncedItems = JsonSerializer.Deserialize<List<RotationItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (syncedItems != null)
                    {
                        if (!_lastConnectSuccess)
                        {
                            AppLogger.LogInfo($"KSRotation Sync restored. Connected to {url}");
                            _lastConnectSuccess = true;
                        }

                        await UpdateRotationDataAsync(syncedItems);
                    }
                    _lastConnectSuccess = true;
                }
                else
                {
                    if (_lastConnectSuccess)
                    {
                        AppLogger.LogError($"KSRotation Sync failed: HTTP Status {response.StatusCode} for {url}", "KSRotationSyncService");
                        _lastConnectSuccess = false;
                    }
                }

                // Query active special event banner
                string eventsUrl = $"http://{AppSettings.KSRotationIpAddress}:{AppSettings.KSRotationPort}/api/special-event/active";
                var eventsResponse = await _httpClient.GetAsync(eventsUrl, token);
                if (eventsResponse.IsSuccessStatusCode)
                {
                    string eventsJson = await eventsResponse.Content.ReadAsStringAsync(token);
                    using var doc = JsonDocument.Parse(eventsJson);
                    if (doc.RootElement.TryGetProperty("activeSpecialEvent", out var activeProp))
                    {
                        string activeEvent = activeProp.GetString() ?? "None";
                        await UpdateSpecialEventDataAsync(activeEvent);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (_lastConnectSuccess)
                {
                    AppLogger.LogError($"KSRotation Sync connection error to {url}: {ex.Message}", "KSRotationSyncService");
                    _lastConnectSuccess = false;
                }
            }
        }

        private Task UpdateSpecialEventDataAsync(string activeSpecialEvent)
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _karaokeViewModel.UpdateActiveSpecialEventFromSync(activeSpecialEvent);
            }).Task;
        }

        private Task UpdateRotationDataAsync(List<RotationItemDto> syncedItems)
        {
            return System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (AreListsEqual(syncedItems, _rotationViewModel.Rotation))
                {
                    return;
                }

                var currentRotation = _rotationViewModel.Rotation.ToList();
                var newRotation = new List<Singer>();

                foreach (var item in syncedItems)
                {
                    var singer = currentRotation.FirstOrDefault(s => s.Name.Equals(item.name, StringComparison.OrdinalIgnoreCase) && s.IsMusic == item.isMusic);
                    if (singer == null)
                    {
                        singer = new Singer { Name = item.name, IsMusic = item.isMusic };
                        LoadSingerDbMetadata(singer);
                    }
                    else
                    {
                        currentRotation.Remove(singer);
                    }

                    singer.SongTitle = item.song ?? string.Empty;
                    singer.Artist = item.artist ?? string.Empty;
                    singer.IsCurrent = item.isCurrent;
                    singer.IsNext = item.isNext;
                    singer.IsInactive = item.isInactive;
                    singer.IsMusic = item.isMusic;
                    singer.IsRotationStart = item.isRotationStart;
                    singer.HasSungInLastRound = item.hasSungInLastRound;

                    newRotation.Add(singer);
                }

                _rotationViewModel.Rotation.Clear();
                foreach (var s in newRotation)
                {
                    _rotationViewModel.Rotation.Add(s);
                }

                _rotationViewModel.NotifyRotationReordered();
            }).Task;
        }

        private void LoadSingerDbMetadata(Singer singer)
        {
            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                var dbSinger = context.Singers.FirstOrDefault(s => s.Name == singer.Name);
                if (dbSinger != null)
                {
                    singer.Score = dbSinger.Score;
                    singer.AverageRating = dbSinger.AverageRating;
                    singer.RatingCount = dbSinger.RatingCount;
                    singer.TotalSongsSung = dbSinger.TotalSongsSung;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, $"KSRotationSyncService.LoadSingerDbMetadata failed for singer '{singer.Name}'");
            }
        }

        private bool AreListsEqual(List<RotationItemDto> synced, ObservableCollection<Singer> current)
        {
            if (synced.Count != current.Count) return false;
            for (int i = 0; i < synced.Count; i++)
            {
                var s = synced[i];
                var c = current[i];
                if (!string.Equals(s.name, c.Name, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(s.song, c.SongTitle, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(s.artist, c.Artist, StringComparison.OrdinalIgnoreCase) ||
                    s.isCurrent != c.IsCurrent ||
                    s.isNext != c.IsNext ||
                    s.isInactive != c.IsInactive ||
                    s.isMusic != c.IsMusic ||
                    s.isRotationStart != c.IsRotationStart ||
                    s.hasSungInLastRound != c.HasSungInLastRound)
                {
                    return false;
                }
            }
            return true;
        }

        private class RotationItemDto
        {
            public string name { get; set; } = string.Empty;
            public string song { get; set; } = string.Empty;
            public string artist { get; set; } = string.Empty;
            public bool isCurrent { get; set; }
            public bool isNext { get; set; }
            public bool isInactive { get; set; }
            public bool isMusic { get; set; }
            public bool isRotationStart { get; set; }
            public bool hasSungInLastRound { get; set; }
        }
    }
}
