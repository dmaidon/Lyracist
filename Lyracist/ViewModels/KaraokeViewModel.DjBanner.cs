// Created on Aug 6, 2026 @ 07:01:27 -> Split DJ Banner management out of KaraokeViewModel.cs (God-object cleanup); pure code move, no behavior change
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    public ObservableCollection<Lyracist.Shared.DjBannerItem> DjBanners { get; } = [];

    [ObservableProperty]
    private Lyracist.Shared.DjBannerItem? _selectedDjBanner;

    private void RefreshDjBanners()
    {
        DjBanners.Clear();
        foreach (var item in Lyracist.Shared.DjBannerFileManager.ScanBanners(Lyracist.Shared.Globals.DjBannersDir))
        {
            DjBanners.Add(item);
        }

        var prefs = _displayService.GetPreferences();
        if (!string.IsNullOrEmpty(prefs.SelectedDjBannerPath))
        {
            SelectedDjBanner = DjBanners.FirstOrDefault(b => b.FullPath == prefs.SelectedDjBannerPath);
        }
        else
        {
            SelectedDjBanner = DjBanners.FirstOrDefault();
        }
    }

    partial void OnSelectedDjBannerChanged(Lyracist.Shared.DjBannerItem? value)
    {
        _displayService.UpdateDjBanner(value?.FullPath ?? string.Empty);
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
                string destPath = Lyracist.Shared.DjBannerFileManager.CopyInWithDedup(dialog.FileName, Lyracist.Shared.Globals.DjBannersDir);
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

                Lyracist.Shared.DjBannerFileManager.DeleteBanner(path);
                RefreshDjBanners();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to delete banner: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void ShowDjBanner()
    {
        _displayService.ShowDjBannerWindow();
    }
}
