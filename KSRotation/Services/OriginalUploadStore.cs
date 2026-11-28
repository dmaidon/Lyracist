// Created on Nov 28, 2026 -> File naming, limits, and library indexing for performer-uploaded original tracks
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace KSRotation.Services
{
    public static class OriginalUploadStore
    {
        public const int MaxTrackBytes = 12_582_912; // 12 MB raw audio/video backing track

        /// <summary>
        /// Request body cap for the upload route only: base64 grows the file by a third, plus room for
        /// the data-URL prefix and the other JSON fields.
        /// </summary>
        public const int MaxUploadBodyBytes = ((MaxTrackBytes + 2) / 3 * 4) + 65_536;

        private const long MaxFolderBytes = 524_288_000; // 500 MB across all uploads
        private const int MaxNamePartLength = 60;

        public static readonly IReadOnlySet<string> AllowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".mp4", ".zip", ".m4a", ".ogg", ".flac" };

        public static string CleanFilePart(string input)
        {
            // Include the Windows-reserved set explicitly: on Android only '/' and NUL are "invalid".
            foreach (char c in Path.GetInvalidFileNameChars().Concat("\\/:*?\"<>|"))
            {
                input = input.Replace(c, '_');
            }
            input = input.Trim();
            return input.Length > MaxNamePartLength ? input[..MaxNamePartLength].Trim() : input;
        }

        /// <summary>Returns a path that doesn't exist yet, so one upload never replaces another.</summary>
        public static string GetUniquePath(string directory, string artist, string song, string extension)
        {
            string baseName = $"{CleanFilePart(artist)} - {CleanFilePart(song)} (Original)";
            string path = Path.Combine(directory, baseName + extension);
            for (int n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(directory, $"{baseName} {n}{extension}");
            }
            return path;
        }

        public static bool HasRoomFor(string directory, long newBytes)
        {
            if (!Directory.Exists(directory)) return true;
            long used = new DirectoryInfo(directory).EnumerateFiles().Sum(f => f.Length);
            return used + newBytes <= MaxFolderBytes;
        }

        /// <summary>A .zip upload is only playable as an MP3+G pair.</summary>
        public static bool IsPlayableZip(byte[] bytes)
        {
            try
            {
                using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                return archive.Entries.Any(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase))
                    && archive.Entries.Any(e => e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                        || e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        /// <summary>Adds the upload to the song library so the DJ can find it by title or artist.</summary>
        public static async Task AddToLibraryAsync(string path, string title, string artist)
        {
#if !MAUI
            try
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                using var context = new Lyracist.Data.LyracistDbContext();
                var song = new Lyracist.Data.Models.Song
                {
                    Title = title,
                    Artist = artist,
                    FilePath = path,
                    IsKaraoke = true,
                    KaraokeType = extension switch { ".zip" => "ZIPCDG", ".mp4" => "MP4", _ => "Original" },
                    Genre = "Originals"
                };
                context.Songs.Add(song);
                await context.SaveChangesAsync();
                await new Lyracist.Data.Services.SearchService(context).IndexSong(song);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("OriginalUploadStore.AddToLibraryAsync", ex);
            }
#else
            await Task.CompletedTask;
#endif
        }
    }
}
