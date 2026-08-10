// Created on Aug 10, 2026 @ 12:42:00 -> Add WifiPasswordStore for persistent SSID password management
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
                    Directory.CreateDirectory(Globals.DataDir);
                    string json = JsonSerializer.Serialize(_passwords, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(FilePath, json);
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
