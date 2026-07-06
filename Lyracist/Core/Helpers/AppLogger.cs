using System;
using System.IO;

namespace Lyracist.Core.Helpers;

public static class AppLogger
{
    private static readonly string LogDir;

    static AppLogger()
    {
        string startupFolder = AppDomain.CurrentDomain.BaseDirectory;
        LogDir = Path.Combine(startupFolder, "Logs");
        Directory.CreateDirectory(LogDir);
        PurgeOldLogs();
    }

    public static void LogAppStart()
    {
        string path = Path.Combine(LogDir, "app.log");
        string entry = $"{new string('-', 60)}{Environment.NewLine}App started {DateTime.Now:MMMM d} @ {DateTime.Now:HH:mm:ss}.{Environment.NewLine}{Environment.NewLine}";

        try
        {
            File.AppendAllText(path, entry);
        }
        catch { }

        try
        {
            string errFileName = $"err_{DateTime.Now:MMMdd}.log";
            string errPath = Path.Combine(LogDir, errFileName);
            string errEntry = $"{new string('=', 60)}{Environment.NewLine}App started {DateTime.Now:MMMM d} @ {DateTime.Now:HH:mm:ss}.{Environment.NewLine}{new string('=', 60)}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(errPath, errEntry);
        }
        catch { }
    }

    public static void LogError(Exception ex, string? context = null)
    {
        string fileName = $"err_{DateTime.Now:MMMdd}.log";
        string path = Path.Combine(LogDir, fileName);

        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";
        if (!string.IsNullOrEmpty(context))
            entry += $" [{context}]";
        entry += $" {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";

        try
        {
            File.AppendAllText(path, entry);
        }
        catch
        {
            // Swallow — can't log a logging failure
        }
    }

    public static void LogError(string message, string? context = null)
    {
        string fileName = $"err_{DateTime.Now:MMMdd}.log";
        string path = Path.Combine(LogDir, fileName);

        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]";
        if (!string.IsNullOrEmpty(context))
            entry += $" [{context}]";
        entry += $" {message}{Environment.NewLine}{Environment.NewLine}";

        try
        {
            File.AppendAllText(path, entry);
        }
        catch { }
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

    private static void PurgeOldLogs()
    {
        try
        {
            DateTime cutoff = DateTime.Now.AddDays(-30);
            foreach (string file in Directory.GetFiles(LogDir, "err_*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }
}