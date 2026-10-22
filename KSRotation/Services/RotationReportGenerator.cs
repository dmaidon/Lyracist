// Edited on Oct 22, 2026 @ 08:42:00 -> Migrate PDF generation from PDFsharp to SkiaSharp
using KSRotation.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace KSRotation.Services
{
    /// <summary>
    /// Generates the night's rotation report as PDF and CSV files. Contains no platform-specific
    /// delivery logic (email, file-open) so it can be shared between KSRotation and KSRotation.Maui.
    /// </summary>
    public static class RotationReportGenerator
    {
        private static string ReportDirectoryPath => AppPaths.ReportsDirectoryPath;

        /// <summary>A single singer's report line: their display name, completed performances (round-ordered),
        /// and whether they are marked inactive in the queue. Built once and shared by both the CSV and PDF writers.</summary>
        internal sealed record ReportRow(string SingerName, List<SongPerformance> Performances, bool IsInactive, bool IsMusic);

        /// <summary>
        /// Builds the ordered set of report rows from the queue and history. Performances are matched to singers by
        /// stable <see cref="SingerEntry.Id"/> first, then by name for legacy entries (Id == Guid.Empty). Singers who
        /// have history but are no longer in the queue are appended afterward.
        /// </summary>
        internal static List<ReportRow> BuildReportRows(List<SingerEntry> singers, List<SongPerformance> history)
        {
            var rows = new List<ReportRow>();

            // 1. Process active queue singers (preserving queue order)
            foreach (var singer in singers)
            {
                var perfs = history
                    .Where(h => (h.SingerId == singer.Id || (h.SingerId == Guid.Empty && string.Equals(h.SingerName, singer.Name, StringComparison.OrdinalIgnoreCase))) && h.IsMusic == singer.IsMusic)
                    .OrderBy(p => p.Round)
                    .ToList();

                rows.Add(new ReportRow(singer.Name, perfs, singer.IsInactive, singer.IsMusic));
            }

            // 2. Process historical performances for singers who are no longer in the active queue
            // Group history by SingerId (stable identity) and IsMusic
            var historicalById = history
                .Where(h => h.SingerId != Guid.Empty)
                .GroupBy(h => new { h.SingerId, h.IsMusic });

            foreach (var group in historicalById)
            {
                // If not in the queue
                if (!singers.Any(s => s.Id == group.Key.SingerId && s.IsMusic == group.Key.IsMusic))
                {
                    string singerName = group.FirstOrDefault()?.SingerName ?? "Unknown Singer";
                    var perfs = group.OrderBy(p => p.Round).ToList();
                    rows.Add(new ReportRow(singerName, perfs, IsInactive: false, IsMusic: group.Key.IsMusic));
                }
            }

            // Group legacy history (SingerId == Guid.Empty) by name and IsMusic
            var historicalByName = history
                .Where(h => h.SingerId == Guid.Empty)
                .GroupBy(h => new { h.SingerName, h.IsMusic });

            foreach (var group in historicalByName)
            {
                // If not in the queue by name and IsMusic
                if (!singers.Any(s => string.Equals(s.Name, group.Key.SingerName, StringComparison.OrdinalIgnoreCase) && s.IsMusic == group.Key.IsMusic)
                    && !rows.Any(r => string.Equals(r.SingerName, group.Key.SingerName, StringComparison.OrdinalIgnoreCase) && r.IsMusic == group.Key.IsMusic))
                {
                    var perfs = group.OrderBy(p => p.Round).ToList();
                    rows.Add(new ReportRow(group.Key.SingerName, perfs, IsInactive: false, IsMusic: group.Key.IsMusic));
                }
            }

            return rows;
        }

        internal static string GenerateCsv(
            List<SingerEntry> singers,
            List<SongPerformance> history)
        {
            StringBuilder sb = new();
            sb.AppendLine("Singer,Duet Partner,Type,Total Completed Songs,Round,Song Title,Artist,Timestamp");

            var rows = BuildReportRows(singers, history);

            // Write Karaoke rows
            foreach (var row in rows.Where(r => !r.IsMusic))
            {
                if (row.Performances.Count > 0)
                {
                    foreach (var perf in row.Performances)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsv(row.SingerName)}\",\"{EscapeCsv(perf.DuetPartnerName)}\",\"Karaoke\",{row.Performances.Count},{perf.Round},\"{EscapeCsv(perf.SongTitle)}\",\"{EscapeCsv(perf.ArtistName)}\",\"{perf.Timestamp:yyyy-MM-dd HH:mm:ss}\"");
                    }
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsv(row.SingerName)}\",\"\",\"Karaoke\",0,,,");
                }
            }

            // Write Music rows
            foreach (var row in rows.Where(r => r.IsMusic))
            {
                if (row.Performances.Count > 0)
                {
                    foreach (var perf in row.Performances)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsv(row.SingerName)}\",\"\",\"Music\",{row.Performances.Count},{perf.Round},\"{EscapeCsv(perf.SongTitle)}\",\"{EscapeCsv(perf.ArtistName)}\",\"{perf.Timestamp:yyyy-MM-dd HH:mm:ss}\"");
                    }
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsv(row.SingerName)}\",\"\",\"Music\",0,,,");
                }
            }

            Directory.CreateDirectory(ReportDirectoryPath);
            string fileName = $"Rotation_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string filePath = Path.Combine(ReportDirectoryPath, fileName);
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return filePath;
        }

        internal static string GeneratePdf(
            List<SingerEntry> singers,
            List<SongPerformance> history,
            string venueName)
        {
            Directory.CreateDirectory(ReportDirectoryPath);
            string fileName = $"Rotation_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(ReportDirectoryPath, fileName);

            var metadata = new SKDocumentPdfMetadata
            {
                Title = $"Karaoke Rotation Report - {venueName}",
                Author = "Karaoke Singer Rotation"
            };

            using (var stream = File.Create(filePath))
            using (var document = SKDocument.CreatePdf(stream, metadata))
            {
                using (var layout = new PdfLayoutManager(document))
                {
                    string date = DateTime.Now.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture);
                    string time = DateTime.Now.ToString("h:mm tt", CultureInfo.CurrentCulture);
                    layout.DrawHeader("Karaoke Rotation Report", venueName, date, time);

                    List<ReportRow> rows = BuildReportRows(singers, history);

                    var karaokeRows = rows.Where(r => !r.IsMusic).ToList();
                    var musicRows = rows.Where(r => r.IsMusic).ToList();

                    // Draw Karaoke section
                    layout.DrawSectionHeader("Karaoke Rotation");
                    if (karaokeRows.Count > 0)
                    {
                        foreach (var row in karaokeRows)
                        {
                            layout.DrawRow(row.SingerName, row.Performances.Count, row.Performances);
                        }
                    }
                    else
                    {
                        layout.DrawEmptySectionMessage("No karaoke performances recorded.");
                    }

                    // Draw Music section
                    layout.DrawSectionHeader("Background Music Requests");
                    if (musicRows.Count > 0)
                    {
                        foreach (var row in musicRows)
                        {
                            layout.DrawRow(row.SingerName, row.Performances.Count, row.Performances);
                        }
                    }
                    else
                    {
                        layout.DrawEmptySectionMessage("No music requests played.");
                    }

                    // Calculate separate totals
                    int totalKaraokeSingers = karaokeRows.Count;
                    int inactiveKaraokeSingers = karaokeRows.Count(r => r.IsInactive);
                    int activeKaraokeSingers = totalKaraokeSingers - inactiveKaraokeSingers;
                    int totalKaraokeSongs = karaokeRows.Sum(r => r.Performances.Count);

                    int totalMusicRequesters = musicRows.Count;
                    int totalMusicTracks = musicRows.Sum(r => r.Performances.Count);

                    layout.DrawSummary(
                        totalKaraokeSingers, activeKaraokeSingers, inactiveKaraokeSingers, totalKaraokeSongs,
                        totalMusicRequesters, totalMusicTracks
                    );
                }
                document.Close();
            }

            return filePath;
        }

        internal static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;

            // Neutralize spreadsheet formula injection: a cell beginning with = + - @ (or tab/CR) can be
            // executed as a formula when opened in Excel/Sheets. Prefix with a single quote to force text.
            char first = val[0];
            if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                val = "'" + val;
            }

            return val.Replace("\"", "\"\"", StringComparison.Ordinal);
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : $"{text.AsSpan(0, maxLength - 3)}...";
        }

        private sealed class PdfLayoutManager : IDisposable
        {
            private readonly SKDocument _document;
            private SKCanvas _canvas = null!;
            private float _yPos;
            private readonly float _margin = 40f;
            private readonly float _pageWidth = 612f;
            private readonly float _pageHeight = 792f;
            private bool _hasOpenPage;

            // Fonts are immutable and reused across every page/row; build them once instead of per-row.
            private static readonly SKTypeface ArialRegular = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            private static readonly SKTypeface ArialBold = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;

            private static readonly SKFont TitleFont = new(ArialBold, 18);
            private static readonly SKFont SectionFont = new(ArialBold, 14);
            private static readonly SKFont MetaFont = new(ArialRegular, 10);
            private static readonly SKFont SingerFont = new(ArialBold, 11);
            private static readonly SKFont SongFont = new(ArialRegular, 9);
            private static readonly SKFont SummaryFont = new(ArialBold, 10);

            private static readonly SKPaint DarkSlateGrayPaint = new() { Color = new SKColor(47, 79, 79), IsAntialias = true };
            private static readonly SKPaint DimGrayPaint = new() { Color = new SKColor(105, 105, 105), IsAntialias = true };
            private static readonly SKPaint GrayPaint = new() { Color = new SKColor(128, 128, 128), IsAntialias = true };
            private static readonly SKPaint BlackPaint = new() { Color = SKColors.Black, IsAntialias = true };
            private static readonly SKPaint GhostWhiteFillPaint = new() { Color = new SKColor(248, 248, 255), Style = SKPaintStyle.Fill };
            private static readonly SKPaint DarkGrayStrokePaint = new() { Color = SKColors.DarkGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            private static readonly SKPaint LightGrayStrokePaint = new() { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

            public PdfLayoutManager(SKDocument document)
            {
                _document = document;
                AddNewPage();
            }

            public void AddNewPage()
            {
                if (_hasOpenPage)
                {
                    _document.EndPage();
                }
                _canvas = _document.BeginPage(_pageWidth, _pageHeight);
                _hasOpenPage = true;
                _yPos = _margin;
            }

            private void DrawText(string text, float x, float topY, SKFont font, SKPaint paint)
            {
                font.GetFontMetrics(out var metrics);
                float baseline = topY - metrics.Ascent;
                _canvas.DrawText(text, x, baseline, SKTextAlign.Left, font, paint);
            }

            private void DrawTextRight(string text, float rightX, float topY, SKFont font, SKPaint paint)
            {
                font.GetFontMetrics(out var metrics);
                float baseline = topY - metrics.Ascent;
                _canvas.DrawText(text, rightX, baseline, SKTextAlign.Right, font, paint);
            }

            public void DrawHeader(string title, string venue, string date, string time)
            {
                // Header Background block
                _canvas.DrawRect(_margin, _yPos, _pageWidth - (2 * _margin), 55, GhostWhiteFillPaint);
                _canvas.DrawRect(_margin, _yPos, _pageWidth - (2 * _margin), 55, DarkGrayStrokePaint);

                // Draw title
                DrawText(title, _margin + 15, _yPos + 8, TitleFont, DarkSlateGrayPaint);

                // Draw metadata
                DrawText($"Generated: {date} {time}", _margin + 15, _yPos + 33, MetaFont, DimGrayPaint);
                DrawTextRight($"Venue: {venue}", _pageWidth - _margin - 15, _yPos + 33, MetaFont, DimGrayPaint);

                _yPos += 75;
            }

            public void DrawSectionHeader(string title)
            {
                if (_yPos + 35 > _pageHeight - _margin)
                {
                    AddNewPage();
                }
                DrawText(title, _margin, _yPos, SectionFont, DarkSlateGrayPaint);
                _yPos += 18;
                _canvas.DrawLine(_margin, _yPos, _pageWidth - _margin, _yPos, LightGrayStrokePaint);
                _yPos += 10;
            }

            public void DrawEmptySectionMessage(string message)
            {
                if (_yPos + 20 > _pageHeight - _margin)
                {
                    AddNewPage();
                }
                DrawText(message, _margin + 15, _yPos, SongFont, GrayPaint);
                _yPos += 22;
            }

            public void DrawRow(string singer, int songCount, List<SongPerformance> songs)
            {
                float rowHeight = 20 + (songs.Count * 14);
                if (_yPos + rowHeight > _pageHeight - _margin)
                {
                    AddNewPage();
                }

                // Draw singer name & count
                DrawText($"{singer} ({songCount} song{(songCount == 1 ? "" : "s")} completed)", _margin, _yPos, SingerFont, BlackPaint);
                _yPos += 16;

                foreach (var song in songs)
                {
                    string duetSuffix = !string.IsNullOrWhiteSpace(song.DuetPartnerName) && song.DuetPartnerName != "None"
                        ? $" (with {song.DuetPartnerName})"
                        : "";
                    string detail = $"Round {song.Round}: {Truncate(song.SongTitle, 40)} - {Truncate(song.ArtistName, 30)}{duetSuffix}";
                    DrawText($"  • {detail}", _margin + 15, _yPos, SongFont, DimGrayPaint);
                    _yPos += 14;
                }

                // Add small space between singers
                _yPos += 8;
            }

            public void DrawSummary(
                int totalKaraokeSingers, int activeKaraokeSingers, int inactiveKaraokeSingers, int totalKaraokeSongs,
                int totalMusicRequesters, int totalMusicTracks)
            {
                const float height = 60f;
                if (_yPos + height > _pageHeight - _margin)
                {
                    AddNewPage();
                }

                _canvas.DrawLine(_margin, _yPos, _pageWidth - _margin, _yPos, DarkGrayStrokePaint);
                _yPos += 10;

                string karaokeSummary = $"Karaoke: {totalKaraokeSingers} Singers ({activeKaraokeSingers} Active, {inactiveKaraokeSingers} Inactive)  |  Total Songs Sung: {totalKaraokeSongs}";
                DrawText(karaokeSummary, _margin, _yPos, SummaryFont, BlackPaint);
                _yPos += 16;

                string musicSummary = $"Music Requests: {totalMusicRequesters} Requesters  |  Total Tracks Played: {totalMusicTracks}";
                DrawText(musicSummary, _margin, _yPos, SummaryFont, BlackPaint);
                _yPos += 20;
            }

            public void Dispose()
            {
                if (_hasOpenPage)
                {
                    _document.EndPage();
                    _hasOpenPage = false;
                }
            }
        }
    }
}
