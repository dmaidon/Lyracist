using System;
using System.IO;

namespace Lyracist.Core.Helpers
{
    public static class AppLogger
    {
        static AppLogger()
        {
            PurgeOldLogs();
        }

        public static void LogAppStart()
        {
            Lyracist.Shared.Globals.LogAppStart("Lyracist Pro");
        }

        public static void LogError(Exception ex, string? context = null)
        {
            Lyracist.Shared.Globals.LogError("Lyracist Pro", context ?? "", ex);
        }

        public static void LogError(string message, string? context = null)
        {
            Lyracist.Shared.Globals.LogError("Lyracist Pro", message, context);
        }

        public static void LogInfo(string message)
        {
            Lyracist.Shared.Globals.LogInfo("Lyracist Pro", message);
        }

        public static void PurgeOldLogs()
        {
            Lyracist.Shared.Globals.PurgeOldLogs(30);
        }

        public static void InitializeLibVlc()
        {
            try
            {
                string arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
                {
                    System.Runtime.InteropServices.Architecture.X64 => "win-x64",
                    System.Runtime.InteropServices.Architecture.X86 => "win-x86",
                    System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
                    _ => "win-x64"
                };
                string libvlcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libvlc", arch);
                LibVLCSharp.Shared.Core.Initialize(libvlcPath);
            }
            catch (Exception ex)
            {
                LogError(ex, "InitializeLibVlc");
            }
        }
    }
}