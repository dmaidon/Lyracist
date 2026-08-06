// Created on Aug 6, 2026 @ 07:01:27 -> Split music library + database management settings out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Core.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    // ─── Music Library ─────────────────────────────────────────────────

    public ObservableCollection<string> LibraryDirectories { get; } = [];

    [ObservableProperty]
    private string? _selectedLibraryDirectory;

    [ObservableProperty]
    private string _libraryStatus = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    private void RefreshLibraryDirectories()
    {
        LibraryDirectories.Clear();
        foreach (var dir in Core.Helpers.AppSettings.LibraryDirectories)
            LibraryDirectories.Add(dir);
    }

    private void RefreshLibraryStatus()
    {
        int count = _library.GetSongCount();
        LibraryStatus = count == 0
            ? "No songs scanned yet — add a folder and scan."
            : $"{count:N0} songs in library.";
    }

    [RelayCommand]
    private void AddLibraryDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Music Library Folder to Scan"
        };
        if (dialog.ShowDialog() != true) return;

        IsScanning = true;
        // ScanDirectory persists to AppSettings internally.
        _library.ScanDirectory(dialog.FolderName);
        RefreshLibraryDirectories();
        LibraryStatus = "Scanning…";
    }

    [RelayCommand]
    private void RemoveLibraryDirectory()
    {
        if (SelectedLibraryDirectory == null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove '{SelectedLibraryDirectory}' from the scan list? Songs already indexed from this folder will also be removed from the library.",
            "Confirm Remove Directory",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        _library.RemoveSongsUnderDirectory(SelectedLibraryDirectory);
        Core.Helpers.AppSettings.RemoveLibraryDirectory(SelectedLibraryDirectory);
        SelectedLibraryDirectory = null;
        RefreshLibraryDirectories();
        RefreshLibraryStatus();
    }

    [RelayCommand]
    private void RescanLibrary()
    {
        if (LibraryDirectories.Count == 0) return;
        IsScanning = true;
        LibraryStatus = "Scanning…";
        _library.RescanAllDirectories();
    }

    [RelayCommand]
    private void LoadDbEditor()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, "LyracistDbEditor.exe");

            if (!File.Exists(exePath))
            {
                exePath = "LyracistDbEditor.exe";
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = baseDir
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Load LyracistDbEditor");
            System.Windows.MessageBox.Show($"Failed to launch LyracistDbEditor: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ScanSelectedDirectory()
    {
        if (SelectedLibraryDirectory == null) return;
        IsScanning = true;
        LibraryStatus = "Scanning…";
        _library.ScanDirectory(SelectedLibraryDirectory);
    }

    [RelayCommand]
    private void ClearDatabase()
    {
        var confirm = System.Windows.MessageBox.Show(
            "Are you sure you want to clear the entire database? This will remove all songs, playlists, performer history, and active rotation queue.",
            "Confirm Clear Database",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using (var db = new Lyracist.Data.LyracistDbContext())
            {
                db.Database.EnsureDeleted();
                // Use Migrate (not EnsureCreated) so __EFMigrationsHistory is populated
                // correctly — otherwise the next app startup's Migrate() call sees no
                // history and tries to re-apply migrations against tables that already exist.
                db.Database.Migrate();
            }

            // Clear lists in memory
            LibraryDirectories.Clear();
            RefreshLibraryDirectories();
            RefreshLibraryStatus();
            _rotation.ClearRotationQueue();

            System.Windows.MessageBox.Show("Database cleared successfully.", "Database Cleared", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Clear Database");
            System.Windows.MessageBox.Show($"Failed to clear database: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void BackupDatabase()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Backup Lyracist Database",
            FileName = $"lyracist_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        string selectedPath = dialog.FileName;

        try
        {
            if (System.IO.File.Exists(selectedPath))
            {
                System.IO.File.Delete(selectedPath);
            }

            using var db = new Lyracist.Data.LyracistDbContext();
#pragma warning disable EF1002
            db.Database.ExecuteSqlRaw($"VACUUM INTO '{selectedPath.Replace("'", "''")}';");
#pragma warning restore EF1002

            System.Windows.MessageBox.Show(
                $"Database backup created successfully at:{Environment.NewLine}{selectedPath}",
                "Backup Successful",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database Backup");
            System.Windows.MessageBox.Show(
                $"Failed to backup database: {ex.Message}",
                "Backup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore Lyracist Database from Backup",
            Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        string selectedPath = dialog.FileName;

        var confirm = System.Windows.MessageBox.Show(
            "Restoring the database will overwrite all current settings, performers, playlists, and history. " +
            "The application will shutdown to complete the restore. Do you want to proceed?",
            "Confirm Database Restore",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using (var db = new Lyracist.Data.LyracistDbContext())
            {
                db.Database.CloseConnection();
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "Data", "lyracist.db");

            System.IO.File.Copy(selectedPath, dbPath, overwrite: true);

            string walPath = dbPath + "-wal";
            string shmPath = dbPath + "-shm";
            if (System.IO.File.Exists(walPath)) System.IO.File.Delete(walPath);
            if (System.IO.File.Exists(shmPath)) System.IO.File.Delete(shmPath);

            System.Windows.MessageBox.Show(
                "Database restored successfully. The application will now close. Please restart Lyracist.",
                "Restore Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "Database Restore");
            System.Windows.MessageBox.Show(
                $"Failed to restore database: {ex.Message}",
                "Restore Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}
