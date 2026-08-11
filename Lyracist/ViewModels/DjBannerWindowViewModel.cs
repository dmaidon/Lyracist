// Edited on Aug 2, 2026 @ 10:05:00 -> Add BannerPath property to support video banner playback in code-behind
using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lyracist.ViewModels;

public partial class DjBannerWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private BitmapImage? _bannerImage;

    [ObservableProperty]
    private string? _bannerPath;

    public void UpdateBanner(string? path)
    {
        BannerPath = path;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            BannerImage = null;
            return;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".mp4")
        {
            BannerImage = null;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
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
