// Edited on Nov 13, 2026 @ 16:06:00 -> Add SaveReportCsv to SavedRotationReportReader so both apps can write and read saved rotation lists
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Lyracist.Shared
{
    /// <summary>
    /// Represents a single row in a saved rotation report CSV file.
    /// </summary>
    public sealed class SavedRotationRow
    {
        public int RowNumber { get; init; }
        public string SingerName { get; init; } = string.Empty;
        public string DuetPartnerName { get; init; } = string.Empty;
        public string EntryType { get; init; } = "Karaoke";
        public int TotalCompletedSongs { get; init; }
        public int? Round { get; init; }
        public string SongTitle { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public DateTime? Timestamp { get; init; }

        public bool IsMusic => string.Equals(EntryType, "Music", StringComparison.OrdinalIgnoreCase);
        public bool HasPerformance => Round.HasValue || !string.IsNullOrWhiteSpace(SongTitle);
        public bool HasDuetPartner => !string.IsNullOrWhiteSpace(DuetPartnerName) && !string.Equals(DuetPartnerName, "None", StringComparison.OrdinalIgnoreCase);
        public string RoundDisplay => Round.HasValue ? $"Round {Round.Value}" : "Queued";
        public string TimestampDisplay => Timestamp.HasValue ? Timestamp.Value.ToString("h:mm tt", CultureInfo.CurrentCulture) : "—";
        public string SongDisplay => string.IsNullOrWhiteSpace(SongTitle)
            ? "(In rotation — 0 completed songs)"
            : string.IsNullOrWhiteSpace(Artist)
                ? SongTitle
                : $"{SongTitle} — {Artist}";
        public string DuetDisplay => HasDuetPartner ? $"with {DuetPartnerName}" : string.Empty;
    }

    /// <summary>
    /// Aggregated summary of a singer's performances within a saved rotation report.
    /// </summary>
    public sealed class SavedRotationSingerGroup
    {
        public int OrderIndex { get; init; }
        public string SingerName { get; init; } = string.Empty;
        public string EntryType { get; init; } = "Karaoke";
        public int CompletedSongCount { get; init; }
        public List<SavedRotationRow> Performances { get; init; } = [];

        public bool IsMusic => string.Equals(EntryType, "Music", StringComparison.OrdinalIgnoreCase);
        public bool HasPerformances => Performances.Count > 0;
        public string CountBadgeText => CompletedSongCount == 1 ? "1 song" : $"{CompletedSongCount} songs";
        public string SummaryLine => Performances.Count == 0
            ? "Queued in rotation (0 completed songs)"
            : string.Join("  •  ", Performances.Select(p =>
                $"R{p.Round ?? 1}: {p.SongTitle}{(string.IsNullOrWhiteSpace(p.Artist) ? "" : $" ({p.Artist})")}{(p.HasDuetPartner ? $" [w/ {p.DuetPartnerName}]" : "")}"));
    }

    /// <summary>
    /// Metadata and parsed contents of a saved rotation report (.csv and optional companion .pdf / .eml).
    /// </summary>
    public sealed class SavedRotationReportItem
    {
        public string CsvFilePath { get; init; } = string.Empty;
        public string? PdfFilePath { get; init; }
        public string? EmlFilePath { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string BaseName { get; init; } = string.Empty;
        public string VenueName { get; init; } = string.Empty;
        public DateTime ReportDate { get; init; }
        public DateTime LastModified { get; init; }
        public int KaraokeSingerCount { get; init; }
        public int ActiveKaraokeSingerCount { get; init; }
        public int TotalKaraokeSongs { get; init; }
        public int MusicRequesterCount { get; init; }
        public int TotalMusicTracks { get; init; }
        public List<SavedRotationRow> Rows { get; init; } = [];
        public List<SavedRotationSingerGroup> SingerGroups { get; init; } = [];

        public bool HasCsv => !string.IsNullOrWhiteSpace(CsvFilePath) && File.Exists(CsvFilePath);
        public bool HasPdf => !string.IsNullOrWhiteSpace(PdfFilePath) && File.Exists(PdfFilePath);
        public bool HasEml => !string.IsNullOrWhiteSpace(EmlFilePath) && File.Exists(EmlFilePath);

        public string DisplayTitle => string.IsNullOrWhiteSpace(VenueName) ? BaseName : VenueName;
        public string DisplayDate => ReportDate.ToString("ddd, MMM d, yyyy", CultureInfo.CurrentCulture);
        public string DisplayShortDate => ReportDate.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
        public string DisplaySummary => $"{KaraokeSingerCount} singer{(KaraokeSingerCount == 1 ? "" : "s")} · {TotalKaraokeSongs} song{(TotalKaraokeSongs == 1 ? "" : "s")}" +
                                        (TotalMusicTracks > 0 ? $" · {TotalMusicTracks} music" : string.Empty);
        public string FormatsBadge => HasCsv && HasPdf ? "CSV + PDF" : (HasPdf ? "PDF" : "CSV");
    }

    /// <summary>
    /// Scans the shared <see cref="Globals.KSRotationReportsDir"/> directory and parses saved rotation report files.
    /// </summary>
    public static class SavedRotationReportReader
    {
        private static readonly string[] DateFormats =
        [
            "MMMddyy",
            "yyyy-MM-dd",
            "yyyyMMdd_HHmmss",
            "yyyyMMdd",
            "MMddyyyy",
            "MM-dd-yyyy"
        ];

        /// <summary>
        /// Loads all saved rotation reports from the specified directory (defaults to <see cref="Globals.KSRotationReportsDir"/>),
        /// sorted newest-first.
        /// </summary>
        public static List<SavedRotationReportItem> LoadAllReports(string? directoryPath = null)
        {
            string reportsDir = string.IsNullOrWhiteSpace(directoryPath)
                ? Globals.KSRotationReportsDir
                : directoryPath;

            var results = new List<SavedRotationReportItem>();
            if (!Directory.Exists(reportsDir))
            {
                return results;
            }

            try
            {
                var csvFiles = Directory.GetFiles(reportsDir, "*.csv", SearchOption.TopDirectoryOnly);
                var handledBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string csvPath in csvFiles)
                {
                    var item = LoadFromFile(csvPath);
                    if (item != null)
                    {
                        results.Add(item);
                        handledBaseNames.Add(item.BaseName);
                    }
                }

                // Also surface standalone PDF reports that might not have a companion CSV file
                var pdfFiles = Directory.GetFiles(reportsDir, "*.pdf", SearchOption.TopDirectoryOnly);
                foreach (string pdfPath in pdfFiles)
                {
                    string baseName = Path.GetFileNameWithoutExtension(pdfPath);
                    if (handledBaseNames.Contains(baseName))
                    {
                        continue;
                    }

                    var item = LoadFromPdfOnly(pdfPath);
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.LogError("SavedRotationReportReader", "LoadAllReports", ex);
            }

            return [.. results
                .OrderByDescending(r => r.ReportDate)
                .ThenByDescending(r => r.LastModified)
                .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Loads and parses a single saved rotation report from a <c>.csv</c> or <c>.pdf</c> file path.
        /// </summary>
        public static SavedRotationReportItem? LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            string ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                string companionCsv = Path.ChangeExtension(filePath, ".csv");
                if (File.Exists(companionCsv))
                {
                    filePath = companionCsv;
                }
                else
                {
                    return LoadFromPdfOnly(filePath);
                }
            }

            try
            {
                var fileInfo = new FileInfo(filePath);
                string dir = fileInfo.DirectoryName ?? Globals.KSRotationReportsDir;
                string baseName = Path.GetFileNameWithoutExtension(fileInfo.Name);

                string candidatePdf = Path.Combine(dir, $"{baseName}.pdf");
                string? pdfPath = File.Exists(candidatePdf) ? candidatePdf : null;

                // Handle legacy timestamped pairs where PDF and CSV differed by 1 second (e.g. Rotation_20261022_085346.pdf / 085347.csv)
                if (pdfPath == null && baseName.StartsWith("Rotation_", StringComparison.OrdinalIgnoreCase))
                {
                    pdfPath = FindClosestRotationPdf(dir, baseName, fileInfo.LastWriteTime);
                }

                string candidateEml = Path.Combine(dir, $"{baseName}.eml");
                string? emlPath = File.Exists(candidateEml) ? candidateEml : null;

                var (venueName, parsedDate) = ParseVenueAndDateFromBaseName(baseName, fileInfo.LastWriteTime);
                var rows = ParseCsvRows(filePath);

                // If any row has a valid timestamp, refine the report date from the earliest performance
                var earliestTimestamp = rows
                    .Where(r => r.Timestamp.HasValue)
                    .Select(r => r.Timestamp!.Value.Date)
                    .DefaultIfEmpty(parsedDate)
                    .Min();

                DateTime reportDate = parsedDate != DateTime.MinValue ? parsedDate : earliestTimestamp;

                var singerGroups = BuildSingerGroups(rows);
                var karaokeGroups = singerGroups.Where(g => !g.IsMusic).ToList();
                var musicGroups = singerGroups.Where(g => g.IsMusic).ToList();

                return new SavedRotationReportItem
                {
                    CsvFilePath = filePath,
                    PdfFilePath = pdfPath,
                    EmlFilePath = emlPath,
                    FileName = fileInfo.Name,
                    BaseName = baseName,
                    VenueName = venueName,
                    ReportDate = reportDate,
                    LastModified = fileInfo.LastWriteTime,
                    KaraokeSingerCount = karaokeGroups.Count,
                    ActiveKaraokeSingerCount = karaokeGroups.Count(g => g.CompletedSongCount > 0),
                    TotalKaraokeSongs = karaokeGroups.Sum(g => g.Performances.Count),
                    MusicRequesterCount = musicGroups.Count,
                    TotalMusicTracks = musicGroups.Sum(g => g.Performances.Count),
                    Rows = rows,
                    SingerGroups = singerGroups
                };
            }
            catch (Exception ex)
            {
                Globals.LogError("SavedRotationReportReader", $"LoadFromFile({filePath})", ex);
                return null;
            }
        }

        private static SavedRotationReportItem? LoadFromPdfOnly(string pdfPath)
        {
            try
            {
                var fileInfo = new FileInfo(pdfPath);
                string dir = fileInfo.DirectoryName ?? Globals.KSRotationReportsDir;
                string baseName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                string candidateEml = Path.Combine(dir, $"{baseName}.eml");
                string? emlPath = File.Exists(candidateEml) ? candidateEml : null;
                var (venueName, reportDate) = ParseVenueAndDateFromBaseName(baseName, fileInfo.LastWriteTime);

                return new SavedRotationReportItem
                {
                    CsvFilePath = string.Empty,
                    PdfFilePath = pdfPath,
                    EmlFilePath = emlPath,
                    FileName = fileInfo.Name,
                    BaseName = baseName,
                    VenueName = venueName,
                    ReportDate = reportDate,
                    LastModified = fileInfo.LastWriteTime
                };
            }
            catch (Exception ex)
            {
                Globals.LogError("SavedRotationReportReader", $"LoadFromPdfOnly({pdfPath})", ex);
                return null;
            }
        }

        private static string? FindClosestRotationPdf(string dir, string csvBaseName, DateTime csvTime)
        {
            try
            {
                string prefix = csvBaseName.Length >= 17 ? csvBaseName[..17] : "Rotation_";
                var candidates = Directory.GetFiles(dir, $"{prefix}*.pdf", SearchOption.TopDirectoryOnly);
                return candidates
                    .Select(p => new FileInfo(p))
                    .Where(fi => Math.Abs((fi.LastWriteTime - csvTime).TotalSeconds) <= 10)
                    .OrderBy(fi => Math.Abs((fi.LastWriteTime - csvTime).TotalSeconds))
                    .FirstOrDefault()?.FullName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the venue name and date from report base filenames such as:
        /// "Plan B - Oct2226", "Plan B - Oct2226 (2)", "Plan B - 2026-10-22", "Oct2226", or "Rotation_20261022_085347".
        /// </summary>
        public static (string VenueName, DateTime ReportDate) ParseVenueAndDateFromBaseName(string baseName, DateTime fallbackDate)
        {
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return ("Saved Rotation", fallbackDate.Date);
            }

            string clean = baseName.Trim();

            // Strip trailing duplicate suffix like " (2)" or " (3)"
            int parenIdx = clean.LastIndexOf(" (", StringComparison.Ordinal);
            if (parenIdx > 0 && clean.EndsWith(')'))
            {
                string inside = clean[(parenIdx + 2)..^1];
                if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    clean = clean[..parenIdx].TrimEnd();
                }
            }

            // Case 1: "[Venue] - [Date]"
            int dashIdx = clean.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dashIdx > 0)
            {
                string venuePart = clean[..dashIdx].Trim();
                string datePart = clean[(dashIdx + 3)..].Trim();
                if (TryParseReportDateToken(datePart, out DateTime parsedDate))
                {
                    return (string.IsNullOrWhiteSpace(venuePart) ? "Saved Rotation" : venuePart, parsedDate);
                }
            }

            // Case 2: "Rotation_yyyyMMdd_HHmmss"
            if (clean.StartsWith("Rotation_", StringComparison.OrdinalIgnoreCase))
            {
                string token = clean["Rotation_".Length..].Trim();
                if (TryParseReportDateToken(token, out DateTime parsedDate))
                {
                    return ("Saved Rotation", parsedDate);
                }
            }

            // Case 3: Pure date filename e.g. "Oct2226" or "2026-10-22"
            if (TryParseReportDateToken(clean, out DateTime directDate))
            {
                return ("Saved Rotation", directDate);
            }

            return (clean, fallbackDate.Date);
        }

        private static bool TryParseReportDateToken(string token, out DateTime date)
        {
            if (DateTime.TryParseExact(
                    token,
                    DateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out DateTime dt))
            {
                date = dt.Date;
                return true;
            }

            date = default;
            return false;
        }

        /// <summary>
        /// Parses the rows of a rotation report CSV file.
        /// Expected header: <c>Singer,Duet Partner,Type,Total Completed Songs,Round,Song Title,Artist,Timestamp</c>.
        /// </summary>
        public static List<SavedRotationRow> ParseCsvRows(string csvFilePath)
        {
            var rows = new List<SavedRotationRow>();
            if (!File.Exists(csvFilePath))
            {
                return rows;
            }

            string[] lines = File.ReadAllLines(csvFilePath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return rows;
            }

            int rowNumber = 1;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> fields = ParseCsvLine(line);
                if (fields.Count == 0)
                {
                    continue;
                }

                string singer = UnescapeFormulaPrefix(GetField(fields, 0));
                if (string.IsNullOrWhiteSpace(singer))
                {
                    continue;
                }

                string duetPartner = UnescapeFormulaPrefix(GetField(fields, 1));
                string type = GetField(fields, 2);
                if (string.IsNullOrWhiteSpace(type))
                {
                    type = "Karaoke";
                }

                _ = int.TryParse(GetField(fields, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out int totalCompleted);

                int? round = null;
                if (int.TryParse(GetField(fields, 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRound))
                {
                    round = parsedRound;
                }

                string songTitle = UnescapeFormulaPrefix(GetField(fields, 5));
                string artist = UnescapeFormulaPrefix(GetField(fields, 6));

                DateTime? timestamp = null;
                string tsText = GetField(fields, 7);
                if (!string.IsNullOrWhiteSpace(tsText) &&
                    DateTime.TryParse(tsText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsedTs))
                {
                    timestamp = parsedTs;
                }

                rows.Add(new SavedRotationRow
                {
                    RowNumber = rowNumber++,
                    SingerName = singer,
                    DuetPartnerName = duetPartner,
                    EntryType = type,
                    TotalCompletedSongs = totalCompleted,
                    Round = round,
                    SongTitle = songTitle,
                    Artist = artist,
                    Timestamp = timestamp
                });
            }

            return rows;
        }

        private static List<SavedRotationSingerGroup> BuildSingerGroups(List<SavedRotationRow> rows)
        {
            var groups = new List<SavedRotationSingerGroup>();
            var lookup = new Dictionary<(string Name, bool IsMusic), SavedRotationSingerGroup>();

            foreach (var row in rows)
            {
                var key = (row.SingerName.ToUpperInvariant(), row.IsMusic);
                if (!lookup.TryGetValue(key, out var group))
                {
                    group = new SavedRotationSingerGroup
                    {
                        OrderIndex = groups.Count + 1,
                        SingerName = row.SingerName,
                        EntryType = row.EntryType,
                        CompletedSongCount = row.TotalCompletedSongs
                    };
                    lookup[key] = group;
                    groups.Add(group);
                }

                if (row.HasPerformance)
                {
                    group.Performances.Add(row);
                }
            }

            return groups;
        }

        private static string GetField(List<string> fields, int index)
        {
            return index >= 0 && index < fields.Count ? fields[index].Trim() : string.Empty;
        }

        private static string UnescapeFormulaPrefix(string value)
        {
            if (value.Length >= 2 && value[0] == '\'' && value[1] is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                return value[1..];
            }
            return value;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>(8);
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            result.Add(sb.ToString());
            return result;
        }

        /// <summary>
        /// Writes a saved rotation report <c>.csv</c> file into <see cref="Globals.KSRotationReportsDir"/>
        /// using the standard 8-column format (<c>Singer,Duet Partner,Type,Total Completed Songs,Round,Song Title,Artist,Timestamp</c>).
        /// </summary>
        public static string SaveReportCsv(
            string? venueName,
            IEnumerable<(string Name, bool IsMusic)> activeSingers,
            IEnumerable<(string SingerName, string DuetPartnerName, string SongTitle, string Artist, DateTime Timestamp, bool IsMusic)> performances)
        {
            Directory.CreateDirectory(Globals.KSRotationReportsDir);

            var perfList = performances.ToList();
            DateTime perfDate = perfList.Count > 0
                ? perfList.Min(p => p.Timestamp).Date
                : DateTime.Today;

            string baseName = BuildReportBaseFileName(venueName, perfDate);
            string csvPath = Path.Combine(Globals.KSRotationReportsDir, $"{baseName}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("Singer,Duet Partner,Type,Total Completed Songs,Round,Song Title,Artist,Timestamp");

            var orderedSingers = new List<(string Name, bool IsMusic)>();
            var seen = new HashSet<(string Name, bool IsMusic)>();

            foreach (var s in activeSingers)
            {
                if (string.IsNullOrWhiteSpace(s.Name)) continue;
                var key = (s.Name.Trim().ToUpperInvariant(), s.IsMusic);
                if (seen.Add(key))
                {
                    orderedSingers.Add((s.Name.Trim(), s.IsMusic));
                }
            }

            foreach (var p in perfList)
            {
                if (string.IsNullOrWhiteSpace(p.SingerName)) continue;
                var key = (p.SingerName.Trim().ToUpperInvariant(), p.IsMusic);
                if (seen.Add(key))
                {
                    orderedSingers.Add((p.SingerName.Trim(), p.IsMusic));
                }
            }

            foreach (var isMusicPass in new[] { false, true })
            {
                string typeLabel = isMusicPass ? "Music" : "Karaoke";
                foreach (var (singerName, _) in orderedSingers.Where(s => s.IsMusic == isMusicPass))
                {
                    var singerPerfs = perfList
                        .Where(p => p.IsMusic == isMusicPass && string.Equals(p.SingerName.Trim(), singerName, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.Timestamp)
                        .ToList();

                    if (singerPerfs.Count > 0)
                    {
                        for (int i = 0; i < singerPerfs.Count; i++)
                        {
                            var perf = singerPerfs[i];
                            int round = i + 1;
                            sb.AppendLine(
                                CultureInfo.InvariantCulture,
                                $"\"{EscapeCsvCell(singerName)}\",\"{EscapeCsvCell(perf.DuetPartnerName)}\",\"{typeLabel}\",{singerPerfs.Count},{round},\"{EscapeCsvCell(perf.SongTitle)}\",\"{EscapeCsvCell(perf.Artist)}\",\"{perf.Timestamp:yyyy-MM-dd HH:mm:ss}\"");
                        }
                    }
                    else
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"\"{EscapeCsvCell(singerName)}\",\"\",\"{typeLabel}\",0,,,");
                    }
                }
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
            return csvPath;
        }

        private static string BuildReportBaseFileName(string? venueName, DateTime perfDate)
        {
            string dateStr = perfDate.ToString("MMMddyy", CultureInfo.InvariantCulture);
            string sanitizedVenue = SanitizeFileNamePart(venueName);
            string baseName = string.IsNullOrWhiteSpace(sanitizedVenue)
                ? dateStr
                : $"{sanitizedVenue} - {dateStr}";

            string candidate = baseName;
            int counter = 2;
            while (File.Exists(Path.Combine(Globals.KSRotationReportsDir, $"{candidate}.pdf")) ||
                   File.Exists(Path.Combine(Globals.KSRotationReportsDir, $"{candidate}.csv")))
            {
                candidate = $"{baseName} ({counter++})";
            }

            return candidate;
        }

        private static string SanitizeFileNamePart(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                if (invalidChars.Contains(c) || c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                {
                    continue;
                }
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private static string EscapeCsvCell(string? val)
        {
            if (string.IsNullOrEmpty(val)) return string.Empty;
            char first = val[0];
            if (first is '=' or '+' or '-' or '@' or '\t' or '\r')
            {
                val = "'" + val;
            }
            return val.Replace("\"", "\"\"", StringComparison.Ordinal);
        }
    }
}
