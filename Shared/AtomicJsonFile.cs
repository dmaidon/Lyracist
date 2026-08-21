// Edited on Aug 21, 2026 @ 12:30:00 -> Replace non-atomic Copy fallback with a retry loop around Move
// so a transient Windows file lock (AV/indexer scanning the freshly-written temp file) can't turn into
// a silently failed or corrupted save.
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Lyracist.Shared;

/// <summary>
/// Serializes objects to JSON using a write-to-temp-then-replace strategy so a crash or power loss
/// during a save cannot leave a truncated/corrupt file. The previous file content survives until the
/// new content is fully flushed and the rename completes.
/// </summary>
public static class AtomicJsonFile
{
    private const int MaxMoveAttempts = 5;
    private const int InitialRetryDelayMs = 25;

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

        MoveWithRetry(tempPath, path);
    }

    /// <summary>
    /// Moves <paramref name="tempPath"/> over <paramref name="path"/>, retrying with a short backoff
    /// if the destination is momentarily locked (e.g. an antivirus/indexer scan of the just-written
    /// temp file). File.Move is a rename and therefore atomic on every successful attempt; unlike the
    /// File.Copy fallback this replaces, a retry can never leave <paramref name="path"/> truncated,
    /// since the copy either happens instantly (rename) or not at all (it stays untouched).
    /// </summary>
    private static void MoveWithRetry(string tempPath, string path)
    {
        int delayMs = InitialRetryDelayMs;
        for (int attempt = 1; attempt <= MaxMoveAttempts; attempt++)
        {
            try
            {
                File.Move(tempPath, path, overwrite: true);
                return;
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < MaxMoveAttempts)
            {
                Thread.Sleep(delayMs);
                delayMs *= 2;
            }
            catch
            {
                // Final attempt failed, or a non-transient exception type: clean up the temp file
                // best-effort so a failed save doesn't leave a stray ".tmp" behind, then let the
                // caller's existing error handling take over.
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best effort cleanup.
                }

                throw;
            }
        }
    }
}
