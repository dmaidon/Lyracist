// Edited on Aug 25, 2026 @ 06:39:00 -> Fix RCS1118 const variable in DrawSummary
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

            using var stream = File.Create(filePath);
            using var document = SKDocument.CreatePdf(stream, metadata);
            using var layout = new PdfLayoutManager(document);
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

            layout.Finish();
            document.Close();
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
            private const float PageWidth = 612f;  // US Letter in points
            private const float PageHeight = 792f;

            private readonly SKDocument _document;
            private SKCanvas _canvas = null!;
            private bool _pageOpen;
            private float _yPos;
            private readonly float _margin = 40;
            private readonly float _pageWidth = PageWidth;
            private readonly float _pageHeight = PageHeight;

            // Typefaces, fonts and paints are reused across every page/row; build them once per report.
            private readonly SKTypeface _boldTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            private readonly SKTypeface _regularTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);
            private readonly SKFont _titleFont;
            private readonly SKFont _sectionFont;
            private readonly SKFont _metaFont;
            private readonly SKFont _singerFont;
            private readonly SKFont _songFont;
            private readonly SKFont _summaryFont;

            private readonly SKPaint _darkSlateGray = new() { Color = SKColors.DarkSlateGray, IsAntialias = true };
            private readonly SKPaint _dimGray = new() { Color = SKColors.DimGray, IsAntialias = true };
            private readonly SKPaint _gray = new() { Color = SKColors.Gray, IsAntialias = true };
            private readonly SKPaint _black = new() { Color = SKColors.Black, IsAntialias = true };
            private readonly SKPaint _ghostWhiteFill = new() { Color = SKColors.GhostWhite, Style = SKPaintStyle.Fill };
            private readonly SKPaint _darkGrayStroke = new() { Color = SKColors.DarkGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            private readonly SKPaint _lightGrayStroke = new() { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

            public PdfLayoutManager(SKDocument document)
            {
                _document = document;
                _titleFont = new SKFont(_boldTypeface, 18);
                _sectionFont = new SKFont(_boldTypeface, 14);
                _metaFont = new SKFont(_regularTypeface, 10);
                _singerFont = new SKFont(_boldTypeface, 11);
                _songFont = new SKFont(_regularTypeface, 9);
                _summaryFont = new SKFont(_boldTypeface, 10);
                AddNewPage();
            }

            public void AddNewPage()
            {
                if (_pageOpen)
                {
                    _document.EndPage();
                }

                _canvas = _document.BeginPage(_pageWidth, _pageHeight);
                _pageOpen = true;
                _yPos = _margin;
            }

            public void Finish()
            {
                if (_pageOpen)
                {
                    _document.EndPage();
                    _pageOpen = false;
                }
            }

            public void Dispose()
            {
                _titleFont.Dispose();
                _sectionFont.Dispose();
                _metaFont.Dispose();
                _singerFont.Dispose();
                _songFont.Dispose();
                _summaryFont.Dispose();
                _boldTypeface.Dispose();
                _regularTypeface.Dispose();
                _darkSlateGray.Dispose();
                _dimGray.Dispose();
                _gray.Dispose();
                _black.Dispose();
                _ghostWhiteFill.Dispose();
                _darkGrayStroke.Dispose();
                _lightGrayStroke.Dispose();
            }

            // Draws text with its top edge at y (matching a top-aligned text layout). For right alignment, x is the right edge.
            private void DrawText(string text, SKFont font, SKPaint paint, float x, float y, SKTextAlign align = SKTextAlign.Left)
            {
                _canvas.DrawText(text, x, y - font.Metrics.Ascent, align, font, paint);
            }

            public void DrawHeader(string title, string venue, string date, string time)
            {
                // Header Background block
                var headerRect = new SKRect(_margin, _yPos, _pageWidth - _margin, _yPos + 55);
                _canvas.DrawRect(headerRect, _ghostWhiteFill);
                _canvas.DrawRect(headerRect, _darkGrayStroke);

                // Draw title
                DrawText(title, _titleFont, _darkSlateGray, _margin + 15, _yPos + 8);

                // Draw metadata
                DrawText($"Generated: {date} {time}", _metaFont, _dimGray, _margin + 15, _yPos + 33);
                DrawText($"Venue: {venue}", _metaFont, _dimGray, _pageWidth - _margin - 15, _yPos + 33, SKTextAlign.Right);

                _yPos += 75;
            }

            public void DrawSectionHeader(string title)
            {
                if (_yPos + 35 > _pageHeight - _margin)
                {
                    AddNewPage();
                }
                DrawText(title, _sectionFont, _darkSlateGray, _margin, _yPos);
                _yPos += 18;
                _canvas.DrawLine(_margin, _yPos, _pageWidth - _margin, _yPos, _lightGrayStroke);
                _yPos += 10;
            }

            public void DrawEmptySectionMessage(string message)
            {
                if (_yPos + 20 > _pageHeight - _margin)
                {
                    AddNewPage();
                }
                DrawText(message, _songFont, _gray, _margin + 15, _yPos);
                _yPos += 22;
            }

            public void DrawRow(string singer, int songCount, List<SongPerformance> songs)
            {
                double rowHeight = 20 + (songs.Count * 14);
                if (_yPos + rowHeight > _pageHeight - _margin)
                {
                    AddNewPage();
                }

                // Draw singer name & count
                DrawText($"{singer} ({songCount} song{(songCount == 1 ? "" : "s")} completed)", _singerFont, _black, _margin, _yPos);
                _yPos += 16;

                foreach (var song in songs)
                {
                    string duetSuffix = !string.IsNullOrWhiteSpace(song.DuetPartnerName) && song.DuetPartnerName != "None"
                        ? $" (with {song.DuetPartnerName})"
                        : "";
                    string detail = $"Round {song.Round}: {Truncate(song.SongTitle, 40)} - {Truncate(song.ArtistName, 30)}{duetSuffix}";
                    DrawText($"  \u2022 {detail}", _songFont, _dimGray, _margin + 15, _yPos);
                    _yPos += 14;
                }

                // Add small space between singers
                _yPos += 8;
            }

            public void DrawSummary(
                int totalKaraokeSingers, int activeKaraokeSingers, int inactiveKaraokeSingers, int totalKaraokeSongs,
                int totalMusicRequesters, int totalMusicTracks)
            {
                const double height = 60;
                if (_yPos + height > _pageHeight - _margin)
                {
                    AddNewPage();
                }

                _canvas.DrawLine(_margin, _yPos, _pageWidth - _margin, _yPos, _darkGrayStroke);
                _yPos += 10;

                string karaokeSummary = $"Karaoke: {totalKaraokeSingers} Singers ({activeKaraokeSingers} Active, {inactiveKaraokeSingers} Inactive)  |  Total Songs Sung: {totalKaraokeSongs}";
                DrawText(karaokeSummary, _summaryFont, _black, _margin, _yPos);
                _yPos += 16;

                string musicSummary = $"Music Requests: {totalMusicRequesters} Requesters  |  Total Tracks Played: {totalMusicTracks}";
                DrawText(musicSummary, _summaryFont, _black, _margin, _yPos);
                _yPos += 20;
            }
        }
    }
}
