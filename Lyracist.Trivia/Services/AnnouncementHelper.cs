// Edited on Aug 30, 2026 @ 08:26:00 -> Update announcements directory to consolidated Banners/LyracistTrivia/Announcements
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Lyracist.Shared;

namespace Lyracist.Trivia.Services;

/// <summary>
/// Scans the LyracistTrivia Announcements folder for pre-game advertisement banners the
/// game master can choose to display on the TV screen before the connect instructions screen.
/// Deliberately lives only in this project (not Lyracist.Trivia.Core) since the feature is
/// exclusive to the standalone Lyracist.Trivia app.
/// </summary>
public static class AnnouncementHelper
{
    public record AnnouncementImage(string FileName, string FullPath)
    {
        /// <summary>
        /// A readable label derived from the filename, e.g. "announce_cartoon.png" -> "Cartoon".
        /// </summary>
        public string DisplayName => BuildDisplayName(FileName);

        private static string BuildDisplayName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            if (name.StartsWith("announce_", StringComparison.OrdinalIgnoreCase))
            {
                name = name["announce_".Length..];
            }
            name = name.Replace('_', ' ').Replace('-', ' ').Trim();
            return name.Length == 0
                ? fileName
                : char.ToUpperInvariant(name[0]) + name[1..];
        }
    }

    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg"];

    public static string GetAnnouncementsDirectory()
    {
        string dir = Path.Combine(Globals.StartupPath, "Banners", "LyracistTrivia", "Announcements");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static List<AnnouncementImage> GetAvailableAnnouncements()
    {
        string dir = GetAnnouncementsDirectory();
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .Select(f => new AnnouncementImage(Path.GetFileName(f), f))
            .ToList();
    }

    private static string GetSelectionPath() =>
        Path.Combine(Globals.SettingsDir, "lyracist_trivia_selected_announcement.json");

    /// <summary>
    /// Remembers the game master's last pick across app restarts. Stored inside the
    /// Announcements folder itself, not the shared TriviaData settings file, since this is a
    /// Lyracist.Trivia-only preference.
    /// </summary>
    public static string? LoadSelectedFileName()
    {
        try
        {
            string path = GetSelectionPath();
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<SelectionPayload>(json);
            return doc?.SelectedFileName;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveSelectedFileName(string? fileName)
    {
        try
        {
            string json = JsonSerializer.Serialize(new SelectionPayload { SelectedFileName = fileName });
            File.WriteAllText(GetSelectionPath(), json);
        }
        catch
        {
            // Non-critical - worst case the game master just re-picks it next launch.
        }
    }

    private class SelectionPayload
    {
        public string? SelectedFileName { get; set; }
    }
}
