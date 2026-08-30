// Edited on Aug 30, 2026 @ 08:26:00 -> Update settings path to Settings/dbeditor_settings.json with legacy fallback migration
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Lyracist.Shared;

namespace LyracistDbEditor;

// Persists the DB Editor's own list of scan directories in Settings/dbeditor_settings.json
internal static class LibraryDirectoryStore
{
    private static readonly string FilePath = Path.Combine(Globals.SettingsDir, "dbeditor_settings.json");
    private static readonly object Lock = new();

    public static List<string> Load()
    {
        lock (Lock)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    // Fallback migration: Check legacy path
                    string legacyPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Lyracist",
                        "dbeditor_library_directories.json"
                    );
                    if (File.Exists(legacyPath))
                    {
                        Directory.CreateDirectory(Globals.SettingsDir);
                        File.Copy(legacyPath, FilePath, true);
                    }
                }

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
