// Created on Jul 16, 2026 @ 12:00:00 -> Shared global path settings
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

        // 3. Settings directory (in Startup Path, for KSRotation app settings)
        public static string KSRotationSettingsDir => Path.Combine(StartupPath, "Settings");

        // 4. Reports directory (in Startup Path, for KSRotation reports)
        public static string KSRotationReportsDir => Path.Combine(StartupPath, "Reports");

        // 5. Lyracist app settings directory (in User AppData)
        public static string LyracistSettingsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyracist");

        // 6. ScaryokeWheel settings directory (in User LocalAppData)
        public static string ScaryokeWheelSettingsDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ParoleSoftware",
            "ScaryokeWheel"
        );

        // Centralized Logging Methods
        public static void LogAppStart(string appName)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string filename = $"app_{DateTime.Now:MMMdd}.log";
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
                string filename = $"app_{DateTime.Now:MMMdd}.log";
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
                string filename = $"err_{DateTime.Now:MMMdd}.log";
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
                string filename = $"err_{DateTime.Now:MMMdd}.log";
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