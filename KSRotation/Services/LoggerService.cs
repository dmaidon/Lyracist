// Edited on Jul 16, 2026 @ 12:00:00 -> Centralize logs integration
using System;
using System.IO;

namespace KSRotation.Services
{
    public static class LoggerService
    {
        public static void LogAppStart()
        {
            Lyracist.Shared.Globals.LogAppStart("KSRotation");
        }

        public static void LogError(string context, Exception ex)
        {
            Lyracist.Shared.Globals.LogError("KSRotation", context, ex);
        }

        public static void CleanupLogs()
        {
            Lyracist.Shared.Globals.PurgeOldLogs(14);
        }
    }
}
