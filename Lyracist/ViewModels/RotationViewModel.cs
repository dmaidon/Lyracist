using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Lyracist.Models;
using Lyracist.Services.Display;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.ViewModels;

public partial class RotationViewModel : BaseViewModel
{
    private readonly IDisplayService _display;

    public ObservableCollection<Singer> Rotation { get; } = new();

    public ObservableCollection<string> SingerNames { get; } = new();

    private readonly Dictionary<string, List<(string Title, string Artist, string Key, string Notes, string Source, string ExternalLink)>> _pendingSingerSongs = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private Singer? _selectedSinger;

    [ObservableProperty]
    private string _newSingerName = string.Empty;

    [ObservableProperty]
    private string _newSingerNotes = string.Empty;

    [ObservableProperty]
    private string _newSingerKey = "0";

    public RotationViewModel(IDisplayService display)
    {
        _display = display;

        if (AppSettings.IsTestMode)
        {
            SeedSingers();
        }
        LoadSingerNames();
    }

    public void SeedSingers()
    {
        Rotation.Clear();
        Rotation.Add(new Singer { Name = "Alice Johnson", Key = "+1", Notes = "Sings soprano, prefers classic pop", SongTitle = "Sweet Caroline", Artist = "Neil Diamond" });
        Rotation.Add(new Singer { Name = "Bob Caruthers", Key = "-2", Notes = "Prefers baritone classic rock", SongTitle = "Hotel California", Artist = "Eagles" });
        Rotation.Add(new Singer { Name = "Charlie Brown", Key = "0", Notes = "First time singing today", SongTitle = "Billie Jean", Artist = "Michael Jackson" });
        Rotation.Add(new Singer { Name = "Diana Smith", Key = "+2", Notes = "Sings alto, loves jazz standards", SongTitle = "Fly Me to the Moon", Artist = "Frank Sinatra" });
        _display.UpdateRotation(Rotation.ToList());
    }

    public void ClearRotationQueue()
    {
        Rotation.Clear();
        _display.UpdateRotation(Rotation.ToList());
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

    public void AddSinger(string name, string title, string artist, string key, string notes, string source = "Local", string externalLink = "")
    {
        // Save to singer song history database
        System.Threading.Tasks.Task.Run(() =>
        {
            Lyracist.Services.Database.SingerHistoryService.SaveHistory(name, title, artist, source, externalLink);
        });

        // First, check if singer already exists in active rotation:
        var existingSinger = Rotation.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existingSinger != null)
        {
            // Buffer/queue the song request for this singer
            if (!_pendingSingerSongs.TryGetValue(name, out var list))
            {
                list = new();
                _pendingSingerSongs[name] = list;
            }
            list.Add((title, artist, key, notes, source, externalLink));
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
            ExternalLink = externalLink
        });

        _display.UpdateRotation(Rotation.ToList());
    }

    [RelayCommand]
    private void AddSinger()
    {
        if (string.IsNullOrWhiteSpace(NewSingerName))
            return;

        Rotation.Add(new Singer
        {
            Name = NewSingerName,
            Notes = NewSingerNotes,
            Key = NewSingerKey
        });

        // Reset input properties
        NewSingerName = string.Empty;
        NewSingerNotes = string.Empty;
        NewSingerKey = "0";

        _display.UpdateRotation(Rotation.ToList());
    }

    [RelayCommand]
    private void RemoveSinger()
    {
        if (SelectedSinger != null)
        {
            string name = SelectedSinger.Name;
            string title = SelectedSinger.SongTitle;
            string artist = SelectedSinger.Artist;

            Rotation.Remove(SelectedSinger);
            SelectedSinger = null;

            // Log performance history in database
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

            _display.UpdateRotation(Rotation.ToList());
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

            _display.UpdateRotation(Rotation.ToList());
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

            _display.UpdateRotation(Rotation.ToList());
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

        _display.UpdateRotation(Rotation.ToList());
    }
}