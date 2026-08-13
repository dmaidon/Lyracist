// Edited on Aug 13, 2026 @ 13:46:21 -> Use AtomicJsonFile.Serialize instead of File.WriteAllText to prevent corruption on crash mid-write
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lyracist.Shared
{
    public static class WifiPasswordStore
    {
        private static readonly object _lock = new();
        private static Dictionary<string, string> _passwords = new(StringComparer.OrdinalIgnoreCase);
        private static string FilePath => Path.Combine(Globals.DataDir, "wifi_passwords.json");

        static WifiPasswordStore()
        {
            Load();
        }

        public static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        string json = File.ReadAllText(FilePath);
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (loaded != null)
                        {
                            _passwords = new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
                        }
                    }
                }
                catch
                {
                    _passwords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    // Write-to-temp-then-rename so a crash/power-loss mid-write can't leave
                    // wifi_passwords.json truncated and silently wipe every saved venue password.
                    AtomicJsonFile.Serialize(FilePath, _passwords, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    // Ignore save errors
                }
            }
        }

        public static string GetPasswordForSsid(string ssid)
        {
            if (string.IsNullOrWhiteSpace(ssid)) return string.Empty;
            lock (_lock)
            {
                return _passwords.TryGetValue(ssid.Trim(), out string? pwd) ? pwd : string.Empty;
            }
        }

        public static void SetPasswordForSsid(string ssid, string password)
        {
            if (string.IsNullOrWhiteSpace(ssid)) return;
            lock (_lock)
            {
                string cleanSsid = ssid.Trim();
                if (string.IsNullOrEmpty(password))
                {
                    _passwords.Remove(cleanSsid);
                }
                else
                {
                    _passwords[cleanSsid] = password;
                }
                Save();
            }
        }
    }
}
