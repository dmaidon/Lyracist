// Last Edit: Jun 30, 2026 06:47 - Switched log directory resolution to shared AppPaths helper.
using System;
using System.IO;

namespace KSRotation.Services
{
    public static class LoggerService
    {
        private static string LogDirectoryPath => AppPaths.LogsDirectoryPath;

        public static void LogError(string context, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(LogDirectoryPath);
                string filename = $"err_{DateTime.Now:MMMdd}.log";
                string filepath = Path.Combine(LogDirectoryPath, filename);

                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{context}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(filepath, logMessage);
            }
            catch
            {
                // Never crash because of logging failure
            }
        }

        public static void CleanupLogs()
        {
            try
            {
                if (!Directory.Exists(LogDirectoryPath)) return;

                var files = Directory.GetFiles(LogDirectoryPath, "err_*.log");
                DateTime cutoff = DateTime.Now.AddDays(-14);
                foreach (var file in files)
                {
                    FileInfo fileInfo = new(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                // Safe log logging failure
                LogError("LoggerService.CleanupLogs", ex);
            }
        }
    }
}
