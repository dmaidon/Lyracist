// Created on Nov 27, 2026 @ 13:21:00 -> Add Printable Branded Paper Request Slips PDF generator and Weekly Karaoke Schedule Poster Maker
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QRCoder;
using SkiaSharp;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Color = System.Windows.Media.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace Lyracist.Shared
{
    public sealed class ScheduleShowRow
    {
        public string Day { get; set; } = string.Empty;
        public string Venue { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Shared SkiaSharp print and promotional graphics generator for Printable Paper Request Slips
    /// (8, 10, or 12 slips per US Letter PDF page with optional QR code) and Weekly Karaoke Schedule
    /// Social Media / TV Flyers (1080x1080 Square or 1920x1080 16:9).
    /// </summary>
    public static class PromoPrintUtility
    {
        public static string GeneratePrintableRequestSlipsPdf(
            string venueName,
            string djName,
            string? portalUrl = null,
            int slipsPerPage = 10,
            bool includeQrCode = true)
        {
            string cleanVenue = string.IsNullOrWhiteSpace(venueName) ? "Karaoke Night" : venueName.Trim();
            string cleanDj = string.IsNullOrWhiteSpace(djName) ? "Guest DJ" : djName.Trim();
            int validSlips = slipsPerPage switch
            {
                <= 8 => 8,
                >= 12 => 12,
                _ => 10
            };

            string reportsDir = Globals.KSRotationReportsDir;
            Directory.CreateDirectory(reportsDir);
            string filePath = Path.Combine(reportsDir, $"Karaoke_Request_Slips_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            const float pageWidth = 612f;  // 8.5 in * 72 pt
            const float pageHeight = 792f; // 11 in * 72 pt
            const float margin = 22f;
            int cols = 2;
            int rows = validSlips / cols;

            float gridWidth = pageWidth - (margin * 2f);
            float gridHeight = pageHeight - (margin * 2f);
            float cellWidth = gridWidth / cols;
            float cellHeight = gridHeight / rows;

            SKBitmap? qrBitmap = null;
            if (includeQrCode && !string.IsNullOrWhiteSpace(portalUrl) && portalUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                qrBitmap = RenderQrSkBitmap(portalUrl.Trim());
            }

            try
            {
                using var boldTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
                using var regTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;

                using var titleFont = new SKFont(boldTypeface, rows >= 6 ? 9.5f : 10.5f);
                using var subFont = new SKFont(boldTypeface, rows >= 6 ? 7.5f : 8.5f);
                using var labelFont = new SKFont(boldTypeface, rows >= 6 ? 8f : 9f);
                using var hintFont = new SKFont(regTypeface, 6.8f);

                using var headerFill = new SKPaint { Color = new SKColor(24, 34, 56), Style = SKPaintStyle.Fill, IsAntialias = true };
                using var borderStroke = new SKPaint { Color = new SKColor(90, 105, 130), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
                using var cutLineStroke = new SKPaint
                {
                    Color = new SKColor(150, 160, 175),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 0.8f,
                    PathEffect = SKPathEffect.CreateDash([4f, 4f], 0),
                    IsAntialias = true
                };
                using var fieldLineStroke = new SKPaint { Color = new SKColor(70, 80, 95), Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, IsAntialias = true };
                using var whiteText = new SKPaint { Color = SKColors.White, IsAntialias = true };
                using var goldText = new SKPaint { Color = new SKColor(251, 191, 36), IsAntialias = true };
                using var darkText = new SKPaint { Color = new SKColor(20, 28, 45), IsAntialias = true };
                using var mutedText = new SKPaint { Color = new SKColor(100, 116, 139), IsAntialias = true };

                var metadata = new SKDocumentPdfMetadata
                {
                    Title = $"Karaoke Request Slips - {cleanVenue}",
                    Author = cleanDj
                };

                using var stream = File.Create(filePath);
                using var doc = SKDocument.CreatePdf(stream, metadata);
                var canvas = doc.BeginPage(pageWidth, pageHeight);

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        float x = margin + (c * cellWidth);
                        float y = margin + (r * cellHeight);

                        // Inner slip card padding
                        var cardRect = new SKRect(x + 5f, y + 4f, x + cellWidth - 5f, y + cellHeight - 4f);
                        canvas.DrawRoundRect(cardRect, 5f, 5f, borderStroke);

                        // Header bar
                        float headerH = rows >= 6 ? 24f : 27f;
                        var headerRect = new SKRect(cardRect.Left, cardRect.Top, cardRect.Right, cardRect.Top + headerH);
                        canvas.DrawRoundRect(headerRect, 5f, 5f, headerFill);
                        canvas.DrawRect(new SKRect(cardRect.Left, cardRect.Top + headerH - 5f, cardRect.Right, cardRect.Top + headerH), headerFill);

                        canvas.DrawText("♪ KARAOKE REQUEST SLIP", cardRect.Left + 8f, cardRect.Top + 11f, SKTextAlign.Left, titleFont, goldText);
                        string brandLine = $"{cleanVenue} • Hosted by {cleanDj}";
                        brandLine = FitText(brandLine, subFont, cardRect.Width - 16f);
                        canvas.DrawText(brandLine, cardRect.Left + 8f, cardRect.Top + headerH - 4f, SKTextAlign.Left, subFont, whiteText);

                        float contentRight = cardRect.Right - 8f;
                        if (qrBitmap != null)
                        {
                            float qrSize = rows >= 6 ? 34f : 40f;
                            var qrRect = new SKRect(cardRect.Right - qrSize - 6f, cardRect.Top + headerH + 3f, cardRect.Right - 6f, cardRect.Top + headerH + 3f + qrSize);
                            using var qrImg = SKImage.FromBitmap(qrBitmap);
                            canvas.DrawImage(qrImg, qrRect, new SKSamplingOptions(SKFilterMode.Nearest));
                            canvas.DrawText("Scan App", qrRect.MidX, qrRect.Bottom + 7f, SKTextAlign.Center, hintFont, mutedText);
                            contentRight = qrRect.Left - 8f;
                        }

                        float rowSpacing = (cardRect.Height - headerH - 10f) / 4f;
                        float curY = cardRect.Top + headerH + rowSpacing - 2f;

                        // Line 1: Singer Name
                        canvas.DrawText("Singer Name:", cardRect.Left + 8f, curY, SKTextAlign.Left, labelFont, darkText);
                        canvas.DrawLine(cardRect.Left + 68f, curY + 1f, contentRight, curY + 1f, fieldLineStroke);

                        // Line 2: Song Title
                        curY += rowSpacing;
                        canvas.DrawText("Song Title:", cardRect.Left + 8f, curY, SKTextAlign.Left, labelFont, darkText);
                        canvas.DrawLine(cardRect.Left + 60f, curY + 1f, contentRight, curY + 1f, fieldLineStroke);

                        // Line 3: Artist
                        curY += rowSpacing;
                        canvas.DrawText("Artist:", cardRect.Left + 8f, curY, SKTextAlign.Left, labelFont, darkText);
                        canvas.DrawLine(cardRect.Left + 40f, curY + 1f, cardRect.Right - 8f, curY + 1f, fieldLineStroke);

                        // Line 4: Duet Partner & Key
                        curY += rowSpacing;
                        canvas.DrawText("Duet Partner:", cardRect.Left + 8f, curY, SKTextAlign.Left, labelFont, darkText);
                        float keyStartX = cardRect.Right - 108f;
                        canvas.DrawLine(cardRect.Left + 68f, curY + 1f, keyStartX - 6f, curY + 1f, fieldLineStroke);
                        canvas.DrawText("Key: [ -2  -1   0  +1  +2 ]", keyStartX, curY, SKTextAlign.Left, subFont, darkText);
                    }
                }

                // Draw dashed cut guidelines
                canvas.DrawLine(margin + cellWidth, margin, margin + cellWidth, pageHeight - margin, cutLineStroke);
                for (int r = 1; r < rows; r++)
                {
                    float cutY = margin + (r * cellHeight);
                    canvas.DrawLine(margin, cutY, pageWidth - margin, cutY, cutLineStroke);
                }

                doc.EndPage();
                doc.Close();
            }
            finally
            {
                qrBitmap?.Dispose();
            }

            return filePath;
        }

        public static string GenerateWeeklySchedulePosterPng(
            string djName,
            string headline,
            IReadOnlyList<ScheduleShowRow> scheduleRows,
            bool squareSocialFormat = true,
            int themeIndex = 0)
        {
            PosterTheme theme = PosterThemes[Math.Clamp(themeIndex, 0, PosterThemes.Length - 1)];
            string cleanDj = string.IsNullOrWhiteSpace(djName) ? "Guest DJ" : djName.Trim();
            string cleanHeadline = string.IsNullOrWhiteSpace(headline) ? "WEEKLY KARAOKE SCHEDULE" : headline.Trim();

            int width = squareSocialFormat ? 1080 : 1920;
            int height = 1080;

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;

            // Rich stage background gradient
            using (var bgPaint = new SKPaint())
            {
                bgPaint.Shader = SKShader.CreateRadialGradient(
                    new SKPoint(width * 0.5f, height * 0.25f),
                    width * 0.85f,
                    [theme.BackgroundCenter, theme.BackgroundMiddle, theme.BackgroundEdge],
                    [0f, 0.6f, 1f],
                    SKShaderTileMode.Clamp);
                canvas.DrawRect(0, 0, width, height, bgPaint);
            }

            // Outer neon frame
            using var outerGlow = new SKPaint
            {
                Color = theme.Accent.WithAlpha(180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 6f,
                IsAntialias = true
            };
            using var goldFrame = new SKPaint
            {
                Color = theme.Frame.WithAlpha(220),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRect(26, 26, width - 26, height - 26), 24, 24, outerGlow);
            canvas.DrawRoundRect(new SKRect(36, 36, width - 36, height - 36), 18, 18, goldFrame);

            using var boldFace = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            using var regFace = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;

            using var headerFont = new SKFont(boldFace, squareSocialFormat ? 52f : 60f);
            using var djFont = new SKFont(boldFace, squareSocialFormat ? 32f : 36f);
            using var dayFont = new SKFont(boldFace, 26f);
            using var venueFont = new SKFont(boldFace, 31f);
            using var timeFont = new SKFont(boldFace, 26f);
            using var notesFont = new SKFont(regFace, 21f);
            using var footerFont = new SKFont(regFace, 20f);

            using var whitePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var goldPaint = new SKPaint { Color = theme.Headline, IsAntialias = true };
            using var cyanPaint = new SKPaint { Color = theme.Host, IsAntialias = true };
            using var mutedPaint = new SKPaint { Color = new SKColor(203, 213, 225), IsAntialias = true };
            using var cardFill = new SKPaint { Color = theme.Card.WithAlpha(225), Style = SKPaintStyle.Fill, IsAntialias = true };
            using var cardStroke = new SKPaint { Color = theme.Badge.WithAlpha(190), Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
            using var dayBadgeFill = new SKPaint { Color = theme.Badge, Style = SKPaintStyle.Fill, IsAntialias = true };

            canvas.DrawText(FitText(cleanHeadline.ToUpperInvariant(), headerFont, width - 120f), width / 2f, 112f, SKTextAlign.Center, headerFont, goldPaint);
            canvas.DrawText(FitText($"HOSTED BY {cleanDj.ToUpperInvariant()}", djFont, width - 120f), width / 2f, 162f, SKTextAlign.Center, djFont, cyanPaint);

            var activeRows = scheduleRows
                .Where(r => !string.IsNullOrWhiteSpace(r.Venue) || !string.IsNullOrWhiteSpace(r.Day))
                .Take(7)
                .ToList();

            if (activeRows.Count == 0)
            {
                activeRows.Add(new ScheduleShowRow
                {
                    Day = "FRIDAY & SATURDAY",
                    Venue = "LIVE KARAOKE NIGHT",
                    Time = "8:00 PM - 1:00 AM",
                    Notes = "Scan the QR code on screen to request songs from your phone!"
                });
            }

            float topStart = 200f;
            float bottomLimit = height - 90f;
            float availHeight = bottomLimit - topStart;
            float rowSlotH = Math.Min(122f, availHeight / activeRows.Count);
            float cardH = rowSlotH - 14f;
            float cardLeft = squareSocialFormat ? 68f : 160f;
            float cardRight = width - cardLeft;

            for (int i = 0; i < activeRows.Count; i++)
            {
                var row = activeRows[i];
                float y = topStart + (i * rowSlotH);
                var rect = new SKRect(cardLeft, y, cardRight, y + cardH);
                canvas.DrawRoundRect(rect, 16f, 16f, cardFill);
                canvas.DrawRoundRect(rect, 16f, 16f, cardStroke);

                // Day badge pill on the left
                var badgeRect = new SKRect(rect.Left + 16f, rect.Top + 14f, rect.Left + 225f, rect.Bottom - 14f);
                canvas.DrawRoundRect(badgeRect, 12f, 12f, dayBadgeFill);
                string dayText = (row.Day ?? "SHOW NIGHT").Trim().ToUpperInvariant();
                // Shrink long day names to fit the badge before resorting to truncation.
                float dayWidth = dayFont.MeasureText(dayText);
                using var fittedDayFont = new SKFont(boldFace, Math.Max(15f, dayFont.Size * Math.Min(1f, (badgeRect.Width - 16f) / Math.Max(1f, dayWidth))));
                canvas.DrawText(FitText(dayText, fittedDayFont, badgeRect.Width - 16f), badgeRect.MidX, badgeRect.MidY + 9f, SKTextAlign.Center, fittedDayFont, whitePaint);

                // Venue and notes
                float textLeft = badgeRect.Right + 22f;
                bool hasNotes = !string.IsNullOrWhiteSpace(row.Notes);
                float venueBaseline = hasNotes ? rect.Top + (cardH * 0.44f) : rect.MidY + 10f;
                string timeText = FitText((row.Time ?? "").Trim(), timeFont, (rect.Right - textLeft) * 0.45f);
                float textWidth = rect.Right - 22f - textLeft - (timeText.Length > 0 ? timeFont.MeasureText(timeText) + 20f : 0f);
                canvas.DrawText(FitText((row.Venue ?? "").Trim(), venueFont, textWidth), textLeft, venueBaseline, SKTextAlign.Left, venueFont, whitePaint);

                if (hasNotes)
                {
                    canvas.DrawText(FitText(row.Notes.Trim(), notesFont, textWidth), textLeft, rect.Top + (cardH * 0.80f), SKTextAlign.Left, notesFont, mutedPaint);
                }

                if (!string.IsNullOrWhiteSpace(row.Time))
                {
                    canvas.DrawText(timeText, rect.Right - 22f, rect.MidY + 9f, SKTextAlign.Right, timeFont, goldPaint);
                }
            }

            canvas.DrawText("♪ Bring Your Friends • Sing Your Favorites • Instant Phone Song Requests!", width / 2f, height - 52f, SKTextAlign.Center, footerFont, mutedPaint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            string targetDir = squareSocialFormat ? Globals.KSRotationReportsDir : Globals.DjBannersDir;
            Directory.CreateDirectory(targetDir);
            string suffix = squareSocialFormat ? "Social_1080x1080" : "TV_1920x1080";
            string filePath = Path.Combine(targetDir, $"Karaoke_Schedule_{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using var outStream = File.Create(filePath);
            data.SaveTo(outStream);

            return filePath;
        }

        private sealed record PosterTheme(
            string Name,
            SKColor BackgroundCenter,
            SKColor BackgroundMiddle,
            SKColor BackgroundEdge,
            SKColor Accent,
            SKColor Frame,
            SKColor Headline,
            SKColor Host,
            SKColor Card,
            SKColor Badge);

        private static readonly PosterTheme[] PosterThemes =
        [
            new("Neon Nightclub", new(30, 18, 66), new(12, 16, 33), new(6, 8, 18),
                new(139, 92, 246), new(245, 158, 11), new(251, 191, 36), new(56, 189, 248), new(22, 31, 56), new(124, 58, 237)),
            new("Classic Gold VIP", new(54, 42, 14), new(22, 18, 10), new(8, 7, 5),
                new(212, 175, 55), new(250, 224, 140), new(250, 214, 100), new(245, 235, 205), new(34, 29, 18), new(146, 112, 28)),
            new("Retro Synthwave", new(92, 18, 92), new(40, 12, 70), new(10, 6, 32),
                new(236, 72, 153), new(34, 211, 238), new(255, 138, 216), new(103, 232, 249), new(46, 18, 74), new(190, 24, 133)),
            new("Rock & Roll Dive", new(66, 16, 16), new(28, 12, 12), new(8, 6, 6),
                new(220, 38, 38), new(214, 211, 209), new(248, 113, 113), new(231, 229, 228), new(38, 24, 24), new(153, 27, 27))
        ];

        private static string FitText(string text, SKFont font, float maxWidth)
        {
            if (font.MeasureText(text) <= maxWidth) return text;
            int length = text.Length;
            while (length > 0 && font.MeasureText(text[..length] + "…") > maxWidth) length--;
            return length == 0 ? string.Empty : text[..length].TrimEnd() + "…";
        }

        private static SKBitmap? RenderQrSkBitmap(string payload)
        {
            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
                using var qrCode = new PngByteQRCode(data);
                byte[] pngBytes = qrCode.GetGraphic(8);
                return SKBitmap.Decode(pngBytes);
            }
            catch
            {
                return null;
            }
        }

        public static void ShowRequestSlipsDialog(Window? owner, string defaultVenue, string defaultDj, string? portalUrl)
        {
            var win = new Window
            {
                Title = "🖨️ Printable Branded Karaoke Request Slips",
                Width = 460,
                Height = 360,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White
            };

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = "🖨️ Generate Cuttable Paper Request Slips",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            root.Children.Add(new TextBlock { Text = "Venue / Show Name:", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            var tbVenue = new TextBox { Text = string.IsNullOrWhiteSpace(defaultVenue) ? "Karaoke Night" : defaultVenue, Height = 30, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(tbVenue);

            root.Children.Add(new TextBlock { Text = "DJ / Host Name:", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            var tbDj = new TextBox { Text = string.IsNullOrWhiteSpace(defaultDj) ? "Guest DJ" : defaultDj, Height = 30, Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(tbDj);

            root.Children.Add(new TextBlock { Text = "Layout (Slips per Page):", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            var cbCount = new ComboBox { Height = 30, Margin = new Thickness(0, 0, 0, 10) };
            cbCount.Items.Add("8 Slips per Page (Large)");
            cbCount.Items.Add("10 Slips per Page (Standard)");
            cbCount.Items.Add("12 Slips per Page (Compact)");
            cbCount.SelectedIndex = 1;
            root.Children.Add(cbCount);

            var chkQr = new CheckBox
            {
                Content = "Include Phone Request QR Code on each slip",
                IsChecked = !string.IsNullOrWhiteSpace(portalUrl),
                Foreground = Brushes.White,
                Margin = new Thickness(0, 2, 0, 16)
            };
            root.Children.Add(chkQr);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnCancel = new Button { Content = "Cancel", Width = 90, Height = 32, Margin = new Thickness(0, 0, 10, 0) };
            btnCancel.Click += (_, _) => win.Close();

            var btnGenerate = new Button
            {
                Content = "🖨️ Generate & Open PDF",
                Width = 180,
                Height = 32,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = Brushes.White
            };
            btnGenerate.Click += (_, _) =>
            {
                int slips = cbCount.SelectedIndex == 0 ? 8 : (cbCount.SelectedIndex == 2 ? 12 : 10);
                try
                {
                    string pdfPath = GeneratePrintableRequestSlipsPdf(
                        tbVenue.Text,
                        tbDj.Text,
                        portalUrl,
                        slips,
                        chkQr.IsChecked == true);
                    Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                    win.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate request slips PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnGenerate);
            root.Children.Add(btnRow);
            win.Content = root;
            win.ShowDialog();
        }

        public static void ShowSchedulePosterDialog(Window? owner, string defaultDj, string defaultVenue)
        {
            var win = new Window
            {
                Title = "🎨 Weekly Karaoke Schedule Poster Maker",
                Width = 680,
                Height = 580,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White
            };

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = "🎨 Weekly Karaoke Schedule Poster & Flyer Maker",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var spHeadline = new StackPanel();
            spHeadline.Children.Add(new TextBlock { Text = "Poster Headline:", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
            var tbHeadline = new TextBox { Text = "WEEKLY KARAOKE SCHEDULE", Height = 28, Padding = new Thickness(5, 3, 5, 3) };
            spHeadline.Children.Add(tbHeadline);
            Grid.SetColumn(spHeadline, 0);
            topGrid.Children.Add(spHeadline);

            var spDj = new StackPanel();
            spDj.Children.Add(new TextBlock { Text = "DJ / Host Name:", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
            var tbDj = new TextBox { Text = string.IsNullOrWhiteSpace(defaultDj) ? "Guest DJ" : defaultDj, Height = 28, Padding = new Thickness(5, 3, 5, 3) };
            spDj.Children.Add(tbDj);
            Grid.SetColumn(spDj, 2);
            topGrid.Children.Add(spDj);

            var spFormat = new StackPanel();
            spFormat.Children.Add(new TextBlock { Text = "Output Format:", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
            var cbFormat = new ComboBox { Height = 28 };
            cbFormat.Items.Add("1080x1080 Square (Social Media)");
            cbFormat.Items.Add("1920x1080 16:9 (TV / DJ Banner)");
            cbFormat.SelectedIndex = 0;
            spFormat.Children.Add(cbFormat);
            Grid.SetColumn(spFormat, 4);
            topGrid.Children.Add(spFormat);

            root.Children.Add(topGrid);

            root.Children.Add(new TextBlock { Text = "Theme:", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
            var cbTheme = new ComboBox { Height = 28, Width = 220, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var posterTheme in PosterThemes)
            {
                cbTheme.Items.Add(posterTheme.Name);
            }
            cbTheme.SelectedIndex = 0;
            root.Children.Add(cbTheme);

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

            var lblDay = new TextBlock { Text = "Day", FontSize = 11, FontWeight = FontWeights.Bold };
            var lblVenue = new TextBlock { Text = "Venue Name", FontSize = 11, FontWeight = FontWeights.Bold };
            var lblTime = new TextBlock { Text = "Show Time", FontSize = 11, FontWeight = FontWeights.Bold };
            var lblNotes = new TextBlock { Text = "Specials / Notes", FontSize = 11, FontWeight = FontWeights.Bold };
            Grid.SetColumn(lblDay, 0);
            Grid.SetColumn(lblVenue, 2);
            Grid.SetColumn(lblTime, 4);
            Grid.SetColumn(lblNotes, 6);
            headerGrid.Children.Add(lblDay);
            headerGrid.Children.Add(lblVenue);
            headerGrid.Children.Add(lblTime);
            headerGrid.Children.Add(lblNotes);
            root.Children.Add(headerGrid);

            string[] sampleDays = ["TUESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY", ""];
            var rowInputs = new List<(TextBox Day, TextBox Venue, TextBox Time, TextBox Notes)>();

            for (int i = 0; i < 6; i++)
            {
                var rGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                rGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

                var tDay = new TextBox { Text = i == 0 ? "FRIDAY" : (i == 1 ? "SATURDAY" : sampleDays[i]), Height = 28, Padding = new Thickness(5, 3, 5, 3) };
                var tVenue = new TextBox { Text = i == 0 ? (string.IsNullOrWhiteSpace(defaultVenue) ? "Main Street Pub" : defaultVenue) : "", Height = 28, Padding = new Thickness(5, 3, 5, 3) };
                var tTime = new TextBox { Text = i == 0 ? "8:00 PM - 1:00 AM" : "", Height = 28, Padding = new Thickness(5, 3, 5, 3) };
                var tNotes = new TextBox { Text = i == 0 ? "Karaoke, Phone Requests & Trivia!" : "", Height = 28, Padding = new Thickness(5, 3, 5, 3) };

                Grid.SetColumn(tDay, 0);
                Grid.SetColumn(tVenue, 2);
                Grid.SetColumn(tTime, 4);
                Grid.SetColumn(tNotes, 6);
                rGrid.Children.Add(tDay);
                rGrid.Children.Add(tVenue);
                rGrid.Children.Add(tTime);
                rGrid.Children.Add(tNotes);
                root.Children.Add(rGrid);
                rowInputs.Add((tDay, tVenue, tTime, tNotes));
            }

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var btnCancel = new Button { Content = "Close", Width = 90, Height = 34, Margin = new Thickness(0, 0, 10, 0) };
            btnCancel.Click += (_, _) => win.Close();

            var btnExport = new Button
            {
                Content = "🎨 Generate & View Schedule Poster",
                Width = 240,
                Height = 34,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground = Brushes.White
            };
            btnExport.Click += (_, _) =>
            {
                var rows = rowInputs
                    .Where(r => !string.IsNullOrWhiteSpace(r.Venue.Text))
                    .Select(r => new ScheduleShowRow
                    {
                        Day = r.Day.Text,
                        Venue = r.Venue.Text,
                        Time = r.Time.Text,
                        Notes = r.Notes.Text
                    })
                    .ToList();

                try
                {
                    bool isSquare = cbFormat.SelectedIndex == 0;
                    string pngPath = GenerateWeeklySchedulePosterPng(tbDj.Text, tbHeadline.Text, rows, isSquare, cbTheme.SelectedIndex);
                    Process.Start(new ProcessStartInfo(pngPath) { UseShellExecute = true });
                    win.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate schedule poster: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnExport);
            root.Children.Add(btnRow);
            win.Content = root;
            win.ShowDialog();
        }
    }
}
