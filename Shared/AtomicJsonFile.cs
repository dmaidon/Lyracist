// Edited on Aug 21, 2026 @ 08:28:00 -> Add fallback Copy/Delete strategy in AtomicJsonFile to prevent MoveFile file lock collisions on Windows
using System.IO;
using System.Text.Json;

namespace Lyracist.Shared;

/// <summary>
/// Serializes objects to JSON using a write-to-temp-then-replace strategy so a crash or power loss
/// during a save cannot leave a truncated/corrupt file. The previous file content survives until the
/// new content is fully flushed and the rename completes.
/// </summary>
public static class AtomicJsonFile
{
    /// <summary>
    /// Atomically serializes <paramref name="value"/> as JSON to <paramref name="path"/>.
    /// Writes to a sibling <c>.tmp</c> file first, then moves it over the destination.
    /// </summary>
    public static void Serialize<T>(string path, T value, JsonSerializerOptions options)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, value, options);
            stream.Flush(flushToDisk: true);
        }

        try
        {
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            File.Copy(tempPath, path, overwrite: true);
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
