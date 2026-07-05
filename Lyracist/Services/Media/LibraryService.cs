using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Models;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Services.Media;

public class LibraryService : ILibraryService
{
    public event EventHandler? LibraryUpdated;

    public void ScanDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        // Persist so this directory survives app restarts and can be rescanned.
        AppSettings.AddLibraryDirectory(path);

        Task.Run(async () =>
        {
            try
            {
                using var context = new LyracistDbContext();
                var scanningService = new ScanningService(context);
                await scanningService.ScanDirectories(new[] { path });
                LibraryUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to scan library directory: {ex.Message}");
            }
        });
    }

    public void RescanAllDirectories()
    {
        foreach (var dir in AppSettings.LibraryDirectories)
            ScanDirectory(dir);
    }

    public int GetSongCount()
    {
        try
        {
            using var context = new LyracistDbContext();
            return context.Songs.Count();
        }
        catch
        {
            return 0;
        }
    }

    public IEnumerable<KaraokeSong> Search(string query)
    {
        try
        {
            using var context = new LyracistDbContext();

            if (string.IsNullOrWhiteSpace(query))
            {
                return context.Songs.AsNoTracking().ToList().Select(MapToKaraokeSong);
            }

            var searchService = new SearchService(context);
            // Run matching search on SQLite FTS5 table
            var results = Task.Run(() => searchService.Search(query)).Result;
            return results.Select(MapToKaraokeSong);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to search library: {ex.Message}");
            return Enumerable.Empty<KaraokeSong>();
        }
    }

    public IEnumerable<KaraokeSong> GetAllSongs()
    {
        try
        {
            using var context = new LyracistDbContext();
            return context.Songs.AsNoTracking().ToList().Select(MapToKaraokeSong);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get all library songs: {ex.Message}");
            return Enumerable.Empty<KaraokeSong>();
        }
    }

    public IEnumerable<KaraokeSong> GetBackgroundMusicSongs()
    {
        try
        {
            using var context = new LyracistDbContext();
            return context.Songs
                .AsNoTracking()
                .Where(s => !s.IsKaraoke)
                .ToList()
                .Select(MapToKaraokeSong);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get background music songs: {ex.Message}");
            return Enumerable.Empty<KaraokeSong>();
        }
    }

    private KaraokeSong MapToKaraokeSong(Song song)
    {
        string? cdgPath = null;
        if (song.IsKaraoke)
        {
            if (song.KaraokeType == "MP3G")
            {
                cdgPath = Path.ChangeExtension(song.FilePath, ".cdg");
            }
            else
            {
                cdgPath = song.FilePath; // ZIPCDG or MP4
            }
        }

        return new KaraokeSong
        {
            Title = song.Title,
            Artist = song.Artist,
            AudioPath = song.FilePath,
            CdgPath = cdgPath,
            IsKaraoke = song.IsKaraoke
        };
    }

    public SongAudioSettings GetAudioSettings(string audioPath)
    {
        try
        {
            using var context = new LyracistDbContext();
            var dbSong = context.Songs
                .Include(s => s.AudioSettings)
                .FirstOrDefault(s => s.FilePath == audioPath);

            if (dbSong == null)
            {
                return new SongAudioSettings();
            }

            if (dbSong.AudioSettings == null)
            {
                return new SongAudioSettings
                {
                    SongId = dbSong.SongId,
                    Gain = 100.0,
                    Tempo = 1.0,
                    Key = 0
                };
            }

            return dbSong.AudioSettings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get song audio settings: {ex.Message}");
            return new SongAudioSettings();
        }
    }

    public void SaveAudioSettings(string audioPath, SongAudioSettings settings)
    {
        try
        {
            using var context = new LyracistDbContext();
            var dbSong = context.Songs
                .Include(s => s.AudioSettings)
                .FirstOrDefault(s => s.FilePath == audioPath);

            if (dbSong == null) return;

            if (dbSong.AudioSettings == null)
            {
                dbSong.AudioSettings = new SongAudioSettings
                {
                    SongId = dbSong.SongId
                };
                context.SongAudioSettings.Add(dbSong.AudioSettings);
            }

            dbSong.AudioSettings.Treble = settings.Treble;
            dbSong.AudioSettings.Mid = settings.Mid;
            dbSong.AudioSettings.Bass = settings.Bass;
            dbSong.AudioSettings.Gain = settings.Gain;
            dbSong.AudioSettings.Key = settings.Key;
            dbSong.AudioSettings.Tempo = settings.Tempo;
            dbSong.AudioSettings.Compressor = settings.Compressor;
            dbSong.AudioSettings.Limiter = settings.Limiter;
            dbSong.AudioSettings.Notes = settings.Notes ?? string.Empty;

            context.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save song audio settings: {ex.Message}");
        }
    }

    public SingerAudioSettings GetSingerSettings(string singerName)
    {
        try
        {
            using var context = new LyracistDbContext();
            var dbSinger = context.Singers
                .Include(s => s.AudioSettings)
                .FirstOrDefault(s => s.Name == singerName);

            if (dbSinger == null)
            {
                return new SingerAudioSettings
                {
                    Gain = 100.0,
                    Tempo = 1.0,
                    Key = 0
                };
            }

            if (dbSinger.AudioSettings == null)
            {
                return new SingerAudioSettings
                {
                    SingerId = dbSinger.SingerId,
                    Gain = 100.0,
                    Tempo = 1.0,
                    Key = 0
                };
            }

            return dbSinger.AudioSettings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get singer audio settings: {ex.Message}");
            return new SingerAudioSettings();
        }
    }

    public void SaveSingerSettings(string singerName, SingerAudioSettings settings)
    {
        try
        {
            using var context = new LyracistDbContext();
            var dbSinger = context.Singers
                .Include(s => s.AudioSettings)
                .FirstOrDefault(s => s.Name == singerName);

            if (dbSinger == null)
            {
                dbSinger = new Lyracist.Data.Models.Singer { Name = singerName };
                context.Singers.Add(dbSinger);
                context.SaveChanges(); // Generate SingerId
            }

            if (dbSinger.AudioSettings == null)
            {
                dbSinger.AudioSettings = new SingerAudioSettings
                {
                    SingerId = dbSinger.SingerId
                };
                context.SingerAudioSettings.Add(dbSinger.AudioSettings);
            }

            dbSinger.AudioSettings.Treble = settings.Treble;
            dbSinger.AudioSettings.Mid = settings.Mid;
            dbSinger.AudioSettings.Bass = settings.Bass;
            dbSinger.AudioSettings.Gain = settings.Gain;
            dbSinger.AudioSettings.Key = settings.Key;
            dbSinger.AudioSettings.Tempo = settings.Tempo;
            dbSinger.AudioSettings.Compressor = settings.Compressor;
            dbSinger.AudioSettings.Limiter = settings.Limiter;
            dbSinger.AudioSettings.Notes = settings.Notes ?? string.Empty;

            context.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save singer audio settings: {ex.Message}");
        }
    }
}
