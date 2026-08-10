// Edited on Aug 10, 2026 @ 13:00:00 -> Suppress projection screen auto-activation on load when Debugger.IsAttached in VS IDE, but retain saved settings in the field
using System;
using System.IO;
using System.Text.Json;
using Lyracist.Core.Helpers;
using Lyracist.Shared;

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
                if (prefs != null)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        prefs.IsDjBannerActive = false;
                        prefs.IsRotationActive = false;
                        prefs.IsLyricsActive = false;
                    }
                    return prefs;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "DisplayPreferencesStore.Load");
        }

        var defaultPrefs = new DisplayPreferences();
        if (System.Diagnostics.Debugger.IsAttached)
        {
            defaultPrefs.IsDjBannerActive = false;
            defaultPrefs.IsRotationActive = false;
            defaultPrefs.IsLyricsActive = false;
        }
        return defaultPrefs;
    }

    public static void Save(DisplayPreferences preferences)
    {
        try
        {
            AtomicJsonFile.Serialize(FilePath, preferences, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "DisplayPreferencesStore.Save");
        }
    }
}
