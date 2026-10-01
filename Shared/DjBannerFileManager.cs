// Edited on Oct 1, 2026 @ 07:46:15 -> Use WifiHelper.EscapeWifiQrValue for standard Wi-Fi QR escaping
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
    private static readonly object _bannerInitLock = new();
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".mp4"];
    // "Connect Instructions" is deliberately excluded — it's an auto-generated Wi-Fi/QR instructional
    // graphic (see ConnectInstructions.png handling below), not a selectable party/event banner, so it
    // must not appear in the DJ's Special Event Banner picker that this list seeds.
    public static readonly string[] StandardEventNames = ["Birthday", "Wedding", "Engagement", "Anniversary", "Last Song"];

    public static string GetStandardBannerFileName(string eventName)
    {
        if (eventName.Equals("Last Song", StringComparison.OrdinalIgnoreCase)) return "LastSong.png";
        if (eventName.Equals("Connect Instructions", StringComparison.OrdinalIgnoreCase)) return "ConnectInstructions.png";
        return $"{eventName}.png";
    }

    public static void EnsureStandardEventBanners(string directory)
    {
        lock (_bannerInitLock)
        {
            if (!Directory.Exists(directory))
            {
                try { Directory.CreateDirectory(directory); } catch { return; }
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
                        else if (standardFiles[i] == "LastSong.png")
                        {
                            CreateLastSongBannerPng(fullPath);
                        }
                        else
                        {
                            CreateDefaultBannerPng(fullPath, StandardEventNames[i]);
                        }
                    }
                    catch
                    {
                        // Fallback to empty file if rendering unavailable or contention occurs
                        try
                        {
                            File.WriteAllBytes(fullPath, []);
                        }
                        catch
                        {
                            // Ignore concurrent file access or permission errors
                        }
                    }
                }
            }
#endif
        }
    }

#if !MAUI
    public static void CreateConnectInstructionsBannerPng(string filePath, string wifiSsid, string wifiPassword, string connectionUrl, int targetWidth = 0, int targetHeight = 0)
    {
        int width = targetWidth > 0 ? targetWidth : 1920;
        int height = targetHeight > 0 ? targetHeight : 1080;
        double scale = width / 1920.0;
        int qrModuleSize = (int)Math.Max(20, Math.Round(40 * scale));

        string? detectedSsid = string.IsNullOrWhiteSpace(wifiSsid) ? WifiHelper.GetConnectedSsid() : wifiSsid;
        bool hasKnownSsid = !string.IsNullOrWhiteSpace(detectedSsid);
        string activeSsid = hasKnownSsid ? detectedSsid! : "Ask DJ for Wi-Fi Name";
        string pwdDisplay = string.IsNullOrWhiteSpace(wifiPassword) ? "No Password Required" : wifiPassword;
        string activeUrl = string.IsNullOrWhiteSpace(connectionUrl) ? "http://localhost:8080/request" : connectionUrl;

        System.Windows.Media.ImageSource? wifiQrSource = null;
        System.Windows.Media.ImageSource? portalQrSource = null;

        try
        {
            // Only generate a Wi-Fi QR when we actually know the SSID — otherwise the code would
            // encode a fake/placeholder network name that fails for anyone who scans it.
            if (hasKnownSsid)
            {
                string wifiPayload = string.IsNullOrWhiteSpace(wifiPassword)
                    ? $"WIFI:S:{EscapeWifiQrValue(activeSsid)};T:nopass;;;"
                    : $"WIFI:S:{EscapeWifiQrValue(activeSsid)};T:WPA;P:{EscapeWifiQrValue(wifiPassword)};;";
                wifiQrSource = GenerateQrBitmap(wifiPayload, System.Windows.Media.Colors.Black, qrModuleSize);
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
            dc.DrawRectangle(null, goldPen, new System.Windows.Rect(28 * scale, 28 * scale, width - (56 * scale), height - (56 * scale)));

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
            dc.DrawText(wifiTitle, new System.Windows.Point((72 + (850 / 2.0)) * scale, 190 * scale));

            var ssidText = new System.Windows.Media.FormattedText(
                $"Network: {activeSsid}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal),
                34 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(ssidText, new System.Windows.Point((72 + (850 / 2.0)) * scale, 260 * scale));

            var pwdText = new System.Windows.Media.FormattedText(
                $"Password: {pwdDisplay}",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
                30 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(pwdText, new System.Windows.Point((72 + (850 / 2.0)) * scale, 315 * scale));

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
            dc.DrawText(wifiInstruction, new System.Windows.Point((72 + (850 / 2.0)) * scale, 845 * scale));

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
            dc.DrawText(portalTitle, new System.Windows.Point((998 + (850 / 2.0)) * scale, 190 * scale));

            var urlText = new System.Windows.Media.FormattedText(
                activeUrl,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal),
                32 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(urlText, new System.Windows.Point((998 + (850 / 2.0)) * scale, 260 * scale));

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
            dc.DrawText(portalInstruction, new System.Windows.Point((998 + (850 / 2.0)) * scale, 845 * scale));

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

            var copyrightText = new System.Windows.Media.FormattedText(
                Globals.Copyright,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal),
                14 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
                1.0)
            { TextAlignment = System.Windows.TextAlignment.Center };
            dc.DrawText(copyrightText, new System.Windows.Point(width / 2.0, height - (70 * scale)));
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        encoder.Save(fs);
    }

    private static string EscapeWifiQrValue(string value) => WifiHelper.EscapeWifiQrValue(value);

    private static System.Windows.Media.Imaging.BitmapImage? GenerateQrBitmap(string payload, System.Windows.Media.Color darkColor, int pixelsPerModule = 40)
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
        const int width = 3840;
        const int height = 2160;

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

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        encoder.Save(fs);
    }

    public static void CreatePersonalizedBirthdayBannerPng(string filePath, string performerName, int targetWidth = 1920, int targetHeight = 1080)
    {
        int width = targetWidth > 0 ? targetWidth : 1920;
        int height = targetHeight > 0 ? targetHeight : 1080;
        double scale = width / 1920.0;

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 1. Festive Background Gradient
            var backgroundGradient = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(18, 8, 38),
                System.Windows.Media.Color.FromRgb(70, 22, 115),
                45);
            dc.DrawRectangle(backgroundGradient, null, new System.Windows.Rect(0, 0, width, height));

            // Radial Glow in Center
            var centerGlow = new System.Windows.Media.RadialGradientBrush(
                System.Windows.Media.Color.FromArgb(120, 255, 215, 0),
                System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            dc.DrawEllipse(centerGlow, null, new System.Windows.Point(width / 2.0, height / 2.0), width * 0.45, height * 0.45);

            // 2. Elegant Outer Gold Border
            var goldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), 10 * scale);
            var innerGoldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 215, 0)), 3 * scale);
            dc.DrawRectangle(null, goldPen, new System.Windows.Rect(30 * scale, 30 * scale, width - (60 * scale), height - (60 * scale)));
            dc.DrawRectangle(null, innerGoldPen, new System.Windows.Rect(45 * scale, 45 * scale, width - (90 * scale), height - (90 * scale)));

            // 3. Vector Fireworks / Starbursts (Top-Left & Top-Right)
            DrawFireworks(dc, new System.Windows.Point(220 * scale, 220 * scale), scale);
            DrawFireworks(dc, new System.Windows.Point(width - (220 * scale), 220 * scale), scale);

            // 4. Vector Balloons (Left & Right margins)
            DrawBalloons(dc, new System.Windows.Point(140 * scale, height * 0.65), scale, System.Windows.Media.Color.FromRgb(236, 72, 153)); // Pink/Magenta
            DrawBalloons(dc, new System.Windows.Point(230 * scale, height * 0.75), scale * 0.85, System.Windows.Media.Color.FromRgb(6, 182, 212)); // Cyan
            DrawBalloons(dc, new System.Windows.Point(width - (140 * scale), height * 0.65), scale, System.Windows.Media.Color.FromRgb(245, 158, 11)); // Gold
            DrawBalloons(dc, new System.Windows.Point(width - (230 * scale), height * 0.75), scale * 0.85, System.Windows.Media.Color.FromRgb(168, 85, 247)); // Purple

            // 5. Floating Music Notes
            DrawMusicNote(dc, new System.Windows.Point(340 * scale, height * 0.35), scale * 1.2, System.Windows.Media.Color.FromRgb(255, 215, 0));
            DrawMusicNote(dc, new System.Windows.Point(width - (340 * scale), height * 0.38), scale * 1.1, System.Windows.Media.Color.FromRgb(6, 182, 212));
            DrawMusicNote(dc, new System.Windows.Point(400 * scale, height * 0.82), scale * 0.9, System.Windows.Media.Color.FromRgb(236, 72, 153));
            DrawMusicNote(dc, new System.Windows.Point(width - (400 * scale), height * 0.80), scale * 1.0, System.Windows.Media.Color.FromRgb(255, 215, 0));

            // 6. Header Text: "HAPPY BIRTHDAY!"
            var headerText = new System.Windows.Media.FormattedText(
                "HAPPY BIRTHDAY!",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.ExtraBold, System.Windows.FontStretches.Normal),
                100 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 220, 100)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(headerText, new System.Windows.Point(width / 2.0, 160 * scale));

            // 7. Performer Name (e.g., "Brenda") - Dynamic Font Size Calculation
            string displayName = string.IsNullOrWhiteSpace(performerName) ? "VIP" : performerName.Trim();
            double nameFontSize = 170 * scale;
            if (displayName.Length > 12) nameFontSize = 120 * scale;
            if (displayName.Length > 18) nameFontSize = 90 * scale;

            // Name Shadow / 3D Effect
            var nameShadowText = new System.Windows.Media.FormattedText(
                displayName.ToUpperInvariant(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Black, System.Windows.FontStretches.Normal),
                nameFontSize,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(nameShadowText, new System.Windows.Point((width / 2.0) + (6 * scale), ((height / 2.0) - (nameShadowText.Height / 2.0)) + (6 * scale)));

            // Name Foreground Text (Gold Gradient / Glow)
            var nameText = new System.Windows.Media.FormattedText(
                displayName.ToUpperInvariant(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Black, System.Windows.FontStretches.Normal),
                nameFontSize,
                new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(255, 235, 120),
                    System.Windows.Media.Color.FromRgb(245, 158, 11), 90),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(nameText, new System.Windows.Point(width / 2.0, (height / 2.0) - (nameText.Height / 2.0)));

            // 8. Footer Badge: "VIP KARAOKE CELEBRATION"
            var footerText = new System.Windows.Media.FormattedText(
                "★ VIP KARAOKE CELEBRATION ★",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                48 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 182, 212)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(footerText, new System.Windows.Point(width / 2.0, height - (200 * scale)));
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        encoder.Save(fs);
    }

    private static void DrawFireworks(System.Windows.Media.DrawingContext dc, System.Windows.Point center, double scale)
    {
        var penGold = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)), 3 * scale);
        var penCyan = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 182, 212)), 2 * scale);
        var penPink = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 72, 153)), 2 * scale);

        const int count = 12;
        double radius = 90 * scale;
        for (int i = 0; i < count; i++)
        {
            double angle = i * (360.0 / count) * (Math.PI / 180.0);
            double innerR = 25 * scale;
            double outerR = radius;

            var p1 = new System.Windows.Point(center.X + (Math.Cos(angle) * innerR), center.Y + (Math.Sin(angle) * innerR));
            var p2 = new System.Windows.Point(center.X + (Math.Cos(angle) * outerR), center.Y + (Math.Sin(angle) * outerR));

            var pen = (i % 3 == 0) ? penGold : (i % 3 == 1) ? penCyan : penPink;
            dc.DrawLine(pen, p1, p2);

            var sparkBrush = (i % 2 == 0) ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.White;
            dc.DrawEllipse(sparkBrush, null, p2, 4 * scale, 4 * scale);
        }
    }

    private static void DrawBalloons(System.Windows.Media.DrawingContext dc, System.Windows.Point basePt, double scale, System.Windows.Media.Color balloonColor)
    {
        double rx = 45 * scale;
        double ry = 58 * scale;
        var center = new System.Windows.Point(basePt.X, basePt.Y - ry);

        var stringPen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 255, 255)), 2 * scale);
        dc.DrawLine(stringPen, basePt, new System.Windows.Point(basePt.X + (15 * scale), basePt.Y + (110 * scale)));

        var balloonBrush = new System.Windows.Media.RadialGradientBrush(
            System.Windows.Media.Color.FromRgb((byte)Math.Min(255, balloonColor.R + 60), (byte)Math.Min(255, balloonColor.G + 60), (byte)Math.Min(255, balloonColor.B + 60)),
            balloonColor)
        {
            GradientOrigin = new System.Windows.Point(0.3, 0.3)
        };

        dc.DrawEllipse(balloonBrush, null, center, rx, ry);
        dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 255, 255)), null,
            new System.Windows.Point(center.X - (rx * 0.35), center.Y - (ry * 0.35)), rx * 0.25, ry * 0.18);
    }

    private static void DrawMusicNote(System.Windows.Media.DrawingContext dc, System.Windows.Point center, double scale, System.Windows.Media.Color color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(color);
        var pen = new System.Windows.Media.Pen(brush, 4 * scale);

        var headCenter = new System.Windows.Point(center.X, center.Y + (20 * scale));
        dc.DrawEllipse(brush, null, headCenter, 14 * scale, 10 * scale);

        var stemTop = new System.Windows.Point(center.X + (12 * scale), center.Y - (25 * scale));
        var stemBottom = new System.Windows.Point(center.X + (12 * scale), center.Y + (20 * scale));
        dc.DrawLine(pen, stemBottom, stemTop);
        dc.DrawLine(pen, stemTop, new System.Windows.Point(center.X + (26 * scale), center.Y - (12 * scale)));
    }

    public static void CreateLastSongBannerPng(string filePath, int targetWidth = 1920, int targetHeight = 1080)
    {
        int width = targetWidth > 0 ? targetWidth : 1920;
        int height = targetHeight > 0 ? targetHeight : 1080;
        double scale = width / 1920.0;

        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 1. Deep Midnight Velvet Gradient
            var backgroundGradient = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(12, 6, 26),
                System.Windows.Media.Color.FromRgb(45, 14, 80),
                45);
            dc.DrawRectangle(backgroundGradient, null, new System.Windows.Rect(0, 0, width, height));

            // Radial Glow in Center (Gold / Amber Warmth)
            var centerGlow = new System.Windows.Media.RadialGradientBrush(
                System.Windows.Media.Color.FromArgb(130, 255, 200, 50),
                System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
            dc.DrawEllipse(centerGlow, null, new System.Windows.Point(width / 2.0, height / 2.0), width * 0.5, height * 0.5);

            // 2. Elegant Dual Gold Borders
            var goldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), 10 * scale);
            var innerGoldPen = new System.Windows.Media.Pen(
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 255, 215, 0)), 3 * scale);
            dc.DrawRectangle(null, goldPen, new System.Windows.Rect(30 * scale, 30 * scale, width - (60 * scale), height - (60 * scale)));
            dc.DrawRectangle(null, innerGoldPen, new System.Windows.Rect(45 * scale, 45 * scale, width - (90 * scale), height - (90 * scale)));

            // 3. Ornate Fireworks / Starbursts (All 4 corners)
            DrawFireworks(dc, new System.Windows.Point(220 * scale, 220 * scale), scale);
            DrawFireworks(dc, new System.Windows.Point(width - (220 * scale), 220 * scale), scale);
            DrawFireworks(dc, new System.Windows.Point(220 * scale, height - (220 * scale)), scale * 0.85);
            DrawFireworks(dc, new System.Windows.Point(width - (220 * scale), height - (220 * scale)), scale * 0.85);

            // 4. Floating Music Notes
            DrawMusicNote(dc, new System.Windows.Point(360 * scale, height * 0.38), scale * 1.3, System.Windows.Media.Color.FromRgb(255, 215, 0));
            DrawMusicNote(dc, new System.Windows.Point(width - (360 * scale), height * 0.38), scale * 1.3, System.Windows.Media.Color.FromRgb(255, 215, 0));
            DrawMusicNote(dc, new System.Windows.Point(420 * scale, height * 0.78), scale * 1.0, System.Windows.Media.Color.FromRgb(6, 182, 212));
            DrawMusicNote(dc, new System.Windows.Point(width - (420 * scale), height * 0.78), scale * 1.0, System.Windows.Media.Color.FromRgb(6, 182, 212));

            // 5. Header: "★ FINALE PERFORMANCE ★"
            var headerText = new System.Windows.Media.FormattedText(
                "★ FINALE PERFORMANCE ★",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.ExtraBold, System.Windows.FontStretches.Normal),
                70 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(6, 182, 212)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(headerText, new System.Windows.Point(width / 2.0, 180 * scale));

            // 6. Main Title: "LAST SONG OF THE NIGHT"
            const string mainTitle = "LAST SONG OF THE NIGHT";
            double titleFontSize = 125 * scale;

            // Shadow / 3D Depth
            var titleShadow = new System.Windows.Media.FormattedText(
                mainTitle,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Black, System.Windows.FontStretches.Normal),
                titleFontSize,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 0, 0, 0)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(titleShadow, new System.Windows.Point((width / 2.0) + (6 * scale), ((height / 2.0) - (titleShadow.Height / 2.0) - (40 * scale)) + (6 * scale)));

            // Foreground Text (Golden/Amber Glow Gradient)
            var titleText = new System.Windows.Media.FormattedText(
                mainTitle,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Black, System.Windows.FontStretches.Normal),
                titleFontSize,
                new System.Windows.Media.LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(255, 245, 160),
                    System.Windows.Media.Color.FromRgb(245, 158, 11), 90),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(titleText, new System.Windows.Point(width / 2.0, (height / 2.0) - (titleText.Height / 2.0) - (40 * scale)));

            // 7. Subtitle: "THANK YOU FOR SINGING WITH US!"
            var subtitleText = new System.Windows.Media.FormattedText(
                "THANK YOU FOR SINGING & PARTYING WITH US TONIGHT!",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                42 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(subtitleText, new System.Windows.Point(width / 2.0, (height / 2.0) + (80 * scale)));

            // 8. Footer: "★ DRIVE SAFE • SEE YOU NEXT TIME! ★"
            var footerText = new System.Windows.Media.FormattedText(
                "★ DRIVE SAFE • SEE YOU NEXT TIME! ★",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Arial"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.ExtraBold, System.Windows.FontStretches.Normal),
                46 * scale,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),
                1.0)
            {
                TextAlignment = System.Windows.TextAlignment.Center
            };
            dc.DrawText(footerText, new System.Windows.Point(width / 2.0, height - (200 * scale)));
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        encoder.Save(fs);
    }
#endif

#if MAUI
    public static void CreatePersonalizedBirthdayBannerPng(string filePath, string performerName, int targetWidth = 1920, int targetHeight = 1080)
    {
        _ = filePath;
        _ = performerName;
        _ = targetWidth;
        _ = targetHeight;
    }

    public static void CreateLastSongBannerPng(string filePath, int targetWidth = 1920, int targetHeight = 1080)
    {
        _ = filePath;
        _ = targetWidth;
        _ = targetHeight;
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
