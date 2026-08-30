// Edited on Aug 30, 2026 @ 08:26:00 -> Update AppPaths to map to consolidated global directory architecture
using System.IO;

namespace KSRotation.Services
{
    public static class AppPaths
    {
        public static string BaseDirectoryPath => Lyracist.Shared.Globals.StartupPath;

        public static string SettingsDirectoryPath => Lyracist.Shared.Globals.SettingsDir;

        public static string DataDirectoryPath => Lyracist.Shared.Globals.DataDir;

        public static string LogsDirectoryPath => Lyracist.Shared.Globals.LogDir;

        public static string ReportsDirectoryPath => Lyracist.Shared.Globals.KSRotationReportsDir;

        public static string BannersDirectoryPath => Lyracist.Shared.Globals.GetBannersDir("KSRotation");

        public static string DjBannersDirectoryPath => Lyracist.Shared.Globals.GetDjBannersDir("KSRotation");

        public static string EventBannersDirectoryPath => Lyracist.Shared.Globals.GetEventBannersDir("KSRotation");

        public static string AnnouncementsDirectoryPath => Lyracist.Shared.Globals.GetAnnouncementsDir("KSRotation");

        public static string PacksDirectoryPath => Lyracist.Shared.Globals.PacksDir;
    }
}