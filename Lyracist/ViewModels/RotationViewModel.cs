// Edited on Sep 4, 2026 @ 07:25:00 -> Add ToggleLastRoundCommand
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Core.Models;
using Lyracist.Models;
using Lyracist.Services.Display;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    // Guards against the Rotation.CollectionChanged handler reacting mid-operation to the
    // intermediate RemoveAt+Insert pairs that RotationHelpers performs internally (e.g. inside
    // AdvanceRotationAfterFinished's float branch), which would otherwise fight the helper's own
    // reordering before it finishes and leave the current singer stuck instead of at the top.
    private bool _suppressRotationOrderSync;

    /// <summary>
    /// Runs <paramref name="action"/> with the CollectionChanged auto-sync handler suppressed, then
    /// re-syncs the rotation-start flag and (if enabled) floats the current singer to the top once,
    /// after the action's own list mutations have all completed.
    /// </summary>
    private void RunRotationOrderChange(Action action)
    {
        _suppressRotationOrderSync = true;
        try
        {
            action();
        }
        finally
        {
            _suppressRotationOrderSync = false;
        }

        Lyracist.Shared.RotationHelpers.EnsureRotationStartFlag(Rotation);
        if (FloatCurrentSingerToTop)
        {
            Lyracist.Shared.RotationHelpers.FloatCurrentSingerToTop(Rotation);
        }

        // Keeps wait-time badges current after every rotation-order change (move, set current,
        // skip, etc.), not just when a singer is marked done - otherwise a fresh rotation shows no
        // badges at all until the first singer finishes, since nothing else recalculates them.
        Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits(Rotation, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: AppSettings.DefaultSongLengthMinutes * 60.0);
    }

    public event Action? RotationStateChanged;

    public ObservableCollection<Singer> Rotation { get; } = [];

    public ObservableCollection<Singer> InactiveSingers { get; } = [];

    public ObservableCollection<string> SingerNames { get; } = [];

    public ObservableCollection<PerformedSong> SessionPerformedSongs { get; } = [];

    [ObservableProperty]
    private bool _floatCurrentSingerToTop;

    [ObservableProperty]
    private bool _isLastRound;

    partial void OnIsLastRoundChanged(bool value)
    {
        if (value)
        {
            foreach (var singer in Rotation)
            {
                singer.HasSungInLastRound = false;
            }
        }
        Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(Rotation, isLastRound: value);
        _display.SetLastRound(value);
        _display.UpdateRotation([.. Rotation]);
        RotationStateChanged?.Invoke();
    }

    [RelayCommand]
    public void ToggleLastRound()
    {
        IsLastRound = !IsLastRound;
    }

    partial void OnFloatCurrentSingerToTopChanged(bool value)
    {
        AppSettings.FloatCurrentSingerToTop = value;
        if (value)
        {
            RunRotationOrderChange(() =>
            {
                var current = Lyracist.Shared.RotationHelpers.GetCurrentSinger(Rotation);
                if (current == null)
                {
                    var first = Rotation.FirstOrDefault(s => s.IsRotationStart && !s.IsInactive && !s.IsPaused)
                                ?? Rotation.FirstOrDefault(s => !s.IsInactive && !s.IsPaused);
                    if (first != null)
                    {
                        Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, first, floatCurrentToTop: true, isLastRound: IsLastRound);
                    }
                }
                else
                {
                    Lyracist.Shared.RotationHelpers.FloatCurrentSingerToTop(Rotation);
                }
            });
            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
        }
    }

    public string SessionPerformedSongsText => SessionPerformedSongs.Count == 0
        ? "No songs performed in this session yet."
        : string.Join(Environment.NewLine, SessionPerformedSongs.Select(s => s.FormattedText));

    public void RecordPerformedSong(string singerName, string songTitle, string artist, string key = "0", string duetPartner = "")
    {
        if (string.IsNullOrWhiteSpace(singerName)) return;

        var performed = new PerformedSong
        {
            OrderNumber = SessionPerformedSongs.Count + 1,
            SingerName = singerName,
            DuetPartnerName = duetPartner ?? string.Empty,
            SongTitle = string.IsNullOrWhiteSpace(songTitle) ? "Unknown Song" : songTitle,
            Artist = artist ?? string.Empty,
            Key = key ?? "0",
            PerformedAt = DateTime.Now
        };

        SessionPerformedSongs.Add(performed);
        OnPropertyChanged(nameof(SessionPerformedSongsText));
    }

    [RelayCommand]
    private void ClearPerformedSongs()
    {
        SessionPerformedSongs.Clear();
        OnPropertyChanged(nameof(SessionPerformedSongsText));
    }

    [RelayCommand]
    private void CopyPerformedSongs()
    {
        if (SessionPerformedSongs.Count == 0) return;
        try
        {
            System.Windows.Clipboard.SetText(SessionPerformedSongsText);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "CopyPerformedSongs: failed to copy to clipboard");
        }
    }

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
        _floatCurrentSingerToTop = AppSettings.FloatCurrentSingerToTop;
        Rotation.CollectionChanged += (_, _) =>
        {
            if (_suppressRotationOrderSync) return;

            Lyracist.Shared.RotationHelpers.EnsureRotationStartFlag(Rotation);
            if (FloatCurrentSingerToTop)
            {
                Lyracist.Shared.RotationHelpers.FloatCurrentSingerToTop(Rotation);
            }
        };

        try
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.Changed += (theme, accent) =>
            {
                // PerformedSong isn't INotifyPropertyChanged, so re-raising PropertyChanged for the
                // (unchanged) collection reference won't make the ListBox re-run item converters.
                // Resetting via Clear+re-Add forces WPF to regenerate containers against the new theme.
                var songs = SessionPerformedSongs.ToList();
                SessionPerformedSongs.Clear();
                foreach (var song in songs)
                {
                    SessionPerformedSongs.Add(song);
                }
            };
        }
        catch
        {
            // Ignore if theme manager is unattached
        }

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
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "SeedSingers: failed to save seed singers to database");
            }
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
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "LoadSingerNames: failed to load singer names from database");
        }
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
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "OnNewSingerNameChanged: failed to load singer history for notes autofill");
            }
        });
    }

    public void AddSinger(string name, string title, string artist, string key, string notes, string source = "Local", string externalLink = "", string duetPartner = "", bool isMusic = false)
    {
        name = NameFormatting.ProperCase(name);
        duetPartner = NameFormatting.ProperCase(duetPartner);
        title = NameFormatting.ProperCase(title);
        artist = NameFormatting.ProperCase(artist);

        if (isMusic)
        {
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
                Score = 0,
                AverageRating = 0,
                RatingCount = 0,
                TotalSongsSung = 0,
                IsMusic = true
            });

            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
            return;
        }

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
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "AddSinger: failed to load existing singer stats from database");
        }

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
                existingSinger.IsMusic = isMusic;
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
            inactiveSinger.IsMusic = isMusic;
            
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
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "AddSinger: failed to persist new singer to database");
            }
        });

        var newSinger = new Singer
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
            TotalSongsSung = totalSongsSung,
            IsMusic = isMusic
        };
        RotationHelpers.InsertNewSinger(Rotation, newSinger);
        Lyracist.Shared.RotationHelpers.EnforceLinkedAdjacency(Rotation);
        RefreshLinkedPartnerNames();

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void AddSinger()
    {
        if (string.IsNullOrWhiteSpace(NewSingerName))
            return;

        var newSinger = new Singer
        {
            Name = NameFormatting.ProperCase(NewSingerName),
            DuetPartnerName = NameFormatting.ProperCase(NewDuetPartnerName),
            Notes = NewSingerNotes,
            Key = NewSingerKey
        };
        RotationHelpers.InsertNewSinger(Rotation, newSinger);
        Lyracist.Shared.RotationHelpers.EnforceLinkedAdjacency(Rotation);
        RefreshLinkedPartnerNames();
        Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits(Rotation, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: AppSettings.DefaultSongLengthMinutes * 60.0);

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
            var removed = SelectedSinger;
            string name = removed.Name;
            string title = removed.SongTitle;
            string artist = removed.Artist;

            bool wasCurrent = removed.IsCurrent;
            Singer? nextCurrent = null;

            if (wasCurrent)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                nextCurrent = Rotation.FirstOrDefault(s => s != removed && s.IsNext && !s.IsInactive && (!IsLastRound || !s.HasSungInLastRound));

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = Rotation.IndexOf(removed);
                    int count = Rotation.Count;
                    for (int i = 1; i < count; i++)
                    {
                        var candidate = Rotation[(currentIndex + i) % count];
                        if (candidate != removed && !candidate.IsInactive && (!IsLastRound || !candidate.HasSungInLastRound))
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
            }

            if (removed.IsRotationStart)
            {
                Lyracist.Shared.RotationHelpers.HandleSingerRetiredOrRemoved(Rotation, removed);
            }

            // Promote before removing: Rotation.Remove below fires CollectionChanged synchronously,
            // which KaraokeViewModel resyncs off of. If nobody were IsCurrent yet at that instant, its
            // resync would race our own promotion and pick a different singer (first-in-list-order)
            // before this method's own UpdateNextSingerHighlight call ran, leaving two singers
            // simultaneously flagged current.
            if (wasCurrent && nextCurrent != null)
            {
                RunRotationOrderChange(() =>
                    Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, nextCurrent, FloatCurrentSingerToTop, isLastRound: IsLastRound));
            }

            Rotation.Remove(removed);
            Lyracist.Shared.RotationHelpers.UnlinkSinger(Rotation, removed);
            RefreshLinkedPartnerNames();
            if (PendingLinkSinger == removed) PendingLinkSinger = null;
            Lyracist.Shared.RotationHelpers.EnsureRotationStartFlag(Rotation);
            SelectedSinger = null;

            if (wasCurrent)
            {
                Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(Rotation, isLastRound: IsLastRound);
            }

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

            Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits(Rotation, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: AppSettings.DefaultSongLengthMinutes * 60.0);
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
            RunRotationOrderChange(() =>
            {
                Rotation.RemoveAt(index);
                Rotation.Insert(index - 1, singer);
            });
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
            RunRotationOrderChange(() =>
            {
                Rotation.RemoveAt(index);
                Rotation.Insert(index + 1, singer);
            });
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

    public Singer? GetCurrentSinger()
    {
        return Lyracist.Shared.RotationHelpers.GetCurrentSinger(Rotation) 
               ?? Rotation.FirstOrDefault(s => s.IsCurrent && !s.IsInactive && !s.IsPaused && (!IsLastRound || !s.HasSungInLastRound)) 
               ?? Rotation.FirstOrDefault(s => !s.IsInactive && !s.IsPaused && (!IsLastRound || !s.HasSungInLastRound));
    }

    public Singer? GetNextSinger()
    {
        var next = Rotation.FirstOrDefault(s => s.IsNext && !s.IsInactive && !s.IsPaused && (!IsLastRound || !s.HasSungInLastRound));
        if (next != null) return next;

        var current = GetCurrentSinger();
        if (current == null) return null;

        int currentIndex = Rotation.IndexOf(current);
        int count = Rotation.Count;
        if (currentIndex >= 0 && count > 1)
        {
            for (int i = 1; i < count; i++)
            {
                var candidate = Rotation[(currentIndex + i) % count];
                if (candidate != current && !candidate.IsInactive && !candidate.IsPaused && (!IsLastRound || !candidate.HasSungInLastRound))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    [RelayCommand]
    public void SkipCurrentSinger()
    {
        var current = GetCurrentSinger();
        if (current == null) return;

        RunRotationOrderChange(() =>
            Lyracist.Shared.RotationHelpers.AdvanceRotationAfterFinished(Rotation, current, FloatCurrentSingerToTop, isLastRound: IsLastRound));

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void SetCurrentSinger(Singer singer)
    {
        if (singer == null)
            return;

        RunRotationOrderChange(() =>
            Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, singer, FloatCurrentSingerToTop, isLastRound: IsLastRound));

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void SetRotationStartSinger(Singer singer)
    {
        if (singer == null)
            return;

        bool wasRotationStart = singer.IsRotationStart;
        RunRotationOrderChange(() =>
        {
            Lyracist.Shared.RotationHelpers.ToggleRotationStartSinger(Rotation, singer);
            if (!wasRotationStart && FloatCurrentSingerToTop)
            {
                Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, singer, floatCurrentToTop: true, isLastRound: IsLastRound);
            }
        });

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    [RelayCommand]
    private void DoneSinger(Singer singer)
    {
        if (singer == null) return;

        if (singer.IsMusic)
        {
            RunRotationOrderChange(() =>
                Lyracist.Shared.RotationHelpers.AdvanceRotationAfterFinished(Rotation, singer, FloatCurrentSingerToTop, isLastRound: IsLastRound));
            Rotation.Remove(singer);
            Lyracist.Shared.RotationHelpers.UnlinkSinger(Rotation, singer);
            if (PendingLinkSinger == singer) PendingLinkSinger = null;
            Lyracist.Shared.RotationHelpers.EnforceLinkedAdjacency(Rotation);
            RefreshLinkedPartnerNames();
            ResolveEstimatedPerformanceSeconds();
            Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits(Rotation, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: AppSettings.DefaultSongLengthMinutes * 60.0);
            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
            return;
        }

        string name = singer.Name;
        string title = singer.SongTitle;
        string artist = singer.Artist;

        if (IsLastRound)
        {
            singer.HasSungInLastRound = true;
        }

        // 1. Increment completed count (cap at 10) and total songs sung
        singer.CompletedCount = Math.Min(singer.CompletedCount + 1, 10);
        singer.TotalSongsSung++;

        // 2. Log performance history in database and session history
        SavePerformanceHistory(name, title, artist);
        RecordPerformedSong(name, title, artist, singer.Key, singer.DuetPartnerName);

        // Clear duet partner for subsequent rounds/songs in rotation
        singer.DuetPartnerName = string.Empty;

        // Advance rotation to next active singer relative to singer
        RunRotationOrderChange(() =>
            Lyracist.Shared.RotationHelpers.AdvanceRotationAfterFinished(Rotation, singer, FloatCurrentSingerToTop, isLastRound: IsLastRound));

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

        // Linked Singers stays linked all night (no auto-unlink) - EnforceLinkedAdjacency exempts a
        // pair while either half IsCurrent, so this won't drag the finished singer back up next to a
        // partner who was just promoted to perform next; once neither is current anymore (both have
        // had their turn), it re-unites them for their next joint turn.
        Lyracist.Shared.RotationHelpers.EnforceLinkedAdjacency(Rotation);
        RefreshLinkedPartnerNames();

        ResolveEstimatedPerformanceSeconds();
        Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits(Rotation, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: AppSettings.DefaultSongLengthMinutes * 60.0);

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    /// <summary>
    /// Singer the DJ has clicked "Link" on, waiting for a second click on the singer to link them
    /// with. Null when no link is pending.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingLinkSinger))]
    private Singer? _pendingLinkSinger;

    public bool HasPendingLinkSinger => PendingLinkSinger != null;

    [RelayCommand]
    private void CancelPendingLink() => PendingLinkSinger = null;

    /// <summary>
    /// Two-click linking: clicking "Link" on a singer with nothing pending arms it as the pending
    /// half of a pair; clicking a second (different) singer completes the link. Clicking the pending
    /// singer again cancels. Clicking an already-linked singer unlinks it instead. See
    /// <see cref="Lyracist.Shared.RotationHelpers.LinkSingers{T}"/>/<see cref="Lyracist.Shared.RotationHelpers.UnlinkSinger{T}"/>.
    /// </summary>
    [RelayCommand]
    private void ToggleLinkSinger(Singer singer)
    {
        if (singer == null) return;

        if (singer.IsLinked)
        {
            Lyracist.Shared.RotationHelpers.UnlinkSinger(Rotation, singer);
            RefreshLinkedPartnerNames();
            if (PendingLinkSinger == singer) PendingLinkSinger = null;
            RotationStateChanged?.Invoke();
            _display.UpdateRotation([.. Rotation]);
            return;
        }

        if (PendingLinkSinger == null)
        {
            PendingLinkSinger = singer;
            return;
        }

        if (PendingLinkSinger == singer)
        {
            PendingLinkSinger = null;
            return;
        }

        Lyracist.Shared.RotationHelpers.LinkSingers(Rotation, PendingLinkSinger, singer);
        RefreshLinkedPartnerNames();
        PendingLinkSinger = null;

        RotationStateChanged?.Invoke();
        _display.UpdateRotation([.. Rotation]);
    }

    /// <summary>
    /// Refreshes every linked singer's <see cref="Singer.LinkedPartnerName"/> display convenience
    /// from the current rotation. Name isn't part of the shared IRotationSinger interface, so this
    /// lookup has to happen here rather than in RotationHelpers - call it after anything that could
    /// change link state, rotation membership, or a linked singer's Name.
    /// </summary>
    private void RefreshLinkedPartnerNames()
    {
        foreach (var s in Rotation)
        {
            if (s.LinkedSingerId.HasValue)
            {
                var partner = Rotation.FirstOrDefault(p => p.Id == s.LinkedSingerId.Value);
                s.LinkedPartnerName = partner?.Name ?? string.Empty;
            }
            else if (!string.IsNullOrEmpty(s.LinkedPartnerName))
            {
                s.LinkedPartnerName = string.Empty;
            }
        }
    }

    /// <summary>
    /// Resolves each queued singer's <see cref="Singer.EstimatedPerformanceSeconds"/> (known song
    /// duration + 30s) from the library in a single batched lookup, so
    /// <see cref="Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits{T}"/> has fresh data
    /// every time it runs. A singer whose song isn't found in the library (or has no song queued)
    /// is left at 0, which RecalculateEstimatedWaits treats as "unknown" and falls back on its
    /// default per-song estimate for.
    /// </summary>
    private void ResolveEstimatedPerformanceSeconds()
    {
        var titles = Rotation
            .Where(s => !string.IsNullOrWhiteSpace(s.SongTitle))
            .Select(s => s.SongTitle)
            .Distinct()
            .ToList();

        if (titles.Count == 0) return;

        try
        {
            using var context = new Lyracist.Data.LyracistDbContext();
            var durationLookup = context.Songs
                .Where(s => titles.Contains(s.Title) && s.Duration > 0)
                .Select(s => new { s.Title, s.Artist, s.Duration })
                .ToList()
                .GroupBy(s => (s.Title, s.Artist))
                .ToDictionary(g => g.Key, g => g.First().Duration);

            foreach (var singer in Rotation)
            {
                if (durationLookup.TryGetValue((singer.SongTitle, singer.Artist), out double duration))
                {
                    singer.EstimatedPerformanceSeconds = duration + 30.0;
                }
                else
                {
                    singer.EstimatedPerformanceSeconds = 0;
                }
            }
        }
        catch (System.Exception ex)
        {
            AppLogger.LogError(ex, "ResolveEstimatedPerformanceSeconds: failed to resolve song durations for wait-time estimate");
        }
    }

    [RelayCommand]
    private void EditSinger(Singer singer)
    {
        if (singer == null) return;

        string oldName = singer.Name;

        var window = App.AppHost.Services.GetRequiredService<Lyracist.Windows.EditSingerWindow>();
        window.ViewModel.Load(singer);
        window.Owner = System.Windows.Application.Current.MainWindow;
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (!singer.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase)
            && SelectedSinger != null && SelectedSinger.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
        {
            RefreshSelectedSingerQueue();
        }

        // A renamed singer's partner (if any) displays LinkedPartnerName as a plain string
        // snapshot, not a live lookup - refresh it so the partner's badge doesn't go stale.
        if (!singer.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
        {
            RefreshLinkedPartnerNames();
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
            bool wasCurrent = singer.IsCurrent;
            Singer? nextCurrent = null;

            if (wasCurrent)
            {
                // 1. Try the singer already flagged as Next (manual next-singer override)
                nextCurrent = Rotation.FirstOrDefault(s => s != singer && s.IsNext && !s.IsInactive && (!IsLastRound || !s.HasSungInLastRound));

                if (nextCurrent == null)
                {
                    // 2. Fall back to standard index-based rotation
                    int currentIndex = Rotation.IndexOf(singer);
                    int count = Rotation.Count;
                    for (int i = 1; i < count; i++)
                    {
                        var candidate = Rotation[(currentIndex + i) % count];
                        if (candidate != singer && !candidate.IsInactive && (!IsLastRound || !candidate.HasSungInLastRound))
                        {
                            nextCurrent = candidate;
                            break;
                        }
                    }
                }
            }

            if (singer.IsRotationStart)
            {
                Lyracist.Shared.RotationHelpers.HandleSingerRetiredOrRemoved(Rotation, singer);
            }

            singer.IsInactive = true;

            // Promote before removing from Rotation below (which fires CollectionChanged
            // synchronously, resynced by KaraokeViewModel) — see RemoveSinger for why the order matters.
            if (wasCurrent)
            {
                singer.IsCurrent = false;
                if (nextCurrent != null)
                {
                    RunRotationOrderChange(() =>
                        Lyracist.Shared.RotationHelpers.SetCurrentSinger(Rotation, nextCurrent, FloatCurrentSingerToTop, isLastRound: IsLastRound));
                }
            }

            Rotation.Remove(singer);
            Lyracist.Shared.RotationHelpers.EnsureRotationStartFlag(Rotation);
            InactiveSingers.Add(singer);

            // If they are currently singing and we set them inactive, stop playback
            if (string.Equals(singer.Name, _mediaEngine.ActiveSingerName, StringComparison.OrdinalIgnoreCase))
            {
                await _mediaEngine.Stop();
            }

            if (wasCurrent)
            {
                Lyracist.Shared.RotationHelpers.UpdateNextSingerHighlight(Rotation, isLastRound: IsLastRound);
            }
        }

        Lyracist.Shared.RotationHelpers.RecalculateEstimatedWaits(Rotation, isLastRound: IsLastRound, defaultEstimatedPerformanceSeconds: AppSettings.DefaultSongLengthMinutes * 60.0);
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
        // A manual drag/drop reorder could have dropped a singer between a linked pair (or dragged
        // one half of a pair away from the other) - snap the pair back adjacent before notifying.
        Lyracist.Shared.RotationHelpers.EnforceLinkedAdjacency(Rotation);
        RefreshLinkedPartnerNames();

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
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "SavePerformanceHistory: failed to save performance history to database");
            }
        });
    }
}