using System;
using System.Collections.Generic;
using System.IO;

namespace Lyracist.Shared;

/// <summary>
/// Scans, uploads, and deletes DJ banner files in the shared banners folder
/// (<see cref="Globals.DjBannersDir"/>) used by both KSRotation and Lyracist.
/// </summary>
public static class DjBannerFileManager
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".mp4"];

    public static IReadOnlyList<DjBannerItem> ScanBanners(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var items = new List<DjBannerItem>();
        foreach (string file in Directory.GetFiles(directory))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (Array.IndexOf(SupportedExtensions, ext) >= 0)
            {
                items.Add(new DjBannerItem
                {
                    FileName = Path.GetFileName(file),
                    FullPath = file
                });
            }
        }

        return items;
    }

    /// <summary>Copies a file into destDirectory, appending "_N" before the extension on a name collision.</summary>
    public static string CopyInWithDedup(string sourceFilePath, string destDirectory)
    {
        Directory.CreateDirectory(destDirectory);

        string dest = Path.Combine(destDirectory, Path.GetFileName(sourceFilePath));
        int counter = 1;
        while (File.Exists(dest))
        {
            string name = Path.GetFileNameWithoutExtension(sourceFilePath);
            string ext = Path.GetExtension(sourceFilePath);
            dest = Path.Combine(destDirectory, $"{name}_{counter++}{ext}");
        }

        File.Copy(sourceFilePath, dest);
        return dest;
    }

    public static void DeleteBanner(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
