// Edited on Oct 3, 2026 @ 08:20:00 -> Guard against TaskCanceledException when Dispatcher is canceled or shutting down during library scan and probing
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
    public event EventHandler<ScanProgress>? ScanProgressChanged;
    public event EventHandler<string>? ScanFailed;
    public event EventHandler<ScanProgress>? MetadataProbeProgressChanged;
    public event EventHandler? MetadataProbeCompleted;

    public void NotifyLibraryUpdated() => LibraryUpdated?.Invoke(this, EventArgs.Empty);

    public void ScanDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        // Persist so this directory survives app restarts and can be rescanned.
        AppSettings.AddLibraryDirectory(path);

        RunScan(new[] { path });
    }

    public void RescanAllDirectories()
    {
        var dirs = AppSettings.LibraryDirectories.ToList();
        if (dirs.Count == 0) return;

        RunScan(dirs);
    }

    private void RunScan(IEnumerable<string> dirs)
    {
        Task.Run(async () =>
        {
            using var context = new LyracistDbContext();
            var scanningService = new ScanningService(context);

            try
            {
                var scanProgress = new Progress<ScanProgress>(p =>
                {
                    try
                    {
                        ScanProgressChanged?.Invoke(this, p);
                    }
                    catch (OperationCanceledException) { }
                });
                await scanningService.ScanDirectories(dirs, scanProgress);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Library scan failed", ex);
                ScanFailed?.Invoke(this, ex.Message);
                // Always notify so the UI can clear its "Scanning…" state, even on failure.
                LibraryUpdated?.Invoke(this, EventArgs.Empty);
                return;
            }

            // The scan itself is fast (no metadata probing) — songs are already inserted and
            // usable at this point, so let the UI drop out of "Scanning…" now.
            LibraryUpdated?.Invoke(this, EventArgs.Empty);

            // Fill in duration/genre afterward as a separate, low-priority background pass so a
            // large library doesn't hold up the scan. Safe to interrupt — it just picks back up
            // with whatever songs are still missing metadata next time a scan runs.
            try
            {
                var probeProgress = new Progress<ScanProgress>(p =>
                {
                    try
                    {
                        MetadataProbeProgressChanged?.Invoke(this, p);
                    }
                    catch (OperationCanceledException) { }
                });
                await scanningService.ProbeMissingMetadataAsync(probeProgress);
                MetadataProbeCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                // App or dispatcher shutting down / canceled — exit cleanly
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Background metadata probing failed", ex);
                ScanFailed?.Invoke(this, $"Metadata fill-in error: {ex.Message}");
            }
        });
    }

    public void RemoveSongsUnderDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            // Shared with LyracistDbEditor: songs also owned by another registered directory are
            // kept, and removed songs are dropped from the search index too.
            using var context = new LyracistDbContext();
            var maintenance = new LibraryMaintenanceService(context);

            var ids = maintenance
                .FindSongIdsUnderDirectoryAsync(path, AppSettings.LibraryDirectories)
                .GetAwaiter().GetResult();
            if (ids.Count == 0) return;

            maintenance.RemoveSongsAsync(ids).GetAwaiter().GetResult();

            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", $"Failed to remove songs under directory {path}", ex);
        }
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

    public IEnumerable<KaraokeSong> Search(string query, bool isMusic = false)
    {
        try
        {
            using var context = new LyracistDbContext();

            if (string.IsNullOrWhiteSpace(query))
            {
                return context.Songs.AsNoTracking().Where(s => s.IsKaraoke == !isMusic).Take(150).ToList().Select(MapToKaraokeSong);
            }

            var searchService = new SearchService(context);
            // Run matching search synchronously on SQLite FTS5 table
            var results = searchService.SearchSync(query, isKaraoke: !isMusic);
            return results.Select(MapToKaraokeSong);
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to search library", ex);
            return [];
        }
    }

    public async Task<IEnumerable<KaraokeSong>> SearchAsync(string query, bool isMusic = false)
    {
        try
        {
            using var context = new LyracistDbContext();

            if (string.IsNullOrWhiteSpace(query))
            {
                var all = await context.Songs.AsNoTracking().Where(s => s.IsKaraoke == !isMusic).Take(150).ToListAsync();
                return all.Select(MapToKaraokeSong);
            }

            var searchService = new SearchService(context);
            var results = await searchService.Search(query, isKaraoke: !isMusic);
            return results.Select(MapToKaraokeSong);
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to search library", ex);
            return [];
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to get all library songs", ex);
            return [];
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to get background music songs", ex);
            return [];
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to get song audio settings", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to save song audio settings", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to get singer audio settings", ex);
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
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to save singer audio settings", ex);
        }
    }

    // Guards against kicking off a duplicate ffmpeg loudness measurement for the same file if it's
    // loaded again (e.g. the DJ replays a track) while an earlier measurement is still running.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _loudnessMeasurementsInFlight = new(StringComparer.OrdinalIgnoreCase);

    public double? GetMeasuredLoudness(string audioPath)
    {
        try
        {
            using var context = new LyracistDbContext();
            return context.Songs
                .AsNoTracking()
                .Where(s => s.FilePath == audioPath)
                .Select(s => s.MeasuredLoudnessLufs)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to get measured loudness", ex);
            return null;
        }
    }

    public async Task MeasureAndSaveLoudnessAsync(string audioPath)
    {
        if (string.IsNullOrEmpty(audioPath) || !_loudnessMeasurementsInFlight.TryAdd(audioPath, 0))
        {
            return;
        }

        try
        {
            double? lufs = await FFmpegService.MeasureIntegratedLoudness(audioPath);
            if (!lufs.HasValue) return;

            using var context = new LyracistDbContext();
            var dbSong = context.Songs.FirstOrDefault(s => s.FilePath == audioPath);
            if (dbSong == null) return;

            dbSong.MeasuredLoudnessLufs = lufs.Value;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", $"Failed to measure/save loudness for {audioPath}", ex);
        }
        finally
        {
            _loudnessMeasurementsInFlight.TryRemove(audioPath, out _);
        }
    }
}
