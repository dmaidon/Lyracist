using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Models;

namespace Lyracist.Services.Media;

public class LibraryService : ILibraryService
{
    public event EventHandler? LibraryUpdated;

    public void ScanDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        // Run scanning in background to avoid blocking the WPF UI thread
        Task.Run(async () =>
        {
            try
            {
                using var context = new LyracistDbContext();
                var scanningService = new ScanningService(context);
                
                await scanningService.ScanDirectories(new[] { path });

                // Notify view models and subscribers that library is updated
                LibraryUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to scan library directory: {ex.Message}");
            }
        });
    }

    public IEnumerable<KaraokeSong> Search(string query)
    {
        try
        {
            using var context = new LyracistDbContext();
            
            if (string.IsNullOrWhiteSpace(query))
            {
                return context.Songs.ToList().Select(MapToKaraokeSong);
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
            return context.Songs.ToList().Select(MapToKaraokeSong);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get all library songs: {ex.Message}");
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
            CdgPath = cdgPath
        };
    }
}
