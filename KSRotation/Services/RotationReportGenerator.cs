// Edited on Aug 25, 2026 @ 06:39:00 -> Fix RCS1118 const variable in DrawSummary
using KSRotation.Models;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
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
        static RotationReportGenerator()
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    PdfSharp.Fonts.GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                }
            }
            catch
            {
                // Guard against multiple initialization attempts or platform errors
            }
        }

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
            PdfDocument document = new();
            document.Info.Title = $"Karaoke Rotation Report - {venueName}";
            document.Info.Author = "Karaoke Singer Rotation";

            var layout = new PdfLayoutManager(document);
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

            Directory.CreateDirectory(ReportDirectoryPath);
            string fileName = $"Rotation_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(ReportDirectoryPath, fileName);
            document.Save(filePath);
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

        private class PdfLayoutManager
        {
            private readonly PdfDocument _document;
            private PdfPage _currentPage = null!;
            private XGraphics _gfx = null!;
            private double _yPos;
            private readonly double _margin = 40;
            private readonly double _pageWidth;
            private readonly double _pageHeight;

            private readonly XStringFormat _leftAlign = new() { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Near };

            // Fonts are immutable and reused across every page/row; build them once instead of per-row.
            private static readonly XFont TitleFont = new("Arial", 18, XFontStyleEx.Bold);
            private static readonly XFont SectionFont = new("Arial", 14, XFontStyleEx.Bold);
            private static readonly XFont MetaFont = new("Arial", 10, XFontStyleEx.Regular);
            private static readonly XFont SingerFont = new("Arial", 11, XFontStyleEx.Bold);
            private static readonly XFont SongFont = new("Arial", 9, XFontStyleEx.Regular);
            private static readonly XFont SummaryFont = new("Arial", 10, XFontStyleEx.Bold);

            public PdfLayoutManager(PdfDocument document)
            {
                _document = document;
                AddNewPage();
                _pageWidth = _currentPage.Width.Point;
                _pageHeight = _currentPage.Height.Point;
            }

            public void AddNewPage()
            {
                _currentPage = _document.AddPage();
                _currentPage.Size = PageSize.Letter;
                _gfx = XGraphics.FromPdfPage(_currentPage);
                _yPos = _margin;
            }

            public void DrawHeader(string title, string venue, string date, string time)
            {
                // Header Background block
                XRect headerRect = new(_margin, _yPos, _pageWidth - (2 * _margin), 55);
                _gfx.DrawRectangle(XPens.DarkGray, XBrushes.GhostWhite, headerRect);

                // Draw title
                _gfx.DrawString(title, TitleFont, XBrushes.DarkSlateGray, new XRect(_margin + 15, _yPos + 8, _pageWidth - (2 * _margin) - 30, 25), _leftAlign);

                // Draw metadata
                _gfx.DrawString($"Generated: {date} {time}", MetaFont, XBrushes.DimGray, new XRect(_margin + 15, _yPos + 33, _pageWidth - (2 * _margin) - 30, 15), _leftAlign);
                _gfx.DrawString($"Venue: {venue}", MetaFont, XBrushes.DimGray, new XRect(_pageWidth - _margin - 215, _yPos + 33, 200, 15), new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near });

                _yPos += 75;
            }

            public void DrawSectionHeader(string title)
            {
                if (_yPos + 35 > _pageHeight - _margin)
                {
                    AddNewPage();
                }
                _gfx.DrawString(title, SectionFont, XBrushes.DarkSlateGray, _margin, _yPos, _leftAlign);
                _yPos += 18;
                _gfx.DrawLine(XPens.LightGray, _margin, _yPos, _pageWidth - _margin, _yPos);
                _yPos += 10;
            }

            public void DrawEmptySectionMessage(string message)
            {
                if (_yPos + 20 > _pageHeight - _margin)
                {
                    AddNewPage();
                }
                _gfx.DrawString(message, SongFont, XBrushes.Gray, _margin + 15, _yPos, _leftAlign);
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
                _gfx.DrawString($"{singer} ({songCount} song{(songCount == 1 ? "" : "s")} completed)", SingerFont, XBrushes.Black, _margin, _yPos, _leftAlign);
                _yPos += 16;

                foreach (var song in songs)
                {
                    string duetSuffix = !string.IsNullOrWhiteSpace(song.DuetPartnerName) && song.DuetPartnerName != "None"
                        ? $" (with {song.DuetPartnerName})"
                        : "";
                    string detail = $"Round {song.Round}: {Truncate(song.SongTitle, 40)} - {Truncate(song.ArtistName, 30)}{duetSuffix}";
                    _gfx.DrawString($"  • {detail}", SongFont, XBrushes.DimGray, _margin + 15, _yPos, _leftAlign);
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

                _gfx.DrawLine(XPens.DarkGray, _margin, _yPos, _pageWidth - _margin, _yPos);
                _yPos += 10;

                string karaokeSummary = $"Karaoke: {totalKaraokeSingers} Singers ({activeKaraokeSingers} Active, {inactiveKaraokeSingers} Inactive)  |  Total Songs Sung: {totalKaraokeSongs}";
                _gfx.DrawString(karaokeSummary, SummaryFont, XBrushes.Black, _margin, _yPos, _leftAlign);
                _yPos += 16;

                string musicSummary = $"Music Requests: {totalMusicRequesters} Requesters  |  Total Tracks Played: {totalMusicTracks}";
                _gfx.DrawString(musicSummary, SummaryFont, XBrushes.Black, _margin, _yPos, _leftAlign);
                _yPos += 20;
            }
        }
    }
}
