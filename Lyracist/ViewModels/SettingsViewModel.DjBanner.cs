// Created on Aug 6, 2026 @ 07:01:27 -> Split DJ Banner management out of SettingsViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Shared;

namespace Lyracist.ViewModels;

public partial class SettingsViewModel
{
    public ObservableCollection<DjBannerItem> DjBanners { get; } = [];

    [ObservableProperty]
    private DjBannerItem? _selectedDjBanner;

    /// <summary>Selectable items for the Jumbotron banner picker: a "None" sentinel (empty
    /// path, shows the "tip your bartenders and DJ" notice) followed by every banner in <see cref="DjBanners"/>.</summary>
    public ObservableCollection<DjBannerItem> JumbotronBanners { get; } = [];

    [ObservableProperty]
    private DjBannerItem? _selectedJumbotronBanner;

    private void RefreshDjBanners()
    {
        DjBanners.Clear();
        foreach (var item in DjBannerFileManager.ScanBanners(Globals.DjBannersDir))
        {
            DjBanners.Add(item);
        }

        var prefs = _display.GetPreferences();
        if (!string.IsNullOrEmpty(prefs.SelectedDjBannerPath))
        {
            SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
        }
        else
        {
            SelectedDjBanner = DjBanners.FirstOrDefault();
        }

        JumbotronBanners.Clear();
        JumbotronBanners.Add(new DjBannerItem { FileName = "(None - show tip message)", FullPath = string.Empty });
        foreach (var item in DjBanners)
        {
            JumbotronBanners.Add(item);
        }

        // Unlike SelectedDjBanner, an empty Jumbotron selection is a valid, deliberate "None"
        // choice - only re-resolve it from saved preferences here, never default to the first banner.
        SelectedJumbotronBanner = string.IsNullOrEmpty(prefs.SelectedJumbotronBannerPath)
            ? JumbotronBanners[0]
            : (JumbotronBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedJumbotronBannerPath) ?? JumbotronBanners[0]);
    }

    partial void OnSelectedDjBannerChanged(DjBannerItem? value)
    {
        _display.UpdateDjBanner(value?.FullPath ?? string.Empty);
    }

    partial void OnSelectedJumbotronBannerChanged(DjBannerItem? value)
    {
        _display.SetJumbotronBanner(value?.FullPath ?? string.Empty);
    }

    [RelayCommand]
    private void UploadDjBanner()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Upload DJ Banner",
            Filter = "Supported Banners (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Video Files (*.mp4)|*.mp4|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string destPath = DjBannerFileManager.CopyInWithDedup(dialog.FileName, Globals.DjBannersDir);
                RefreshDjBanners();

                SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == destPath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to upload banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void DeleteDjBanner()
    {
        if (SelectedDjBanner == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete the DJ Banner '{SelectedDjBanner.FileName}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                string path = SelectedDjBanner.FullPath;
                SelectedDjBanner = null;

                DjBannerFileManager.DeleteBanner(path);
                RefreshDjBanners();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to delete banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
