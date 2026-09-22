// Edited on Sep 22, 2026 @ 08:44:00 -> Add IsVideo and IsImage helper properties to DjBannerItem
namespace Lyracist.Shared;

public class DjBannerItem
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    public bool IsVideo => !string.IsNullOrEmpty(FullPath) && System.IO.Path.GetExtension(FullPath).Equals(".mp4", System.StringComparison.OrdinalIgnoreCase);
    public bool IsImage => !string.IsNullOrEmpty(FullPath) && !IsVideo;
}

