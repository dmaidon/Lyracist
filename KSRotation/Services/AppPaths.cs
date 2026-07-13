// Last Edit: Jun 30, 2026 06:47 - Added centralized app-relative directory paths for Settings, Logs, and Reports.
using System.IO;

namespace KSRotation.Services
{
    public static class AppPaths
    {
        public static string BaseDirectoryPath =>
#if MAUI
            Microsoft.Maui.Storage.FileSystem.AppDataDirectory;
#else
            AppContext.BaseDirectory;
#endif

        public static string SettingsDirectoryPath => Path.Combine(BaseDirectoryPath, "Settings");

        public static string LogsDirectoryPath => Path.Combine(BaseDirectoryPath, "Logs");

        public static string ReportsDirectoryPath => Path.Combine(BaseDirectoryPath, "Reports");
    }
}