// Created on Jul 27, 2026 @ 10:00:00 -> Split out of RotationReportService so KSRotation.Maui can
// reuse the PDF/CSV generation without the desktop-only .eml-draft delivery mechanism.
using KSRotation.Models;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Globalization;
using System.IO;
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
        internal sealed record ReportRow(string SingerName, List<SongPerformance> Performances, bool IsInactive);

        /// <summary>
        /// Builds the ordered set of report rows from the queue and history. Performances are matched to singers by
        /// stable <see cref="SingerEntry.Id"/> first, then by name for legacy entries (Id == Guid.Empty). Singers who
        /// have history but are no longer in the queue are appended afterward.
        /// </summary>
        internal static List<ReportRow> BuildReportRows(List<SingerEntry> singers, List<SongPerformance> history)
        {
            var performanceGroupsBySingerId = history
                .Where(h => h.SingerId != Guid.Empty)
                .GroupBy(h => h.SingerId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Round).ToList());

            var legacyPerformanceGroupsByName = history
                .Where(h => h.SingerId == Guid.Empty)
                .GroupBy(h => h.SingerName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Round).ToList(), StringComparer.OrdinalIgnoreCase);

            var rows = new List<ReportRow>(singers.Count);

            // Singers currently in the queue (preserve queue order).
            foreach (var singer in singers)
            {
                List<SongPerformance> perfs = [];
                if (performanceGroupsBySingerId.TryGetValue(singer.Id, out var byIdPerfs))
                {
                    perfs = byIdPerfs;
                }
                else if (legacyPerformanceGroupsByName.TryGetValue(singer.Name, out var byNamePerfs))
                {
                    perfs = byNamePerfs;
                }

                rows.Add(new ReportRow(singer.Name, perfs, singer.IsInactive));
            }

            // Singers who have performances but are no longer in the queue (counted as active).
            var queueSingerIds = new HashSet<Guid>(singers.Select(s => s.Id));
            var queueNames = new HashSet<string>(singers.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var group in performanceGroupsBySingerId)
            {
                if (!queueSingerIds.Contains(group.Key))
                {
                    string singerName = group.Value.FirstOrDefault()?.SingerName ?? "Unknown Singer";
                    rows.Add(new ReportRow(singerName, group.Value, IsInactive: false));
                }
            }

            foreach (var group in legacyPerformanceGroupsByName)
            {
                if (!queueNames.Contains(group.Key))
                {
                    rows.Add(new ReportRow(group.Key, group.Value, IsInactive: false));
                }
            }

            return rows;
        }

        internal static string GenerateCsv(
            List<SingerEntry> singers,
            List<SongPerformance> history)
        {
            StringBuilder sb = new();
            sb.AppendLine("Singer,Total Completed Songs,Round,Song Title,Artist,Timestamp");

            foreach (var row in BuildReportRows(singers, history))
            {
                if (row.Performances.Count > 0)
                {
                    foreach (var perf in row.Performances)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsv(row.SingerName)}\",{row.Performances.Count},{perf.Round},\"{EscapeCsv(perf.SongTitle)}\",\"{EscapeCsv(perf.ArtistName)}\",\"{perf.Timestamp:yyyy-MM-dd HH:mm:ss}\"");
                    }
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsv(row.SingerName)}\",0,,,");
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

            int totalSingers = rows.Count;
            int inactiveSingers = rows.Count(r => r.IsInactive);
            int activeSingers = totalSingers - inactiveSingers;
            int totalSongs = history.Count;

            foreach (var row in rows)
            {
                layout.DrawRow(row.SingerName, row.Performances.Count, row.Performances);
            }

            layout.DrawSummary(totalSingers, activeSingers, inactiveSingers, totalSongs);

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
            private readonly XStringFormat _rightAlign = new() { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near };

            // Fonts are immutable and reused across every page/row; build them once instead of per-row.
            private static readonly XFont TitleFont = new("Arial", 18, XFontStyleEx.Bold);
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
                _gfx.DrawString($"Venue: {venue}", MetaFont, XBrushes.DimGray, new XRect(_pageWidth - _margin - 215, _yPos + 33, 200, 15), _rightAlign);

                _yPos += 75;
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
                    string detail = $"Round {song.Round}: {Truncate(song.SongTitle, 40)} - {Truncate(song.ArtistName, 30)}";
                    _gfx.DrawString($"  • {detail}", SongFont, XBrushes.DimGray, _margin + 15, _yPos, _leftAlign);
                    _yPos += 14;
                }

                // Add small space between singers
                _yPos += 8;
            }

            public void DrawSummary(int totalSingers, int activeSingers, int inactiveSingers, int totalSongs)
            {
                double height = 50;
                if (_yPos + height > _pageHeight - _margin)
                {
                    AddNewPage();
                }

                _gfx.DrawLine(XPens.DarkGray, _margin, _yPos, _pageWidth - _margin, _yPos);
                _yPos += 10;

                string summary = $"Total Singers: {totalSingers}  |  Active: {activeSingers}  |  Inactive: {inactiveSingers}  |  Total Songs Sung: {totalSongs}";
                _gfx.DrawString(summary, SummaryFont, XBrushes.Black, _margin, _yPos, _leftAlign);
                _yPos += 20;
            }
        }
    }
}
