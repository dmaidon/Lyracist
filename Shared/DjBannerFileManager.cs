// Edited on Aug 10, 2026 @ 10:52:00 -> Upgrade banner graphics to 3840x2160 4K UHD master resolution for 1:1 razor-sharp pixel rendering
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
    public static readonly string[] StandardEventNames = ["Birthday", "Wedding", "Engagement", "Anniversary", "Last Song", "Connect Instructions"];

    public static string GetStandardBannerFileName(string eventName)
    {
        if (eventName.Equals("Last Song", StringComparison.OrdinalIgnoreCase)) return "LastSong.png";
        if (eventName.Equals("Connect Instructions", StringComparison.OrdinalIgnoreCase)) return "ConnectInstructions.png";
        return $"{eventName}.png";
    }

    public static void EnsureStandardEventBanners(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

#if !MAUI
        string[] standardFiles = ["Birthday.png", "Wedding.png", "Engagement.png", "Anniversary.png", "LastSong.png", "ConnectInstructions.png"];
        for (int i = 0; i < standardFiles.Length; i++)
        {
            string fullPath = Path.Combine(directory, standardFiles[i]);
            if (!File.Exists(fullPath))
            {
                try
                {
                    if (standardFiles[i] == "ConnectInstructions.png")
                    {
                        CreateConnectInstructionsBannerPng(fullPath, string.Empty, string.Empty, string.Empty);
                    }
                    else
                    {
                        CreateDefaultBannerPng(fullPath, StandardEventNames[i]);
                    }
                }
                catch
                {
                    // Fallback to empty file if rendering unavailable
                    File.WriteAllBytes(fullPath, []);
                }
            }
        }
#endif
    }

#if !MAUI
    public static void CreateConnectInstructionsBannerPng(string filePath, string wifiSsid, string wifiPassword, string connectionUrl, int targetWidth = 0, int targetHeight = 0)
    {
        int width = targetWidth > 0 ? targetWidth : 1920;
        int height = targetHeight > 0 ? targetHeight : 1080;
        double scale = width / 1920.0;
        int qrModuleSize = (int)Math.Max(20, Math.Round(40 * scale));

        string activeSsid = string.IsNullOrWhiteSpace(wifiSsid) ? (WifiHelper.GetConnectedSsid() ?? "Wi-Fi Network") : wifiSsid;
        string pwdDisplay = string.IsNullOrWhiteSpace(wifiPassword) ? "No Password Required" : wifiPassword;
        string activeUrl = string.IsNullOrWhiteSpace(connectionUrl) ? "http://localhost:8080/request" : connectionUrl;

        System.Windows.Media.ImageSource? wifiQrSource = null;
        System.Windows.Media.ImageSource? portalQrSource = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(activeSsid))
            {
                string wifiPayload = string.IsNullOrWhiteSpace(wifiPassword)
                    ? $"WIFI:S:{activeSsid};T:nopass;;;"
                    : $"WIFI:S:{activeSsid};T:WPA;P:{wifiPassword};;";
                wifiQrSource = GenerateQrBitmap(wifiPayload, System.Windows.Media.Color.FromRgb(6, 78, 59), qrModuleSize);
            }

            portalQrSource = GenerateQrBitmap(activeUrl, System.Windows.Media.Colors.Black, qrModuleSize);
        }
        catch
        {
            // Ignore QR generation fallback if QRCoder fails
        }

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bgBrush = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(15, 23, 42),
                System.Windows.Media.Color.FromRgb(30, 41, 59),
                45);
            dc.DrawRectangle(bgBrush, null, new System.Windows.Rect(0, 0, width, height));

            var goldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), 8 * scale);
            dc.DrawRectangle(null, goldPen, new System.Windows.Rect(28 * scale, 28 * scale, width - 56 * scale, height - 56 * scale));

            var headerText = new System.Windows.Media.FormattedText(
                "HOW TO CONNECT & REQUEST SONGS",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                58 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(headerText, new System.Windows.Point(width / 2.0, 52 * scale));

            var cardBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 27, 75));
            var cardPen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(129, 140, 248)), 4 * scale);

            // Left Card: Wi-Fi
            dc.DrawRoundedRectangle(cardBg, cardPen, new System.Windows.Rect(72 * scale, 160 * scale, 850 * scale, 760 * scale), 20 * scale, 20 * scale);

            var wifiTitle = new System.Windows.Media.FormattedText(
                "1. CONNECT TO WI-FI",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                42 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 213, 255)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(wifiTitle, new System.Windows.Point((72 + 850 / 2.0) * scale, 190 * scale));

            var ssidText = new System.Windows.Media.FormattedText(
                $"Network: {activeSsid}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal),
                34 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(ssidText, new System.Windows.Point((72 + 850 / 2.0) * scale, 260 * scale));

            var pwdText = new System.Windows.Media.FormattedText(
                $"Password: {pwdDisplay}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
                30 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(pwdText, new System.Windows.Point((72 + 850 / 2.0) * scale, 315 * scale));

            if (wifiQrSource != null)
            {
                dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White), null, new System.Windows.Rect(307 * scale, 385 * scale, 380 * scale, 380 * scale));
                dc.DrawImage(wifiQrSource, new System.Windows.Rect(317 * scale, 395 * scale, 360 * scale, 360 * scale));
            }

            var wifiInstruction = new System.Windows.Media.FormattedText(
                "Scan Code to Join Network",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
                30 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(wifiInstruction, new System.Windows.Point((72 + 850 / 2.0) * scale, 845 * scale));

            // Right Card: Song Requests
            dc.DrawRoundedRectangle(cardBg, cardPen, new System.Windows.Rect(998 * scale, 160 * scale, 850 * scale, 760 * scale), 20 * scale, 20 * scale);

            var portalTitle = new System.Windows.Media.FormattedText(
                "2. REQUEST SONGS",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                42 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(233, 213, 255)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(portalTitle, new System.Windows.Point((998 + 850 / 2.0) * scale, 190 * scale));

            var urlText = new System.Windows.Media.FormattedText(
                activeUrl,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal),
                32 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(urlText, new System.Windows.Point((998 + 850 / 2.0) * scale, 260 * scale));

            if (portalQrSource != null)
            {
                dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White), null, new System.Windows.Rect(1233 * scale, 385 * scale, 380 * scale, 380 * scale));
                dc.DrawImage(portalQrSource, new System.Windows.Rect(1243 * scale, 395 * scale, 360 * scale, 360 * scale));
            }

            var portalInstruction = new System.Windows.Media.FormattedText(
                "Scan Code to Open Request Portal",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
                30 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(portalInstruction, new System.Windows.Point((998 + 850 / 2.0) * scale, 845 * scale));

            var footerText = new System.Windows.Media.FormattedText(
                "Browse Catalog • Submit Songs & Dedications • View Live Performer Rotation",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
                30 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(footerText, new System.Windows.Point(width / 2.0, 965 * scale));
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    private static System.Windows.Media.ImageSource? GenerateQrBitmap(string payload, System.Windows.Media.Color darkColor, int pixelsPerModule = 40)
    {
        using var qrGenerator = new QRCoder.QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
        byte[] bytes = qrCode.GetGraphic(pixelsPerModule, [darkColor.R, darkColor.G, darkColor.B], [255, 255, 255]);

        using var ms = new MemoryStream(bytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void CreateDefaultBannerPng(string filePath, string title)
    {
        int width = 3840;
        int height = 2160;

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var backgroundGradient = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(25, 12, 45),
                System.Windows.Media.Color.FromRgb(75, 25, 125),
                45);

            dc.DrawRectangle(backgroundGradient, null, new System.Windows.Rect(0, 0, width, height));

            var goldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), 24);
            dc.DrawRectangle(null, goldPen, new System.Windows.Rect(60, 60, width - 120, height - 120));

            var formattedText = new System.Windows.Media.FormattedText(
                title.ToUpperInvariant(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                240,
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
#endif

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
