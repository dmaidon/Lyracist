// Edited on Sep 22, 2026 @ 08:45:00 -> Set BannerImage before BannerPath so decoded image is ready when path changes
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
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            BannerImage = null;
            BannerPath = path;
            return;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".mp4")
        {
            BannerImage = null;
            BannerPath = path;
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
            BannerPath = path;
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", "Failed to load DJ banner", ex);
            BannerImage = null;
            BannerPath = path;
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
            // Open explicitly with FileShare.ReadWrite and dispose deterministically — a Uri-based
            // BitmapDecoder with DelayCreation can hold the file open until the GC finalizes it,
            // which then made the next banner regeneration fail to overwrite this same file (silently,
            // since the caller swallows the exception) because it was still "in use".
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            // OnDemand (not OnLoad) — we only need the frame's PixelWidth header metadata here,
            // not the decoded pixel buffer, and the real capped decode happens right after this
            // returns. OnLoad would force a full-resolution pixel decode just to read a dimension.
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnDemand);
            int nativeWidth = decoder.Frames.Count > 0 ? decoder.Frames[0].PixelWidth : 0;
            return nativeWidth > 0 ? Math.Min(monitorCap, nativeWidth) : monitorCap;
        }
        catch
        {
            return monitorCap;
        }
    }
}
