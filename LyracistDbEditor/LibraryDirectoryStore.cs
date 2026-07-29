using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LyracistDbEditor;

// Persists the DB Editor's own list of scan directories independently of Lyracist's main
// settings.json, so this tool can never clobber the live app's configuration.
internal static class LibraryDirectoryStore
{
    private static readonly string FilePath = Path.Combine(Lyracist.Shared.Globals.LyracistSettingsDir, "dbeditor_library_directories.json");
    private static readonly object Lock = new();

    public static List<string> Load()
    {
        lock (Lock)
        {
            try
            {
                if (!File.Exists(FilePath)) return [];
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    public static void Save(List<string> directories)
    {
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Lyracist.Shared.Globals.LyracistSettingsDir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(directories));
            }
            catch
            {
                // Non-critical — worst case the list doesn't survive a restart.
            }
        }
    }
}
