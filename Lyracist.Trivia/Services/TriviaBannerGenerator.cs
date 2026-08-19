// Edited on Aug 19, 2026 @ 09:32:00 -> Remove hardcoded counts, timers, and scoring rules from category banners
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lyracist.Trivia.Core.Services;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaPen = System.Windows.Media.Pen;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfPoint = System.Windows.Point;
using WpfFlowDirection = System.Windows.FlowDirection;

namespace Lyracist.Trivia.Services;

public static class TriviaBannerGenerator
{
    public record BannerDefinition(
        string FileName,
        string Title,
        string Subtitle,
        string Icon,
        MediaColor BgTop,
        MediaColor BgBottom,
        MediaColor AccentPrimary,
        MediaColor AccentSecondary
    );

    public static readonly BannerDefinition[] AllBanners =
    [
        new(
            "biker_trivia.png",
            "BIKERS & MOTORCYCLES",
            "Harley-Davidson, Indian, Classic Choppers, Road Rallies & MC Culture",
            "🏍️",
            MediaColor.FromRgb(0x18, 0x18, 0x1B),
            MediaColor.FromRgb(0x27, 0x27, 0x2A),
            MediaColor.FromRgb(0xEA, 0x58, 0x0C),
            MediaColor.FromRgb(0xF5, 0x9E, 0x0B)
        ),
        new(
            "rock_and_roll.png",
            "ROCK & ROLL LEGENDS",
            "Classic Rock, Guitar Heroes, 70s/80s Arena Bands & Anthems",
            "🎸",
            MediaColor.FromRgb(0x1E, 0x1B, 0x4B),
            MediaColor.FromRgb(0x31, 0x2E, 0x81),
            MediaColor.FromRgb(0xEC, 0x48, 0x99),
            MediaColor.FromRgb(0x8B, 0x5C, 0xF6)
        ),
        new(
            "country_music.png",
            "COUNTRY MUSIC HITS",
            "Outlaw Country, 90s Anthems, Grand Ole Opry & Honky-Tonk Legends",
            "🤠",
            MediaColor.FromRgb(0x2E, 0x10, 0x05),
            MediaColor.FromRgb(0x45, 0x1A, 0x03),
            MediaColor.FromRgb(0xF5, 0x9E, 0x0B),
            MediaColor.FromRgb(0xD9, 0x77, 0x06)
        ),
        new(
            "geography.png",
            "WORLD GEOGRAPHY",
            "Continents, Oceans, Mountain Peaks, Rivers, Borders & Global Wonders",
            "🌍",
            MediaColor.FromRgb(0x02, 0x2C, 0x22),
            MediaColor.FromRgb(0x06, 0x4E, 0x3B),
            MediaColor.FromRgb(0x10, 0xB9, 0x81),
            MediaColor.FromRgb(0x06, 0xB6, 0xD4)
        ),
        new(
            "state_capitals.png",
            "STATE & WORLD CAPITALS",
            "All 50 U.S. State Capitals, Territories & Major Global Capitals",
            "🏛️",
            MediaColor.FromRgb(0x0F, 0x17, 0x2A),
            MediaColor.FromRgb(0x1E, 0x3A, 0x8A),
            MediaColor.FromRgb(0x38, 0xBD, 0xF8),
            MediaColor.FromRgb(0xFB, 0xBF, 0x24)
        ),
        new(
            "history.png",
            "WORLD HISTORY",
            "Ancient Civilizations, American Revolutions, World Wars & Space Race",
            "📜",
            MediaColor.FromRgb(0x2A, 0x12, 0x08),
            MediaColor.FromRgb(0x43, 0x14, 0x07),
            MediaColor.FromRgb(0xF9, 0x73, 0x16),
            MediaColor.FromRgb(0xEF, 0x44, 0x44)
        ),
        new(
            "complete_the_lyric.png",
            "COMPLETE THE LYRIC",
            "Finish the Words to the Greatest Rock, Pop & Singalong Classics",
            "🎤",
            MediaColor.FromRgb(0x2E, 0x10, 0x65),
            MediaColor.FromRgb(0x4C, 0x1D, 0x95),
            MediaColor.FromRgb(0xF4, 0x3F, 0x5E),
            MediaColor.FromRgb(0x06, 0xB6, 0xD4)
        ),
        new(
            "tv_shows.png",
            "TV SHOWS & SITCOMS",
            "Classic Sitcoms, Drama Series, 90s TV & Emmy-Winning Hits",
            "📺",
            MediaColor.FromRgb(0x0F, 0x17, 0x2A),
            MediaColor.FromRgb(0x1E, 0x1B, 0x4B),
            MediaColor.FromRgb(0x63, 0x66, 0xF1),
            MediaColor.FromRgb(0x38, 0xBD, 0xF8)
        ),
        new(
            "sports.png",
            "SPORTS & ATHLETES",
            "NFL Football, MLB Baseball, NBA Basketball, NHL Hockey & Olympics",
            "🏆",
            MediaColor.FromRgb(0x05, 0x2E, 0x16),
            MediaColor.FromRgb(0x14, 0x53, 0x2D),
            MediaColor.FromRgb(0x22, 0xC5, 0x5E),
            MediaColor.FromRgb(0xEA, 0xB3, 0x08)
        ),
        new(
            "logos_and_slogans.png",
            "LOGOS & SLOGANS",
            "Famous Advertising Taglines, Commercial Jingles & Brand Mascots",
            "🏷️",
            MediaColor.FromRgb(0x11, 0x18, 0x27),
            MediaColor.FromRgb(0x1F, 0x29, 0x37),
            MediaColor.FromRgb(0x3B, 0x82, 0xF6),
            MediaColor.FromRgb(0xF5, 0x9E, 0x0B)
        ),
        new(
            "music_legends.png",
            "MUSIC & KARAOKE LEGENDS",
            "Iconic Vocalists, Chart-Topping Superstars & Hall of Fame Legends",
            "🌟",
            MediaColor.FromRgb(0x2E, 0x10, 0x65),
            MediaColor.FromRgb(0x3B, 0x07, 0x64),
            MediaColor.FromRgb(0xA8, 0x55, 0xF7),
            MediaColor.FromRgb(0xFB, 0xBF, 0x24)
        ),
        new(
            "pop_culture_80s_90s.png",
            "80s & 90s POP CULTURE",
            "Retro Toys, Video Games, 80s/90s Blockbusters & Nostalgic Trends",
            "🕹️",
            MediaColor.FromRgb(0x28, 0x0B, 0x3B),
            MediaColor.FromRgb(0x4A, 0x0E, 0x4E),
            MediaColor.FromRgb(0xEC, 0x48, 0x99),
            MediaColor.FromRgb(0x2D, 0xD4, 0xBF)
        ),
        new(
            "movie_soundtracks.png",
            "MOVIE SOUNDTRACKS",
            "Blockbuster Film Themes, Oscar-Winning Scores & Iconic Needle-Drops",
            "🎬",
            MediaColor.FromRgb(0x18, 0x18, 0x1B),
            MediaColor.FromRgb(0x09, 0x09, 0x0B),
            MediaColor.FromRgb(0xF5, 0x9E, 0x0B),
            MediaColor.FromRgb(0xE2, 0xE8, 0xF0)
        ),
        new(
            "pub_general_knowledge.png",
            "PUB TRIVIA ALL-STARS",
            "Science, Literature, Food & Drink, Mythologies & Pub Favorites",
            "🍻",
            MediaColor.FromRgb(0x1C, 0x19, 0x17),
            MediaColor.FromRgb(0x29, 0x25, 0x24),
            MediaColor.FromRgb(0xF5, 0x9E, 0x0B),
            MediaColor.FromRgb(0x10, 0xB9, 0x81)
        ),
        new(
            "famous_movie_quotes.png",
            "FAMOUS QUOTES FROM MOVIES",
            "Iconic Catchphrases, Memorable Movie Quotes & Legendary Film Lines",
            "🍿",
            MediaColor.FromRgb(0x2E, 0x10, 0x65),
            MediaColor.FromRgb(0x3B, 0x07, 0x64),
            MediaColor.FromRgb(0xA8, 0x55, 0xF7),
            MediaColor.FromRgb(0x38, 0xBD, 0xF8)
        )
    ];

    public static void GenerateAllBanners(string? outputDir = null)
    {
        string dir = outputDir ?? TriviaStorageHelper.GetBannersDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        foreach (var def in AllBanners)
        {
            string outPath = Path.Combine(dir, def.FileName);
            GenerateBanner(def, outPath);
        }
    }

    public static void GenerateBanner(BannerDefinition def, string destinationPath)
    {
        const int width = 1920;
        const int height = 1080;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 1. Background deep gradient
            var bgBrush = new LinearGradientBrush(def.BgTop, def.BgBottom, new WpfPoint(0, 0), new WpfPoint(1, 1));
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

            // 2. Central ambient radial glow
            var glowBrush = new RadialGradientBrush
            {
                Center = new WpfPoint(0.5, 0.48),
                GradientOrigin = new WpfPoint(0.5, 0.48),
                RadiusX = 0.55,
                RadiusY = 0.50
            };
            glowBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(0x45, def.AccentPrimary.R, def.AccentPrimary.G, def.AccentPrimary.B), 0.0));
            glowBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(0x15, def.AccentSecondary.R, def.AccentSecondary.G, def.AccentSecondary.B), 0.6));
            glowBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
            dc.DrawRectangle(glowBrush, null, new Rect(0, 0, width, height));

            // 3. Decorative outer border with glowing accent
            var outerBorderPen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), 2.0);
            dc.DrawRoundedRectangle(null, outerBorderPen, new Rect(40, 40, width - 80, height - 80), 24, 24);

            var innerBorderPen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x60, def.AccentPrimary.R, def.AccentPrimary.G, def.AccentPrimary.B)), 1.5);
            dc.DrawRoundedRectangle(null, innerBorderPen, new Rect(55, 55, width - 110, height - 110), 18, 18);

            // Corner decorative brackets
            var bracketPen = new MediaPen(new SolidColorBrush(def.AccentPrimary), 4.0);
            // Top-Left
            dc.DrawLine(bracketPen, new WpfPoint(70, 70), new WpfPoint(140, 70));
            dc.DrawLine(bracketPen, new WpfPoint(70, 70), new WpfPoint(70, 140));
            // Top-Right
            dc.DrawLine(bracketPen, new WpfPoint(width - 140, 70), new WpfPoint(width - 70, 70));
            dc.DrawLine(bracketPen, new WpfPoint(width - 70, 70), new WpfPoint(width - 70, 140));
            // Bottom-Left
            dc.DrawLine(bracketPen, new WpfPoint(70, height - 70), new WpfPoint(140, height - 70));
            dc.DrawLine(bracketPen, new WpfPoint(70, height - 70), new WpfPoint(70, height - 140));
            // Bottom-Right
            dc.DrawLine(bracketPen, new WpfPoint(width - 140, height - 70), new WpfPoint(width - 70, height - 70));
            dc.DrawLine(bracketPen, new WpfPoint(width - 70, height - 70), new WpfPoint(width - 70, height - 140));

            // 4. Top Header Pill Badge: "★ LYRACIST TRIVIA NIGHT • CATEGORY SPOTLIGHT ★"
            const double pillWidth = 580;
            const double pillHeight = 46;
            double pillX = (width - pillWidth) / 2.0;
            const double pillY = 120;
            var pillBg = new SolidColorBrush(MediaColor.FromArgb(0xDD, 0x0F, 0x17, 0x2A));
            var pillBorder = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0xAA, def.AccentPrimary.R, def.AccentPrimary.G, def.AccentPrimary.B)), 1.5);
            dc.DrawRoundedRectangle(pillBg, pillBorder, new Rect(pillX, pillY, pillWidth, pillHeight), 23, 23);

            var pillText = new FormattedText(
                "★  LYRACIST TRIVIA NIGHT  •  CATEGORY SPOTLIGHT  ★",
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal),
                16,
                new SolidColorBrush(def.AccentSecondary),
                1.0
            );
            dc.DrawText(pillText, new WpfPoint((width - pillText.Width) / 2.0, pillY + 12));

            // 5. Central Category Icon
            var iconText = new FormattedText(
                def.Icon,
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI Emoji"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                120,
                MediaBrushes.White,
                1.0
            );
            dc.DrawText(iconText, new WpfPoint((width - iconText.Width) / 2.0, 220));

            // 6. Main Category Title
            var titleBrush = new LinearGradientBrush(
                Colors.White,
                def.AccentSecondary,
                new WpfPoint(0, 0),
                new WpfPoint(0, 1)
            );

            var titleText = new FormattedText(
                def.Title,
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal),
                76,
                titleBrush,
                1.0
            );

            // Title shadow
            var titleShadow = new FormattedText(
                def.Title,
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal),
                76,
                new SolidColorBrush(MediaColor.FromArgb(0xAA, 0, 0, 0)),
                1.0
            );
            double titleY = 410;
            dc.DrawText(titleShadow, new WpfPoint(((width - titleText.Width) / 2.0) + 4, titleY + 4));
            dc.DrawText(titleText, new WpfPoint((width - titleText.Width) / 2.0, titleY));

            // Title accent underline bar
            double barWidth = Math.Max(titleText.Width + 80, 500);
            double barX = (width - barWidth) / 2.0;
            double barY = titleY + titleText.Height + 16;
            var barPen = new MediaPen(new SolidColorBrush(def.AccentPrimary), 3.5);
            dc.DrawLine(barPen, new WpfPoint(barX, barY), new WpfPoint(barX + barWidth, barY));

            // 7. Subtitle / Description Text
            var subText = new FormattedText(
                def.Subtitle,
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                28,
                new SolidColorBrush(MediaColor.FromRgb(0xCB, 0xD5, 0xE1)),
                1.0
            );
            dc.DrawText(subText, new WpfPoint((width - subText.Width) / 2.0, barY + 36));

            // 8. Bottom Action Callout Pill: "📱 SCAN THE SCREEN QR CODE TO JOIN  •  LOCK IN YOUR ANSWERS ON YOUR PHONE!"
            const double calloutWidth = 920;
            const double calloutHeight = 64;
            double calloutX = (width - calloutWidth) / 2.0;
            const double calloutY = 720;
            var calloutBg = new SolidColorBrush(MediaColor.FromArgb(0xDD, 0x0F, 0x17, 0x2A));
            var calloutBorder = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x99, def.AccentPrimary.R, def.AccentPrimary.G, def.AccentPrimary.B)), 2.0);
            dc.DrawRoundedRectangle(calloutBg, calloutBorder, new Rect(calloutX, calloutY, calloutWidth, calloutHeight), 16, 16);

            var calloutText = new FormattedText(
                "📱  SCAN QR CODE ON SCREEN TO JOIN  •  GET READY TO BUZZ IN!",
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                22,
                MediaBrushes.White,
                1.0
            );
            dc.DrawText(calloutText, new WpfPoint((width - calloutText.Width) / 2.0, calloutY + 17));

            // 9. Bottom Footer Bar
            var footerText = new FormattedText(
                "LYRACIST PUB TRIVIA NIGHT  •  LIVE VENUE MULTI-PLAYER GAME",
                CultureInfo.InvariantCulture,
                WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                16,
                new SolidColorBrush(MediaColor.FromRgb(0x64, 0x74, 0x8B)),
                1.0
            );
            dc.DrawText(footerText, new WpfPoint((width - footerText.Width) / 2.0, height - 100));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        using var fs = File.OpenWrite(destinationPath);
        encoder.Save(fs);
    }

    private record ChipStyle(string Icon, MediaColor AccentPrimary, MediaColor AccentSecondary);

    /// <summary>
    /// Renders a 1920x1080 lobby banner listing every category in a mixed multi-pack game, styled
    /// to match the single-category banners (gradient background, glow, corner brackets, pill
    /// badges) instead of the plain "🎯 Mixed Trivia" text fallback. Returns a frozen in-memory
    /// bitmap - no disk file is written, so there's no stale-file caching to worry about when the
    /// game master changes which packs are checked.
    /// </summary>
    public static BitmapSource RenderMixedBanner(IReadOnlyList<TriviaQuestionPack> packs)
    {
        const int width = 1920;
        const int height = 1080;

        var accentPrimary = MediaColor.FromRgb(0x8B, 0x5C, 0xF6);
        var accentSecondary = MediaColor.FromRgb(0xFB, 0xBF, 0x24);
        var bgTop = MediaColor.FromRgb(0x1E, 0x1B, 0x4B);
        var bgBottom = MediaColor.FromRgb(0x0F, 0x0A, 0x1F);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // 1. Background gradient + ambient glow (same look as the single-category banners)
            var bgBrush = new LinearGradientBrush(bgTop, bgBottom, new WpfPoint(0, 0), new WpfPoint(1, 1));
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

            var glowBrush = new RadialGradientBrush { Center = new WpfPoint(0.5, 0.42), GradientOrigin = new WpfPoint(0.5, 0.42), RadiusX = 0.65, RadiusY = 0.6 };
            glowBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(0x40, accentPrimary.R, accentPrimary.G, accentPrimary.B), 0.0));
            glowBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(0x12, accentSecondary.R, accentSecondary.G, accentSecondary.B), 0.6));
            glowBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
            dc.DrawRectangle(glowBrush, null, new Rect(0, 0, width, height));

            // 2. Decorative border + corner brackets
            var outerBorderPen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x30, 0xFF, 0xFF, 0xFF)), 2.0);
            dc.DrawRoundedRectangle(null, outerBorderPen, new Rect(40, 40, width - 80, height - 80), 24, 24);
            var innerBorderPen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x60, accentPrimary.R, accentPrimary.G, accentPrimary.B)), 1.5);
            dc.DrawRoundedRectangle(null, innerBorderPen, new Rect(55, 55, width - 110, height - 110), 18, 18);

            var bracketPen = new MediaPen(new SolidColorBrush(accentPrimary), 4.0);
            dc.DrawLine(bracketPen, new WpfPoint(70, 70), new WpfPoint(140, 70));
            dc.DrawLine(bracketPen, new WpfPoint(70, 70), new WpfPoint(70, 140));
            dc.DrawLine(bracketPen, new WpfPoint(width - 140, 70), new WpfPoint(width - 70, 70));
            dc.DrawLine(bracketPen, new WpfPoint(width - 70, 70), new WpfPoint(width - 70, 140));
            dc.DrawLine(bracketPen, new WpfPoint(70, height - 70), new WpfPoint(140, height - 70));
            dc.DrawLine(bracketPen, new WpfPoint(70, height - 70), new WpfPoint(70, height - 140));
            dc.DrawLine(bracketPen, new WpfPoint(width - 140, height - 70), new WpfPoint(width - 70, height - 70));
            dc.DrawLine(bracketPen, new WpfPoint(width - 70, height - 70), new WpfPoint(width - 70, height - 140));

            // 3. Header pill
            DrawPill(dc, width, "⚡  MIXED CATEGORY SHUFFLE  •  LYRACIST TRIVIA NIGHT  ⚡", 120, accentPrimary, accentSecondary);

            // 4. Big title + subtitle
            const string title = "MIXED TRIVIA NIGHT";
            var titleBrush = new LinearGradientBrush(Colors.White, accentSecondary, new WpfPoint(0, 0), new WpfPoint(0, 1));
            var titleFace = new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal);
            var titleText = new FormattedText(title, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, titleFace, 64, titleBrush, 1.0);
            var titleShadow = new FormattedText(title, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, titleFace, 64, new SolidColorBrush(MediaColor.FromArgb(0xAA, 0, 0, 0)), 1.0);
            const double titleY = 210;
            dc.DrawText(titleShadow, new WpfPoint(((width - titleText.Width) / 2.0) + 4, titleY + 4));
            dc.DrawText(titleText, new WpfPoint((width - titleText.Width) / 2.0, titleY));

            var subText = new FormattedText(
                $"{packs.Count} Categories Shuffled Together Into One Game",
                CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                26, new SolidColorBrush(MediaColor.FromRgb(0xCB, 0xD5, 0xE1)), 1.0);
            dc.DrawText(subText, new WpfPoint((width - subText.Width) / 2.0, titleY + 92));

            // 5. One color-coded chip per category, in a centered grid (cap the grid at 6 - if more
            // packs than that are checked, the overflow is summarized below the grid instead of
            // shrinking every chip into illegibility).
            int chipCount = Math.Min(packs.Count, 6);
            int columns = chipCount <= 3 ? Math.Max(chipCount, 1) : (int)Math.Ceiling(chipCount / 2.0);
            int rows = chipCount == 0 ? 0 : (int)Math.Ceiling(chipCount / (double)columns);
            const double chipWidth = 460;
            const double chipHeight = 150;
            const double chipGapX = 30;
            const double chipGapY = 26;
            double gridWidth = columns * chipWidth + (columns - 1) * chipGapX;
            double gridHeight = rows * chipHeight + Math.Max(0, rows - 1) * chipGapY;
            double gridX = (width - gridWidth) / 2.0;
            const double gridY = 470;

            for (int i = 0; i < chipCount; i++)
            {
                int col = i % columns;
                int row = i / columns;
                double x = gridX + col * (chipWidth + chipGapX);
                double y = gridY + row * (chipHeight + chipGapY);
                DrawChip(dc, packs[i].Title, ResolveChipStyle(packs[i]), x, y, chipWidth, chipHeight);
            }

            if (packs.Count > chipCount)
            {
                var moreText = new FormattedText(
                    $"+ {packs.Count - chipCount} more categories in the mix",
                    CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
                    new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Italic, FontWeights.SemiBold, FontStretches.Normal),
                    22, new SolidColorBrush(MediaColor.FromRgb(0x94, 0xA3, 0xB8)), 1.0);
                dc.DrawText(moreText, new WpfPoint((width - moreText.Width) / 2.0, gridY + gridHeight + 16));
            }

            // 6. Bottom callout + footer
            DrawCallout(dc, width, "📱  SCAN QR CODE ON SCREEN TO JOIN  •  GET READY TO BUZZ IN!", 720, accentPrimary);

            var footerText = new FormattedText(
                "LYRACIST PUB TRIVIA NIGHT  •  LIVE VENUE MULTI-PLAYER GAME",
                CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
                new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                16, new SolidColorBrush(MediaColor.FromRgb(0x64, 0x74, 0x8B)), 1.0);
            dc.DrawText(footerText, new WpfPoint((width - footerText.Width) / 2.0, height - 100));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>
    /// Maps a pack to the icon/accent colors of its matching built-in category banner (by looking
    /// up the same file TriviaStorageHelper.GetBannerPathForPack would resolve), so a mixed-game
    /// chip for "Rock & Roll Legends" reuses that category's real icon and colors instead of a
    /// generic placeholder. Falls back to a neutral violet/target style for custom user packs that
    /// don't match any built-in category.
    /// </summary>
    private static ChipStyle ResolveChipStyle(TriviaQuestionPack pack)
    {
        string? bannerPath = TriviaStorageHelper.GetBannerPathForPack(pack.Category);
        if (bannerPath != null)
        {
            string fileName = Path.GetFileName(bannerPath);
            var match = Array.Find(AllBanners, b => string.Equals(b.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return new ChipStyle(match.Icon, match.AccentPrimary, match.AccentSecondary);
        }

        return new ChipStyle("🎯", MediaColor.FromRgb(0x8B, 0x5C, 0xF6), MediaColor.FromRgb(0xA7, 0x8B, 0xFA));
    }

    private static void DrawPill(DrawingContext dc, int width, string text, double y, MediaColor accentPrimary, MediaColor accentSecondary)
    {
        var pillText = new FormattedText(
            text, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
            new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal),
            16, new SolidColorBrush(accentSecondary), 1.0);

        double pillWidth = pillText.Width + 64;
        const double pillHeight = 46;
        double pillX = (width - pillWidth) / 2.0;
        var pillBg = new SolidColorBrush(MediaColor.FromArgb(0xDD, 0x0F, 0x17, 0x2A));
        var pillBorder = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0xAA, accentPrimary.R, accentPrimary.G, accentPrimary.B)), 1.5);
        dc.DrawRoundedRectangle(pillBg, pillBorder, new Rect(pillX, y, pillWidth, pillHeight), 23, 23);
        dc.DrawText(pillText, new WpfPoint((width - pillText.Width) / 2.0, y + 12));
    }

    private static void DrawCallout(DrawingContext dc, int width, string text, double y, MediaColor accentPrimary)
    {
        var calloutText = new FormattedText(
            text, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
            new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            22, MediaBrushes.White, 1.0);

        double calloutWidth = calloutText.Width + 100;
        const double calloutHeight = 64;
        double calloutX = (width - calloutWidth) / 2.0;
        var calloutBg = new SolidColorBrush(MediaColor.FromArgb(0xDD, 0x0F, 0x17, 0x2A));
        var calloutBorder = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0x99, accentPrimary.R, accentPrimary.G, accentPrimary.B)), 2.0);
        dc.DrawRoundedRectangle(calloutBg, calloutBorder, new Rect(calloutX, y, calloutWidth, calloutHeight), 16, 16);
        dc.DrawText(calloutText, new WpfPoint((width - calloutText.Width) / 2.0, y + 17));
    }

    private static void DrawChip(DrawingContext dc, string label, ChipStyle style, double x, double y, double w, double h)
    {
        var chipBg = new SolidColorBrush(MediaColor.FromArgb(0xCC, 0x0F, 0x17, 0x2A));
        var chipBorder = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(0xAA, style.AccentPrimary.R, style.AccentPrimary.G, style.AccentPrimary.B)), 2.0);
        dc.DrawRoundedRectangle(chipBg, chipBorder, new Rect(x, y, w, h), 16, 16);

        var iconText = new FormattedText(
            style.Icon, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
            new Typeface(new MediaFontFamily("Segoe UI Emoji"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            52, MediaBrushes.White, 1.0);
        dc.DrawText(iconText, new WpfPoint(x + 20, y + (h - iconText.Height) / 2.0));

        var labelText = new FormattedText(
            label.ToUpperInvariant(), CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight,
            new Typeface(new MediaFontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            24, new SolidColorBrush(style.AccentSecondary), 1.0)
        {
            MaxTextWidth = Math.Max(w - 110, 10),
            MaxTextHeight = h - 20,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(labelText, new WpfPoint(x + 95, y + (h - Math.Min(labelText.Height, h - 20)) / 2.0));
    }
}
