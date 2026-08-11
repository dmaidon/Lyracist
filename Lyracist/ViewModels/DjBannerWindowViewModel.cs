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
            int decodeWidth = GetDecodeTargetWidth(path);
            if (decodeWidth > 0)
            {
                image.DecodePixelWidth = decodeWidth;
            }
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

    // Caps the decoded bitmap to the display's real needs: design resolution is 4K UHD, but if every
    // connected monitor is smaller than that, decoding at 4K would just waste memory for no visible
    // gain. Also never decodes larger than the source image's own native size (would upscale-decode
    // and waste memory on smaller user-uploaded banners).
    private static int GetDecodeTargetWidth(string path)
    {
        const int DesignMaxWidth = 3840; // 4K UHD ceiling — matches the resolution DJ banners are generated at

        int monitorCap = DesignMaxWidth;
        try
        {
            var monitors = Lyracist.Shared.MonitorEnumerator.GetMonitors();
            int largestMonitorWidth = 0;
            foreach (var m in monitors)
            {
                if (m.Width > largestMonitorWidth) largestMonitorWidth = m.Width;
            }
            if (largestMonitorWidth > 0)
            {
                monitorCap = Math.Min(DesignMaxWidth, largestMonitorWidth);
            }
        }
        catch
        {
            // Fall back to the 4K design ceiling if monitor enumeration fails
        }

        try
        {
            var decoder = BitmapDecoder.Create(
                new Uri(path),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);
            int nativeWidth = decoder.Frames.Count > 0 ? decoder.Frames[0].PixelWidth : 0;
            return nativeWidth > 0 ? Math.Min(monitorCap, nativeWidth) : monitorCap;
        }
        catch
        {
            return monitorCap;
        }
    }
}
