// Edited on Aug 30, 2026 @ 08:26:00 -> Consolidated global directory structure (Settings, Data, Banners, Logs, Packs) and per-app log naming
using System;
using System.IO;

namespace Lyracist.Shared
{
    public static class Globals
    {
        public const string CompanyName = "PAROLE Software";

        public const string AuthorName = "Dennis Maidon";

        // Dynamically compute the copyright year based on current UTC year
        public static string Copyright => $"© {DateTime.UtcNow.Year} {CompanyName} - All rights reserved.";

        // Application startup folder
        public static string StartupPath =>
#if MAUI
            Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
#else
            AppDomain.CurrentDomain.BaseDirectory;
#endif

        // 1. Logs directory (in Startup Path)
        public static string LogDir => Path.Combine(StartupPath, "Logs");

        // 2. Data directory (in Startup Path)
        public static string DataDir => Path.Combine(StartupPath, "Data");

        // 3. Settings directory (in Startup Path, unified for all applications)
        public static string SettingsDir => Path.Combine(StartupPath, "Settings");

        // Backward compatibility aliases
        public static string KSRotationSettingsDir => SettingsDir;
        public static string LyracistSettingsDir => SettingsDir;
        public static string ScaryokeWheelSettingsDir => SettingsDir;

        // 4. Reports directory (in Startup Path, for KSRotation reports)
        public static string KSRotationReportsDir => Path.Combine(StartupPath, "Reports");

        // 5. Packs directory (in Startup Path, shared by trivia applications)
        public static string PacksDir => Path.Combine(StartupPath, "Packs");

        // 6. Banners directory (in Startup Path, structured per application)
        public static string BannersDir => Path.Combine(StartupPath, "Banners");

        public static string DjBannersDir => Path.Combine(BannersDir, "DJBanners");
        public static string EventBannersDir => Path.Combine(BannersDir, "EventBanners");
        public static string AnnouncementsDir => Path.Combine(BannersDir, "Announcements");
        public static string CategoryBannersDir => Path.Combine(BannersDir, "CategoryBanners");
        public static string CustomBannersDir => Path.Combine(BannersDir, "CustomBanners");

        // 7. Avatars directory (in Startup Path) - a folder for uploaded performer profile selfies.
        public static string AvatarsDir => Path.Combine(StartupPath, "Avatars");

        private static string SanitizeAppName(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName)) return "app";
            return appName.Trim().ToLowerInvariant()
                .Replace(" ", "_")
                .Replace(".", "_")
                .Replace("-", "_");
        }

        // Centralized Logging Methods with App-Distinguished Log Files
        public static void LogAppStart(string appName)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string prefix = SanitizeAppName(appName);
                string filename = $"{prefix}_app_{DateTime.Now:MMMdd}.log";
                string filepath = Path.Combine(LogDir, filename);
                string logMessage = $"\"<{appName}\" started <{DateTime.Now:MMMM d}> @ \"{DateTime.Now:HH:mm:ss}>.\"{Environment.NewLine}";
                File.AppendAllText(filepath, logMessage);
            }
            catch { }
        }

        public static void LogInfo(string appName, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string prefix = SanitizeAppName(appName);
                string filename = $"{prefix}_app_{DateTime.Now:MMMdd}.log";
                string filepath = Path.Combine(LogDir, filename);
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{appName}] [INFO] {message}{Environment.NewLine}";
                File.AppendAllText(filepath, logMessage);
            }
            catch { }
        }

        public static void LogError(string appName, string context, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string prefix = SanitizeAppName(appName);
                string filename = $"{prefix}_err_{DateTime.Now:MMMdd}.log";
                string filepath = Path.Combine(LogDir, filename);
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{appName}] [{context}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(filepath, logMessage);
            }
            catch { }
        }

        public static void LogError(string appName, string message, string? context = null)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string prefix = SanitizeAppName(appName);
                string filename = $"{prefix}_err_{DateTime.Now:MMMdd}.log";
                string filepath = Path.Combine(LogDir, filename);
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{appName}]" + (string.IsNullOrEmpty(context) ? "" : $" [{context}]") + $" {message}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(filepath, logMessage);
            }
            catch { }
        }

        public static void PurgeOldLogs(int daysToKeep)
        {
            try
            {
                if (!Directory.Exists(LogDir)) return;
                var files = Directory.GetFiles(LogDir, "*.log");
                DateTime cutoff = DateTime.Now.AddDays(-daysToKeep);
                foreach (var file in files)
                {
                    FileInfo info = new(file);
                    if (info.LastWriteTime < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }
    }
}