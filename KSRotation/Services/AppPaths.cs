// Edited on Jul 16, 2026 @ 12:00:00 -> Relocate application paths
// Last Edit: Jun 30, 2026 06:47 - Added centralized app-relative directory paths for Settings, Logs, and Reports.
using System.IO;

namespace KSRotation.Services
{
    public static class AppPaths
    {
        public static string BaseDirectoryPath => Lyracist.Shared.Globals.StartupPath;

        public static string SettingsDirectoryPath => Lyracist.Shared.Globals.KSRotationSettingsDir;

        public static string LogsDirectoryPath => Lyracist.Shared.Globals.LogDir;

        public static string ReportsDirectoryPath => Lyracist.Shared.Globals.KSRotationReportsDir;
    }
}