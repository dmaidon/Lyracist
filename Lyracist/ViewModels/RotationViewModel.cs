using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Models;
using Lyracist.Services.Display;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.ViewModels;

public partial class PendingSong : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _key = "0";

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _source = "Local";

    [ObservableProperty]
    private string _externalLink = string.Empty;
}

public partial class RotationViewModel : BaseViewModel
{
    private readonly IDisplayService _display;
    private readonly IMediaEngine _mediaEngine;

    public event Action? RotationStateChanged;

    public ObservableCollection<Singer> Rotation { get; } = [];

    public ObservableCollection<Singer> InactiveSingers { get; } = [];

    public ObservableCollection<string> SingerNames { get; } = [];

    private readonly Dictionary<string, ObservableCollection<PendingSong>> _pendingSingerSongs = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<PendingSong> SelectedSingerQueue { get; } = [];

    [ObservableProperty]
    private Singer? _selectedSinger;

    partial void OnSelectedSingerChanged(Singer? oldValue, Singer? newValue)
    {
        RefreshSelectedSingerQueue();
    }

    public void RefreshSelectedSingerQueue()
    {
        SelectedSingerQueue.Clear();
        if (SelectedSinger != null && _pendingSingerSongs.TryGetValue(SelectedSinger.Name, out var queue))
        {
            foreach (var song in queue)
            {
                SelectedSingerQueue.Add(song);
            }
        }
    }

    [ObservableProperty]
    private string _newSingerName = string.Empty;

    [ObservableProperty]
    private string _newSingerNotes = string.Empty;

    [ObservableProperty]
    private string _newSingerKey = "0";

    [ObservableProperty]
    private string _newDuetPartnerName = string.Empty;

    public RotationViewModel(IDisplayService display, IMediaEngine mediaEngine)
    {
        _display = display;
        _mediaEngine = mediaEngine;

        if (AppSettings.IsTestMode)
        {
            SeedSingers();
        }
        LoadSingerNames();
    }

    public void SeedSingers()
    {
        Rotation.Clear();
        Rotation.Add(new Singer { Name = "Alice Johnson", Key = "+1", Notes = "Sings soprano, prefers classic pop", SongTitle = "Sweet Caroline", Artist = "Neil Diamond", Score = 240, AverageRating = 4.8, RatingCount = 5 });
        Rotation.Add(new Singer { Name = "Bob Caruthers", Key = "-2", Notes = "Prefers baritone classic rock", SongTitle = "Hotel California", Artist = "Eagles", Score = 180, AverageRating = 4.5, RatingCount = 4 });
        Rotation.Add(new Singer { Name = "Charlie Brown", Key = "0", Notes = "First time singing today", SongTitle = "Billie Jean", Artist = "Michael Jackson", Score = 120, AverageRating = 4.0, RatingCount = 3 });
        Rotation.Add(new Singer { Name = "Diana Smith", Key = "+2", Notes = "Sings alto, loves jazz standards", SongTitle = "Fly Me to the Moon", Artist = "Frank Sinatra", Score = 90, AverageRating = 4.5, RatingCount = 2 });
        Rotation.Add(new Singer { Name = "Emma Watson", Key = "0", Notes = "Loves pop ballads", SongTitle = "Rolling in the Deep", Artist = "Adele", Score = 50, AverageRating = 5.0, RatingCount = 1 });
        Rotation.Add(new Singer { Name = "Frank Miller", Key = "-1", Notes = "Prefers classic soul", SongTitle = "My Girl", Artist = "Temptations", Score = 40, AverageRating = 4.0, RatingCount = 1 });
        Rotation.Add(new Singer { Name = "Grace Hopper", Key = "+3", Notes = "Energetic performance style", SongTitle = "Respect", Artist = "Aretha Franklin" });
        Rotation.Add(new Singer { Name = "Harry Potter", Key = "0", Notes = "Group favorite song choice", SongTitle = "Bohemian Rhapsody", Artist = "Queen" });
        Rotation.Add(new Singer { Name = "Irene Adler", Key = "+2", Notes = "Sings soprano powerhouse tracks", SongTitle = "I Will Always Love You", Artist = "Whitney Houston" });
        Rotation.Add(new Singer { Name = "Jack Sparrow", Key = "-3", Notes = "Likes pub singalongs", SongTitle = "Piano Man", Artist = "Billy Joel" });
        Rotation.Add(new Singer { Name = "Karen Walker", Key = "+1", Notes = "Likes modern acoustic pop", SongTitle = "Someone Like You", Artist = "Adele" });
        Rotation.Add(new Singer { Name = "Leo Tolstoy", Key = "0", Notes = "Classic rock fan", SongTitle = "Hey Jude", Artist = "Beatles" });
        Rotation.Add(new Singer { Name = "Mary Shelley", Key = "-1", Notes = "Loves spooky themed pop", SongTitle = "Thriller", Artist = "Michael Jackson" });
        Rotation.Add(new Singer { Name = "Ned Stark", Key = "+2", Notes = "Epic rock singalong", SongTitle = "Don't Stop Believin'", Artist = "Journey" });
        Rotation.Add(new Singer { Name = "Oliver Twist", Key = "0", Notes = "Prefers 80s synth rock", SongTitle = "Purple Rain", Artist = "Prince" });

        // Save seed singers to DB for leaderboard
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                foreach (var r in Rotation)
                {
                    var dbSinger = context.Singers.FirstOrDefault(s => s.Name == r.Name);
                    if (dbSinger == null)
                    {
                        dbSinger = new Lyracist.Data.Models.Singer
                        {
                            Name = r.Name,
                            JoinDate = System.DateTime.UtcNow,
                            Score = r.Score,
                            AverageRating = r.AverageRating,
                            RatingCount = r.RatingCount,
                            RatingPoints = (int)(r.AverageRating * r.RatingCount)
                        };
                        context.Singers.Add(dbSinger);
                    }
                    else
                    {
                        dbSinger.Score = r.Score;
                        dbSinger.AverageRating = r.AverageRating;
                        dbSinger.RatingCount = r.RatingCount;
                        dbSinger.RatingPoints = (int)(r.AverageRating * r.RatingCount);
                        context.Singers.Update(dbSinger);
                    }
                }
                context.SaveChanges();
            }
            catch { }
        });

        // Deferred: seeding runs from the RotationViewModel constructor when test
        // mode is on, and UpdateRotation transitively resolves ShowFlowService,
        // which depends on RotationViewModel itself. Calling it synchronously here
        // would reenter this constructor before the DI container has cached the
        // singleton, causing unbounded recursive construction.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _display.UpdateRotation([.. Rotation]);
        });
    }

    public void ClearRotationQueue()
    {
        Rotation.Clear();
        _display.UpdateRotation([.. Rotation]);
    }

    public void LoadSingerNames()
    {
        try
        {
            using var context = new Lyracist.Data.LyracistDbContext();
            var list = context.Singers.Select(s => s.Name).Distinct().ToList();
            SingerNames.Clear();
            foreach (var name in list)
            {
                SingerNames.Add(name);
            }
        }
        catch { }
    }

    partial void OnNewSingerNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            NewSingerNotes = string.Empty;
            return;
        }

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                var history = context.RotationEntries
                    .Include(r => r.Singer)
                    .Include(r => r.Song)
                    .Where(r => r.Singer!.Name == value && r.Status == "Finished")
                    .OrderByDescending(r => r.TimestampAdded)
                    .Select(r => r.Song!.Title + " - " + r.Song.Artist)
                    .Distinct()
                    .Take(5)
                    .ToList();

                if (history.Count > 0)
                {
                    string historyText = "Previously: " + string.Join(", ", history);
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        NewSingerNotes = historyText;
                    });
                }
            }
            catch { }
        });
    }

    public void AddSinger(string name, string title, string artist, string key, string notes, string source = "Local", string externalLink = "", string duetPartner = "")
    {
        name = NameFormatting.ProperCase(name);
        duetPartner = NameFormatting.ProperCase(duetPartner);

        // Save to singer song history database
        System.Threading.Tasks.Task.Run(() =>
        {
            Lyracist.Services.Database.SingerHistoryService.SaveHistory(name, title, artist, source, externalLink);
        });

        int score = 0;
        double avgRating = 0.0;
        int ratingCount = 0;
        int totalSongsSung = 0;
        try
        {
            using var context = new Lyracist.Data.LyracistDbContext();
            var dbSinger = context.Singers.FirstOrDefault(s => s.Name == name);
            if (dbSinger != null)
            {
                score = dbSinger.Score;
                avgRating = dbSinger.AverageRating;
                ratingCount = dbSinger.RatingCount;
                totalSongsSung = dbSinger.TotalSongsSung;
            }
        }
        catch { }

        // First, check if singer already exists in active rotation:
        var existingSinger = Rotation.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existingSinger != null)
        {
            if (string.IsNullOrEmpty(existingSinger.SongTitle))
            {
                existingSinger.SongTitle = title;
                existingSinger.Artist = artist;
                existingSinger.Key = key;
                existingSinger.Notes = notes;
                existingSinger.Source = source;
                existingSinger.ExternalLink = externalLink;
                existingSinger.DuetPartnerName = duetPartner;
                existingSinger.Score = score;
                existingSinger.AverageRating = avgRating;
                existingSinger.RatingCount = ratingCount;
                existingSinger.TotalSongsSung = totalSongsSung;
            }
            else
            {
                // Buffer/queue the song request for this singer
                if (!_pendingSingerSongs.TryGetValue(name, out var list))
                {
                    list = [];
                    _pendingSingerSongs[name] = list;
                }
                list.Add(new PendingSong
                {
                    Title = title,
                    Artist = artist,
                    Key = key,
                    Notes = notes,
                    Source = source,
                    ExternalLink = externalLink
                });

                if (SelectedSinger != null && SelectedSinger.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    RefreshSelectedSingerQueue();
                }
            }
            _display.UpdateRotation([.. Rotation]);
            RotationStateChanged?.Invoke();
            return;
        }

        // Second, check if singer is in the inactive list and reinstate them:
        var inactiveSinger = InactiveSingers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (inactiveSinger != null)
        {
            inactiveSinger.IsInactive = false;
            inactiveSinger.SongTitle = title;
            inactiveSinger.Artist = artist;
            inactiveSinger.Key = key;
            inactiveSinger.Notes = notes;
            inactiveSinger.Source = source;
            inactiveSinger.ExternalLink = externalLink;
            inactiveSinger.DuetPartnerName = duetPartner;
            inactiveSinger.Score = score;
            inactiveSinger.AverageRating = avgRating;
            inactiveSinger.RatingCount = ratingCount;
            inactiveSinger.TotalSongsSung = totalSongsSung;
            
            InactiveSingers.Remove(inactiveSinger);
            Rotation.Add(inactiveSinger);

            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
            return;
        }

        // Save to database as a persistent Singer
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                var dbSinger = context.Singers.FirstOrDefault(s => s.Name == name);
                if (dbSinger == null)
                {
                    dbSinger = new Lyracist.Data.Models.Singer { Name = name, JoinDate = System.DateTime.UtcNow };
                    context.Singers.Add(dbSinger);
                    context.SaveChanges();

                    // Reload names
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        LoadSingerNames();
                    });
                }
            }
            catch { }
        });

        Rotation.Add(new Singer
        {
            Name = name,
            SongTitle = title,
            Artist = artist,
            Key = key,
            Notes = notes,
            Source = source,
            ExternalLink = externalLink,
            DuetPartnerName = duetPartner,
            Score = score,
            AverageRating = avgRating,
            RatingCount = ratingCount,
            TotalSongsSung = totalSongsSung
        });

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void AddSinger()
    {
        if (string.IsNullOrWhiteSpace(NewSingerName))
            return;

        Rotation.Add(new Singer
        {
            Name = NameFormatting.ProperCase(NewSingerName),
            DuetPartnerName = NameFormatting.ProperCase(NewDuetPartnerName),
            Notes = NewSingerNotes,
            Key = NewSingerKey
        });

        // Reset input properties
        NewSingerName = string.Empty;
        NewDuetPartnerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }


    [RelayCommand]
    private async Task RemoveSinger()
    {
        if (SelectedSinger != null)
        {
            string name = SelectedSinger.Name;
            string title = SelectedSinger.SongTitle;
            string artist = SelectedSinger.Artist;

            Rotation.Remove(SelectedSinger);
            SelectedSinger = null;

            // If this performer is currently singing, stop playback
            if (string.Equals(name, _mediaEngine.ActiveSingerName, StringComparison.OrdinalIgnoreCase))
            {
                await _mediaEngine.Stop();
            }

            // Log performance history in database
            SavePerformanceHistory(name, title, artist);

            // Check if there is a pending song for this singer!
            if (_pendingSingerSongs.TryGetValue(name, out var list) && list.Count > 0)
            {
                var nextSong = list[0];
                list.RemoveAt(0);

                // Add the singer back to the end of the rotation with the next song
                Rotation.Add(new Singer
                {
                    Name = name,
                    SongTitle = nextSong.Title,
                    Artist = nextSong.Artist,
                    Key = nextSong.Key,
                    Notes = nextSong.Notes,
                    Source = nextSong.Source,
                    ExternalLink = nextSong.ExternalLink
                });
            }

            if (SelectedSinger != null && SelectedSinger.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                RefreshSelectedSingerQueue();
            }

            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedSinger == null)
            return;

        int index = Rotation.IndexOf(SelectedSinger);
        if (index > 0)
        {
            var singer = SelectedSinger;
            Rotation.RemoveAt(index);
            Rotation.Insert(index - 1, singer);
            SelectedSinger = singer;

            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedSinger == null)
            return;

        int index = Rotation.IndexOf(SelectedSinger);
        if (index < Rotation.Count - 1)
        {
            var singer = SelectedSinger;
            Rotation.RemoveAt(index);
            Rotation.Insert(index + 1, singer);
            SelectedSinger = singer;

            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
        }
    }

    [RelayCommand]
    private void MarkAsSinging()
    {
        if (SelectedSinger == null)
            return;

        _display.ShowRotationWindow();
        _display.HighlightSinger(SelectedSinger);
    }

    [RelayCommand]
    private void ClearRotation()
    {
        Rotation.Clear();
        SelectedSinger = null;

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void SetCurrentSinger(Singer singer)
    {
        if (singer == null)
            return;

        Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, singer);

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void DoneSinger(Singer singer)
    {
        if (singer == null) return;

        string name = singer.Name;
        string title = singer.SongTitle;
        string artist = singer.Artist;

        // 1. Increment completed count (cap at 10) and total songs sung
        singer.CompletedCount = Math.Min(singer.CompletedCount + 1, 10);
        singer.TotalSongsSung++;

        // 2. Log performance history in database
        SavePerformanceHistory(name, title, artist);

        // If the singer being marked Done is the current singer, advance the indicator first
        if (singer.IsCurrent)
        {
            // Find the next active singer (the one currently marked as IsNext)
            var nextActive = Rotation.FirstOrDefault(s => s.IsNext);
            if (nextActive != null)
            {
                nextActive.IsCurrent = true;
                singer.IsCurrent = false;
            }
            else
            {
                singer.IsCurrent = false;
            }
        }

        // 3. Check for pending songs
        if (_pendingSingerSongs.TryGetValue(name, out var list) && list.Count > 0)
        {
            var nextSong = list[0];
            list.RemoveAt(0);

            // Update to next song
            singer.SongTitle = nextSong.Title;
            singer.Artist = nextSong.Artist;
            singer.Key = nextSong.Key;
            singer.Notes = nextSong.Notes;
            singer.Source = nextSong.Source;
            singer.ExternalLink = nextSong.ExternalLink;
        }
        else
        {
            // Clear song
            singer.SongTitle = string.Empty;
            singer.Artist = string.Empty;
            singer.Key = "0";
            singer.ExternalLink = string.Empty;
            singer.Source = "Local";
        }

        if (SelectedSinger != null && SelectedSinger.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            RefreshSelectedSingerQueue();
        }

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private async Task TogglePauseSinger(Singer singer)
    {
        if (singer == null) return;
        singer.IsPaused = !singer.IsPaused;

        // If they are currently singing and we paused them, pause the music
        if (singer.IsPaused && string.Equals(singer.Name, _mediaEngine.ActiveSingerName, StringComparison.OrdinalIgnoreCase))
        {
            await _mediaEngine.Pause();
        }

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private async Task ToggleInactiveSinger(Singer singer)
    {
        if (singer == null) return;

        if (singer.IsInactive)
        {
            singer.IsInactive = false;
            InactiveSingers.Remove(singer);
            Rotation.Add(singer);
        }
        else
        {
            singer.IsInactive = true;
            Rotation.Remove(singer);
            InactiveSingers.Add(singer);

            // If they are currently singing and we set them inactive, stop playback
            if (string.Equals(singer.Name, _mediaEngine.ActiveSingerName, StringComparison.OrdinalIgnoreCase))
            {
                await _mediaEngine.Stop();
            }
        }

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void DeleteInactiveSinger(Singer singer)
    {
        if (singer == null) return;
        InactiveSingers.Remove(singer);

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void MovePendingSongUp(PendingSong song)
    {
        if (SelectedSinger == null || song == null) return;
        if (_pendingSingerSongs.TryGetValue(SelectedSinger.Name, out var queue))
        {
            int index = queue.IndexOf(song);
            if (index > 0)
            {
                queue.RemoveAt(index);
                queue.Insert(index - 1, song);
                RefreshSelectedSingerQueue();
                RotationStateChanged?.Invoke();
            }
        }
    }

    [RelayCommand]
    private void MovePendingSongDown(PendingSong song)
    {
        if (SelectedSinger == null || song == null) return;
        if (_pendingSingerSongs.TryGetValue(SelectedSinger.Name, out var queue))
        {
            int index = queue.IndexOf(song);
            if (index >= 0 && index < queue.Count - 1)
            {
                queue.RemoveAt(index);
                queue.Insert(index + 1, song);
                RefreshSelectedSingerQueue();
                RotationStateChanged?.Invoke();
            }
        }
    }

    [RelayCommand]
    private void RemovePendingSong(PendingSong song)
    {
        if (SelectedSinger == null || song == null) return;
        if (_pendingSingerSongs.TryGetValue(SelectedSinger.Name, out var queue))
        {
            queue.Remove(song);
            RefreshSelectedSingerQueue();
            RotationStateChanged?.Invoke();
        }
    }

    public void NotifyRotationReordered()
    {
        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    private void SavePerformanceHistory(string name, string title, string artist)
    {
        if (string.IsNullOrEmpty(name)) return;

        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                var dbSinger = context.Singers.FirstOrDefault(s => s.Name == name);
                if (dbSinger != null)
                {
                    dbSinger.TotalSongsSung += 1;
                    dbSinger.LastSang = System.DateTime.UtcNow;
                    context.Singers.Update(dbSinger);

                    // Find corresponding song to get SongId, or create a transient one
                    var dbSong = context.Songs.FirstOrDefault(s => s.Title == title && s.Artist == artist);
                    if (dbSong == null && !string.IsNullOrEmpty(title))
                    {
                        dbSong = new Lyracist.Data.Models.Song
                        {
                            Title = title,
                            Artist = artist,
                            IsKaraoke = true,
                            FilePath = "External"
                        };
                        context.Songs.Add(dbSong);
                        context.SaveChanges();
                    }

                    if (dbSong != null)
                    {
                        var entry = new Lyracist.Data.Models.RotationEntry
                        {
                            SingerId = dbSinger.SingerId,
                            SongId = dbSong.SongId,
                            Status = "Finished",
                            TimestampAdded = System.DateTime.UtcNow
                        };
                        context.RotationEntries.Add(entry);
                    }

                    context.SaveChanges();
                }
            }
            catch { }
        });
    }
}