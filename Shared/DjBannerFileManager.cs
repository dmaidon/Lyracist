// Edited on Aug 9, 2026 @ 10:14:00 -> Add EnsureStandardEventBanners method to generate standard event banner graphics if missing
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
    public static readonly string[] StandardEventNames = ["Birthday", "Wedding", "Engagement", "Anniversary"];

    public static void EnsureStandardEventBanners(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string[] standardFiles = ["Birthday.png", "Wedding.png", "Engagement.png", "Anniversary.png"];
        for (int i = 0; i < standardFiles.Length; i++)
        {
            string fullPath = Path.Combine(directory, standardFiles[i]);
            if (!File.Exists(fullPath))
            {
                try
                {
                    CreateDefaultBannerPng(fullPath, StandardEventNames[i]);
                }
                catch
                {
                    // Fallback to empty file if rendering unavailable
                    File.WriteAllBytes(fullPath, []);
                }
            }
        }
    }

    private static void CreateDefaultBannerPng(string filePath, string title)
    {
        int width = 800;
        int height = 450;

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var backgroundGradient = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(25, 12, 45),
                System.Windows.Media.Color.FromRgb(75, 25, 125),
                45);

            dc.DrawRectangle(backgroundGradient, null, new System.Windows.Rect(0, 0, width, height));

            var goldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), 6);
            dc.DrawRectangle(null, goldPen, new System.Windows.Rect(15, 15, width - 30, height - 30));

            var formattedText = new System.Windows.Media.FormattedText(
                title.ToUpperInvariant(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                54,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),
                1.0);

            formattedText.TextAlignment = System.Windows.TextAlignment.Center;
            dc.DrawText(formattedText, new System.Windows.Point(width / 2.0, (height - formattedText.Height) / 2.0));
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

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
