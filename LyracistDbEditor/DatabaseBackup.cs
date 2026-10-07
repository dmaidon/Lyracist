// Created on Oct 7, 2026 @ 13:00:00 -> Automatic pre-operation database backups for bulk deletes and renames
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lyracist.Data;
using Lyracist.Shared;
using Microsoft.EntityFrameworkCore;

namespace LyracistDbEditor;

// Snapshots the library database before destructive bulk operations (same VACUUM INTO approach as
// Lyracist's own Settings > Backup Database), into Data\Backups, keeping only the newest few.
internal static class DatabaseBackup
{
    private const int KeepCount = 10;

    public static string BackupDirectory => Path.Combine(Globals.DataDir, "Backups");

    /// <summary>Creates a timestamped backup and returns its path.</summary>
    public static async Task<string> CreateAsync(string reason)
    {
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(BackupDirectory);
            string safeReason = new([.. reason.Where(char.IsLetterOrDigit)]);
            string path = Path.Combine(BackupDirectory, $"lyracist_{DateTime.Now:yyyyMMdd_HHmmss}_{safeReason}.db");

            using (var context = new LyracistDbContext())
            {
#pragma warning disable EF1002
                context.Database.ExecuteSqlRaw($"VACUUM INTO '{path.Replace("'", "''")}';");
#pragma warning restore EF1002
            }

            Prune();
            return path;
        });
    }

    /// <summary>
    /// Creates a backup, or if that fails asks whether to continue without one.
    /// Returns false when the caller should abort the operation.
    /// </summary>
    public static async Task<bool> EnsureBackupAsync(string reason)
    {
        try
        {
            await CreateAsync(reason);
            return true;
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", "LyracistDbEditor: Pre-operation database backup failed", ex);
            var choice = System.Windows.MessageBox.Show(
                $"A safety backup of the database could not be created:\n{ex.Message}\n\nContinue anyway?",
                "Backup Failed",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            return choice == System.Windows.MessageBoxResult.Yes;
        }
    }

    private static void Prune()
    {
        try
        {
            var old = new DirectoryInfo(BackupDirectory)
                .GetFiles("lyracist_*.db")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(KeepCount);
            foreach (var f in old)
            {
                try { f.Delete(); } catch { }
            }
        }
        catch { }
    }
}
