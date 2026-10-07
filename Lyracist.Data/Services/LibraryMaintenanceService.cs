// Created on Oct 7, 2026 @ 15:00:00 -> Shared directory-removal logic used by both Lyracist and LyracistDbEditor
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Data.Services
{
    /// <summary>
    /// Library clean-up operations shared by Lyracist and LyracistDbEditor, so both apps decide
    /// which songs belong to a folder - and remove them from the database and the search index -
    /// in exactly the same way.
    /// </summary>
    public class LibraryMaintenanceService(LyracistDbContext context)
    {
        private readonly LyracistDbContext _context = context;

        /// <summary>
        /// True if filePath is inside directoryPath (or a subfolder of it). A plain StartsWith
        /// on the raw strings would also match an unrelated sibling folder that happens to share
        /// the prefix (e.g. "C:\Music" would match "C:\Music2\..."), so this normalizes with a
        /// trailing separator and compares case-insensitively.
        /// </summary>
        public static bool IsPathUnderDirectory(string filePath, string directoryPath)
        {
            string normalizedDir = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return filePath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ids of songs that live under <paramref name="directory"/> and are not also covered by
        /// another still-registered library directory (nested inside it or containing it), so
        /// removing one folder never deletes songs another registered folder still owns.
        /// Matching happens in memory rather than in SQL so case sensitivity and backslash
        /// escaping don't depend on how the provider translates StartsWith.
        /// </summary>
        public async Task<List<int>> FindSongIdsUnderDirectoryAsync(string directory, IEnumerable<string> otherRegisteredDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory)) return [];

            var others = otherRegisteredDirectories
                .Where(d => !string.IsNullOrWhiteSpace(d)
                            && !string.Equals(d.TrimEnd('\\', '/'), directory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var candidates = await _context.Songs
                .AsNoTracking()
                .Select(s => new { s.SongId, s.FilePath })
                .ToListAsync();

            return candidates
                .Where(c => !string.IsNullOrEmpty(c.FilePath)
                            && IsPathUnderDirectory(c.FilePath, directory)
                            && !others.Any(o => IsPathUnderDirectory(c.FilePath, o)))
                .Select(c => c.SongId)
                .ToList();
        }

        /// <summary>Deletes the songs and their search-index entries; returns how many rows were removed.</summary>
        public async Task<int> RemoveSongsAsync(IReadOnlyCollection<int> songIds)
        {
            if (songIds.Count == 0) return 0;

            var songs = await _context.Songs.Where(s => songIds.Contains(s.SongId)).ToListAsync();
            if (songs.Count == 0) return 0;

            var searchService = new SearchService(_context);
            _context.Songs.RemoveRange(songs);
            await _context.SaveChangesAsync();
            foreach (var song in songs)
            {
                await searchService.RemoveSongFromIndex(song.SongId);
            }
            return songs.Count;
        }
    }
}
