// Last Edit: Jun 30, 2026 08:29 - Added atomic JSON write helper to prevent file corruption on crash mid-write.
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

        File.Move(tempPath, path, overwrite: true);
    }
}
