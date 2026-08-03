// Last Edit: Jun 30, 2026 06:47 - Switched Settings directory resolution to shared AppPaths helper.
using Lyracist.Shared;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KSRotation.Services
{
    /// <summary>
    /// Shared persistence helper for named string lists (venues, DJs, etc.).
    /// All logic that was duplicated between VenueService and DjService lives here.
    /// </summary>
    public static class StringListStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = AppJsonContext.Default.Options;

        private static string DirectoryPath => AppPaths.SettingsDirectoryPath;

        private static string FilePath(string fileName) => Path.Combine(DirectoryPath, fileName);

        /// <summary>Loads a string list from <paramref name="fileName"/> in the Settings directory.
        /// Returns a single-element list containing <paramref name="defaultValue"/> when the file is absent or empty.</summary>
        public static List<string> Load(string fileName, string defaultValue)
        {
            string path = FilePath(fileName);
            if (!File.Exists(path))
                return [defaultValue];

            try
            {
                using FileStream stream = File.OpenRead(path);
                List<string>? items = JsonSerializer.Deserialize<List<string>>(stream, SerializerOptions);
                return items is { Count: > 0 } ? items : [defaultValue];
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"The file '{path}' contains invalid JSON.", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to read '{path}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Access denied reading '{path}'.", ex);
            }
        }

        /// <summary>Saves <paramref name="items"/> to <paramref name="fileName"/> in the Settings directory.</summary>
        public static void Save(IEnumerable<string> items, string fileName)
        {
            ArgumentNullException.ThrowIfNull(items);

            try
            {
                // Materialize to List<string> so the source-generated context (which registers List<string>,
                // not IEnumerable<string>) can resolve the metadata regardless of the caller's static type.
                List<string> list = items as List<string> ?? [.. items];
                AtomicJsonFile.Serialize(FilePath(fileName), list, SerializerOptions);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to save '{fileName}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Access denied saving '{fileName}'.", ex);
            }
        }
    }
}
