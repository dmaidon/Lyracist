using System;
using System.IO;
using System.Text.Json;
using Lyracist.Core.Helpers;

namespace Lyracist.Services.Display;

/// <summary>
/// Persists monitor assignments and mirror state to disk (next to the exe,
/// matching AppLogger's convention) so the show layout survives app restarts.
/// </summary>
public static class DisplayPreferencesStore
{
    private static readonly string FilePath;

    static DisplayPreferencesStore()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings");
        Directory.CreateDirectory(folder);
        FilePath = Path.Combine(folder, "display.json");
    }

    public static DisplayPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var prefs = JsonSerializer.Deserialize<DisplayPreferences>(json);
                if (prefs != null) return prefs;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "DisplayPreferencesStore.Load");
        }

        return new DisplayPreferences();
    }

    public static void Save(DisplayPreferences preferences)
    {
        try
        {
            string json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "DisplayPreferencesStore.Save");
        }
    }
}
