// Created on Aug 1, 2026 @ 09:42:45 -> Add DJ Banner Window view model
using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.ViewModels;

public partial class DjBannerWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private BitmapImage? _bannerImage;

    public void UpdateBanner(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            BannerImage = null;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            BannerImage = image;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load DJ banner: {ex.Message}");
            BannerImage = null;
        }
    }
}
