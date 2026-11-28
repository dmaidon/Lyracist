// Created on Nov 27, 2026 @ 13:17:00 -> Custom Karaoke Track Studio exporter: FFmpeg vocal isolation, binary CDG packet generator, and SkiaSharp MP4 video renderer
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Lyracist.Data.Services;
using SkiaSharp;

namespace Lyracist.Services.Media;

public partial class StudioLyricLine : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartTimeFormatted))]
    private double _startTimeSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EndTimeFormatted))]
    private double _endTimeSeconds;

    [ObservableProperty]
    private string _lyricText = string.Empty;

    [ObservableProperty]
    private string _translationText = string.Empty;

    /// <summary>"Singer 1", "Singer 2", or "Both (Duet)"</summary>
    [ObservableProperty]
    private string _singerAssignment = "Singer 1";

    public string StartTimeFormatted
    {
        get => TimeSpan.FromSeconds(Math.Max(0, StartTimeSeconds)).ToString(@"mm\:ss\.ff");
        set
        {
            if (TryParseTimestamp(value, out double sec))
            {
                StartTimeSeconds = sec;
            }
        }
    }

    public string EndTimeFormatted
    {
        get => TimeSpan.FromSeconds(Math.Max(0, EndTimeSeconds)).ToString(@"mm\:ss\.ff");
        set
        {
            if (TryParseTimestamp(value, out double sec))
            {
                EndTimeSeconds = sec;
            }
        }
    }

    public static bool TryParseTimestamp(string? input, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        string clean = input.Trim().Trim('[', ']');
        if (TimeSpan.TryParseExact(clean, [@"mm\:ss\.ff", @"m\:ss\.ff", @"mm\:ss\.fff", @"m\:ss\.fff", @"mm\:ss\.f", @"mm\:ss", @"m\:ss"], CultureInfo.InvariantCulture, out var ts))
        {
            seconds = ts.TotalSeconds;
            return true;
        }
        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }

    /// <summary>
    /// Parses pasted or imported lyrics (plain text or LRC). LRC header tags such as [ti:] are skipped,
    /// [S2] / [BOTH] prefixes become singer assignments, and "Line | Translation" is split.
    /// </summary>
    public static List<StudioLyricLine> ParseLyrics(string? raw)
    {
        var result = new List<StudioLyricLine>();
        double cursorSec = 4.0;
        foreach (string rawLine in (raw ?? string.Empty).Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            string text = rawLine.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^\[[A-Za-z#]+:[^\]]*\]$")) continue;

            double start = cursorSec;
            if (text.StartsWith('[') && text.IndexOf(']') is int closeIdx and > 1
                && TryParseTimestamp(text[1..closeIdx], out double parsedSec))
            {
                start = parsedSec;
                text = text[(closeIdx + 1)..].Trim();
            }

            string singer = "Singer 1";
            if (text.StartsWith("[S2]", StringComparison.OrdinalIgnoreCase))
            {
                singer = "Singer 2";
                text = text[4..].Trim();
            }
            else if (text.StartsWith("[BOTH]", StringComparison.OrdinalIgnoreCase))
            {
                singer = "Both (Duet)";
                text = text[6..].Trim();
            }

            string translation = string.Empty;
            int pipe = text.IndexOf('|');
            if (pipe >= 0)
            {
                translation = text[(pipe + 1)..].Trim();
                text = text[..pipe].Trim();
            }

            if (string.IsNullOrWhiteSpace(text)) continue;

            result.Add(new StudioLyricLine
            {
                StartTimeSeconds = start,
                EndTimeSeconds = start + 3.5,
                LyricText = text,
                TranslationText = translation,
                SingerAssignment = singer
            });
            cursorSec = start + 4.0;
        }
        return result;
    }

    /// <summary>
    /// Returns copies of the lines re-timed for a tempo change. Timestamps are stamped against the
    /// original audio, so at 80% tempo every line lands later by a factor of 1 / 0.8.
    /// </summary>
    public static List<StudioLyricLine> ScaleForTempo(IEnumerable<StudioLyricLine> lines, int tempoPercent)
    {
        double factor = KaraokeStudioExporter.ClampTempoPercent(tempoPercent) / 100.0;
        return lines.Select(l => new StudioLyricLine
        {
            StartTimeSeconds = l.StartTimeSeconds / factor,
            EndTimeSeconds = l.EndTimeSeconds / factor,
            LyricText = l.LyricText,
            TranslationText = l.TranslationText,
            SingerAssignment = l.SingerAssignment
        }).ToList();
    }
}

public sealed record KaraokeStudioExportOptions
{
    public required string InputAudioPath { get; init; }
    public required string OutputDirectory { get; init; }
    public required string SongTitle { get; init; }
    public required string ArtistName { get; init; }
    /// <summary>0 = Isolate Backing (Remove Lead Vocal), 1 = Guide Vocal at GuideVocalPercent, 2 = Keep Original Audio</summary>
    public int VocalIsolationMode { get; init; }
    public int GuideVocalPercent { get; init; } = 25;
    public int KeySemitones { get; init; }
    public int TempoPercent { get; init; } = 100;
    /// <summary>0 = MP3+G (.mp3 + .cdg), 1 = MP4 16:9 Wide (1280x720), 2 = MP4 9:16 Vertical Social (720x1280)</summary>
    public int ExportFormatIndex { get; init; }
    public bool IncludeInstrumentalCountdowns { get; init; } = true;
    public required IReadOnlyList<StudioLyricLine> Lines { get; init; }
}

public static class KaraokeStudioExporter
{
    private const int CdgWidth = 300;
    private const int CdgHeight = 216;
    private const int CdgPacketSize = 24;
    private const int CdgPacketsPerSecond = 300;

    public const int MinTempoPercent = 50;
    public const int MaxTempoPercent = 150;

    public static int ClampTempoPercent(int tempoPercent) => Math.Clamp(tempoPercent, MinTempoPercent, MaxTempoPercent);

    /// <summary>
    /// Center-channel vocal reduction. 0% guide removes the lead vocal, 100% leaves the original mix,
    /// and values between blend the two.
    /// </summary>
    public static string BuildVocalFilter(int vocalIsolationMode, int guideVocalPercent)
    {
        if (vocalIsolationMode == 2) return string.Empty;
        double guide = vocalIsolationMode == 1 ? Math.Clamp(guideVocalPercent, 0, 100) / 100.0 : 0.0;
        if (guide >= 1.0) return string.Empty;

        if (guide <= 0.0)
        {
            return "pan=stereo|c0=0.5*c0-0.5*c1|c1=0.5*c1-0.5*c0,highpass=f=110,volume=1.35";
        }

        string keep = (0.5 + (0.5 * guide)).ToString("F3", CultureInfo.InvariantCulture);
        string cancel = (0.5 - (0.5 * guide)).ToString("F3", CultureInfo.InvariantCulture);
        string volume = (1.0 + (0.35 * (1.0 - guide))).ToString("F3", CultureInfo.InvariantCulture);
        return $"pan=stereo|c0={keep}*c0-{cancel}*c1|c1={keep}*c1-{cancel}*c0,volume={volume}";
    }

    /// <summary>
    /// Returns a base file name that doesn't collide with any existing export file, so an export can
    /// never overwrite the source track or an existing library song.
    /// </summary>
    public static string GetUniqueBaseName(string directory, string baseName)
    {
        string[] suffixes = [".mp3", ".cdg", ".lrc", ".mp4", " (Vertical).mp4"];
        string candidate = baseName;
        for (int n = 2; suffixes.Any(s => File.Exists(Path.Combine(directory, candidate + s))); n++)
        {
            candidate = $"{baseName} ({n})";
        }
        return candidate;
    }

    private static async Task ProcessAudioTrackAsync(KaraokeStudioExportOptions options, string outputMp3Path, CancellationToken token)
    {
        var filters = new[]
        {
            BuildVocalFilter(options.VocalIsolationMode, options.GuideVocalPercent),
            FfmpegRunner.BuildPitchTempoFilter(options.KeySemitones, ClampTempoPercent(options.TempoPercent) / 100.0)
        }.Where(f => f.Length > 0).ToList();

        string filterArg = filters.Count > 0 ? $"-af \"{string.Join(",", filters)}\"" : string.Empty;
        await FfmpegRunner.RunAsync(
            $"-y -i \"{options.InputAudioPath}\" -vn {filterArg} -ar 44100 -ac 2 -b:a 192k \"{outputMp3Path}\"",
            token);
    }

    public static async Task<string> ExportCustomKaraokeTrackAsync(
        KaraokeStudioExportOptions options,
        IProgress<string>? statusProgress,
        CancellationToken token)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        options = options with { Lines = StudioLyricLine.ScaleForTempo(options.Lines, options.TempoPercent) };

        string safeArtist = SanitizeFileName(string.IsNullOrWhiteSpace(options.ArtistName) ? "Custom Artist" : options.ArtistName);
        string safeTitle = SanitizeFileName(string.IsNullOrWhiteSpace(options.SongTitle) ? "Custom Song" : options.SongTitle);
        bool isMp3G = options.ExportFormatIndex == 0;
        bool vertical = options.ExportFormatIndex == 2;

        // Everything is built in a private temp folder and only moved into place once it all succeeded.
        string workDir = Path.Combine(Path.GetTempPath(), "LyracistStudio_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            string workMp3 = Path.Combine(workDir, "audio.mp3");
            statusProgress?.Report("Step 1/3: Isolating vocals & applying key/tempo in FFmpeg...");
            await ProcessAudioTrackAsync(options, workMp3, token);

            double durationSec = EstimateDurationSeconds(workMp3, options.Lines);

            // Companion .lrc so Lyracist LRC player / Store pipeline has line timing
            string workLrc = Path.Combine(workDir, "lyrics.lrc");
            WriteLrcFile(workLrc, options);

            string workCdg = Path.Combine(workDir, "graphics.cdg");
            string workMp4 = Path.Combine(workDir, "video.mp4");
            if (isMp3G)
            {
                statusProgress?.Report("Step 2/3: Encoding binary CDG graphics stream (300 packets/sec)...");
                await Task.Run(() => GenerateCdgFile(workCdg, options, durationSec, token), token);
            }
            else
            {
                statusProgress?.Report("Step 2/3: Rendering synchronized karaoke video & encoding MP4...");
                await RenderMp4KaraokeVideoAsync(workMp3, workMp4, options, durationSec, vertical, token);
            }

            string baseName = GetUniqueBaseName(options.OutputDirectory, $"{safeArtist} - {safeTitle}");
            string resultPath;
            if (isMp3G)
            {
                resultPath = Path.Combine(options.OutputDirectory, baseName + ".mp3");
                File.Move(workCdg, Path.Combine(options.OutputDirectory, baseName + ".cdg"));
                File.Move(workMp3, resultPath);
            }
            else
            {
                resultPath = Path.Combine(options.OutputDirectory, baseName + (vertical ? " (Vertical).mp4" : ".mp4"));
                File.Move(workMp4, resultPath);
            }

            File.Move(workLrc, Path.Combine(options.OutputDirectory, baseName + ".lrc"));
            statusProgress?.Report($"Step 3/3: Export complete! Created {Path.GetFileName(resultPath)}");
            return resultPath;
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static double EstimateDurationSeconds(string audioPath, IReadOnlyList<StudioLyricLine> lines)
    {
        double maxLineEnd = lines.Count > 0 ? lines.Max(l => Math.Max(l.EndTimeSeconds, l.StartTimeSeconds + 4.0)) + 5.0 : 180.0;
        try
        {
            using var tagFile = TagLib.File.Create(audioPath);
            if (tagFile.Properties.Duration.TotalSeconds > 5.0)
            {
                return tagFile.Properties.Duration.TotalSeconds;
            }
        }
        catch { }
        return Math.Max(30.0, maxLineEnd);
    }

    private static void WriteLrcFile(string lrcPath, KaraokeStudioExportOptions options)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[ti:{options.SongTitle}]");
        sb.AppendLine($"[ar:{options.ArtistName}]");
        sb.AppendLine("[by:Lyracist Karaoke Track Studio]");

        foreach (var line in options.Lines.OrderBy(l => l.StartTimeSeconds))
        {
            TimeSpan ts = TimeSpan.FromSeconds(Math.Max(0, line.StartTimeSeconds));
            string prefix = line.SingerAssignment switch
            {
                "Singer 2" => "[S2] ",
                "Both (Duet)" => "[BOTH] ",
                _ => ""
            };
            sb.AppendLine($"[{ts:mm\\:ss\\.ff}]{prefix}{line.LyricText}");
        }
        File.WriteAllText(lrcPath, sb.ToString());
    }

    /// <summary>
    /// Encodes a real binary CD+G (.cdg) karaoke packet stream at 300 packets/second using SkiaSharp
    /// 300x216 16-color indexed frame rasterization and 6x12 tile delta encoding.
    /// </summary>
    public static void GenerateCdgFile(string cdgPath, KaraokeStudioExportOptions options, double durationSeconds, CancellationToken token = default)
    {
        int totalPackets = Math.Max(900, (int)Math.Ceiling(durationSeconds * CdgPacketsPerSecond));
        byte[] streamBytes = new byte[totalPackets * CdgPacketSize];
        int packetCursor = 0;

        // 16-color palette (Index 0: Dark Navy Background, 1: Gold Highlight, 2: White Unsung, 3: Cyan Singer 1, 4: Hot Pink Singer 2, 5: Emerald Duet, 6: Slate Translation, 7: Amber Countdown)
        SKColor[] palette =
        [
            new SKColor(10, 14, 28),    // 0: Background
            new SKColor(251, 191, 36),  // 1: Active Highlight Gold
            new SKColor(240, 240, 255), // 2: Unsung White
            new SKColor(56, 189, 248),  // 3: Singer 1 Cyan
            new SKColor(244, 114, 182), // 4: Singer 2 Pink
            new SKColor(52, 211, 153),  // 5: Duet Emerald
            new SKColor(148, 163, 184), // 6: Subtitle / Translation
            new SKColor(245, 158, 11),  // 7: Countdown Amber
            SKColors.Black, SKColors.Black, SKColors.Black, SKColors.Black,
            SKColors.Black, SKColors.Black, SKColors.Black, SKColors.Black
        ];

        void WritePacket(int index, byte instruction, ReadOnlySpan<byte> payload16)
        {
            if (index < 0 || index >= totalPackets) return;
            int off = index * CdgPacketSize;
            streamBytes[off] = 0x09; // CDG Command
            streamBytes[off + 1] = (byte)(instruction & 0x3F);
            for (int i = 0; i < Math.Min(16, payload16.Length); i++)
            {
                streamBytes[off + 4 + i] = (byte)(payload16[i] & 0x3F);
            }
        }

        // Write initial palette (LoadColorTableLow = 30) and MemoryPreset (1)
        Span<byte> palData = stackalloc byte[16];
        for (int i = 0; i < 8; i++)
        {
            int r4 = palette[i].Red / 17;
            int g4 = palette[i].Green / 17;
            int b4 = palette[i].Blue / 17;
            palData[i * 2] = (byte)(((r4 & 0x0F) << 2) | ((g4 >> 2) & 0x03));
            palData[(i * 2) + 1] = (byte)(((g4 & 0x03) << 4) | (b4 & 0x0F));
        }
        WritePacket(packetCursor++, 30, palData);

        Span<byte> presetData = stackalloc byte[16];
        presetData[0] = 0;
        presetData[1] = 0;
        WritePacket(packetCursor++, 1, presetData);
        WritePacket(packetCursor++, 2, presetData);

        byte[,] currentCanvas = new byte[CdgWidth, CdgHeight];
        byte[,] targetCanvas = new byte[CdgWidth, CdgHeight];

        using var infoSurface = SKSurface.Create(new SKImageInfo(CdgWidth, CdgHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var boldFace = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
        using var mainFont = new SKFont(boldFace, 15f);
        using var subFont = new SKFont(boldFace, 11f);

        // Render keyframe updates every 0.40 seconds (120 packets)
        const int stepPackets = 120;
        Span<byte> tileData = stackalloc byte[16];
        for (int pkt = 10; pkt < totalPackets - stepPackets; pkt += stepPackets)
        {
            token.ThrowIfCancellationRequested();
            double timeSec = (double)pkt / CdgPacketsPerSecond;
            RenderKaraokeFrameToIndexedCanvas(infoSurface.Canvas, infoSurface, options, timeSec, mainFont, subFont, palette, targetCanvas);

            int writeIdx = pkt;
            // Compare 6x12 tiles (50 cols x 18 rows) and emit TileBlockNormal (instruction 6) for changed tiles
            for (int ty = 1; ty < 17 && writeIdx < pkt + stepPackets - 1; ty++)
            {
                for (int tx = 1; tx < 49 && writeIdx < pkt + stepPackets - 1; tx++)
                {
                    bool changed = false;
                    int x0 = tx * 6;
                    int y0 = ty * 12;
                    byte fgColor = 0;

                    for (int dy = 0; dy < 12; dy++)
                    {
                        for (int dx = 0; dx < 6; dx++)
                        {
                            byte tPix = targetCanvas[x0 + dx, y0 + dy];
                            if (tPix != currentCanvas[x0 + dx, y0 + dy]) changed = true;
                            if (tPix != 0) fgColor = tPix;
                        }
                    }

                    if (!changed) continue;

                    tileData.Clear();
                    tileData[0] = 0; // bg color 0
                    tileData[1] = (byte)(fgColor & 0x0F);
                    tileData[2] = (byte)(ty & 0x1F);
                    tileData[3] = (byte)(tx & 0x3F);

                    for (int dy = 0; dy < 12; dy++)
                    {
                        byte rowMask = 0;
                        for (int dx = 0; dx < 6; dx++)
                        {
                            byte pix = targetCanvas[x0 + dx, y0 + dy];
                            if (pix != 0)
                            {
                                rowMask |= (byte)(1 << (5 - dx));
                            }
                            currentCanvas[x0 + dx, y0 + dy] = pix != 0 ? fgColor : (byte)0;
                        }
                        tileData[4 + dy] = rowMask;
                    }

                    WritePacket(writeIdx++, 6, tileData);
                }
            }
        }

        File.WriteAllBytes(cdgPath, streamBytes);
    }

    private static void RenderKaraokeFrameToIndexedCanvas(
        SKCanvas canvas,
        SKSurface surface,
        KaraokeStudioExportOptions options,
        double timeSec,
        SKFont mainFont,
        SKFont subFont,
        SKColor[] palette,
        byte[,] outIndexed)
    {
        canvas.Clear(palette[0]);

        var ordered = options.Lines.OrderBy(l => l.StartTimeSeconds).ToList();
        if (timeSec < 3.5 || ordered.Count == 0)
        {
            using var titlePaint = new SKPaint { Color = palette[1], IsAntialias = false };
            using var artistPaint = new SKPaint { Color = palette[3], IsAntialias = false };
            canvas.DrawText(Truncate(options.SongTitle, 24), CdgWidth / 2f, 95f, SKTextAlign.Center, mainFont, titlePaint);
            canvas.DrawText(Truncate(options.ArtistName, 26), CdgWidth / 2f, 125f, SKTextAlign.Center, subFont, artistPaint);
        }
        else
        {
            int activeIdx = ordered.FindLastIndex(l => l.StartTimeSeconds <= timeSec + 0.3);
            if (activeIdx < 0) activeIdx = 0;

            var currentLine = ordered[activeIdx];
            var nextLine = activeIdx + 1 < ordered.Count ? ordered[activeIdx + 1] : null;

            // Instrumental break countdown check
            if (options.IncludeInstrumentalCountdowns && nextLine != null && timeSec > currentLine.EndTimeSeconds + 1.0 && (nextLine.StartTimeSeconds - currentLine.EndTimeSeconds) >= 8.0)
            {
                int secsLeft = Math.Max(1, (int)Math.Ceiling(nextLine.StartTimeSeconds - timeSec));
                if (secsLeft <= 5)
                {
                    using var countPaint = new SKPaint { Color = palette[7], IsAntialias = false };
                    canvas.DrawText($"♪ NEXT IN {secsLeft}... ♪", CdgWidth / 2f, 55f, SKTextAlign.Center, subFont, countPaint);
                }
            }

            DrawIndexedLyricRow(canvas, currentLine, timeSec, 95f, mainFont, subFont, palette, isCurrent: true);
            if (nextLine != null)
            {
                DrawIndexedLyricRow(canvas, nextLine, timeSec, 152f, mainFont, subFont, palette, isCurrent: false);
            }
        }

        using var snap = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(snap);
        for (int y = 0; y < CdgHeight; y++)
        {
            for (int x = 0; x < CdgWidth; x++)
            {
                SKColor c = bmp.GetPixel(x, y);
                outIndexed[x, y] = FindNearestPaletteIndex(c, palette);
            }
        }
    }

    private static void DrawIndexedLyricRow(
        SKCanvas canvas,
        StudioLyricLine line,
        double timeSec,
        float y,
        SKFont mainFont,
        SKFont subFont,
        SKColor[] palette,
        bool isCurrent)
    {
        string text = Truncate(line.LyricText, 28);
        byte baseColorIdx = line.SingerAssignment switch
        {
            "Singer 2" => (byte)4,
            "Both (Duet)" => (byte)5,
            _ => isCurrent ? (byte)3 : (byte)2
        };

        bool isHighlighted = isCurrent && timeSec >= line.StartTimeSeconds && timeSec <= Math.Max(line.EndTimeSeconds, line.StartTimeSeconds + 3.5);
        using var paint = new SKPaint { Color = palette[isHighlighted ? 1 : baseColorIdx], IsAntialias = false };
        canvas.DrawText(text, CdgWidth / 2f, y, SKTextAlign.Center, mainFont, paint);

        if (!string.IsNullOrWhiteSpace(line.TranslationText))
        {
            using var subPaint = new SKPaint { Color = palette[6], IsAntialias = false };
            canvas.DrawText(Truncate(line.TranslationText, 34), CdgWidth / 2f, y + 18f, SKTextAlign.Center, subFont, subPaint);
        }
    }

    private static byte FindNearestPaletteIndex(SKColor c, SKColor[] palette)
    {
        int bestIdx = 0;
        int bestDist = int.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            int dr = c.Red - palette[i].Red;
            int dg = c.Green - palette[i].Green;
            int db = c.Blue - palette[i].Blue;
            int d = (dr * dr) + (dg * dg) + (db * db);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }
        return (byte)bestIdx;
    }

    private static async Task RenderMp4KaraokeVideoAsync(
        string audioPath,
        string outputMp4Path,
        KaraokeStudioExportOptions options,
        double durationSec,
        bool vertical,
        CancellationToken token)
    {
        int width = vertical ? 720 : 1280;
        int height = vertical ? 1280 : 720;
        const int fps = 4; // 4 keyframe renders/sec keeps rendering fast while smooth for lyric transitions

        int totalFrames = Math.Max(8, (int)Math.Ceiling(durationSec * fps));
        await FfmpegRunner.RunAsync(
            $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -framerate {fps} -i - -i \"{audioPath}\" -c:v libx264 -pix_fmt yuv420p -preset ultrafast -c:a aac -b:a 192k -shortest \"{outputMp4Path}\"",
            token,
            (input, ct) => Task.Run(() =>
            {
                using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using var boldFace = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
                using var mainFont = new SKFont(boldFace, vertical ? 38f : 44f);
                using var subFont = new SKFont(boldFace, vertical ? 24f : 26f);

                using var goldPaint = new SKPaint { Color = new SKColor(251, 191, 36), IsAntialias = true };
                using var cyanPaint = new SKPaint { Color = new SKColor(56, 189, 248), IsAntialias = true };
                using var pinkPaint = new SKPaint { Color = new SKColor(244, 114, 182), IsAntialias = true };
                using var emeraldPaint = new SKPaint { Color = new SKColor(52, 211, 153), IsAntialias = true };
                using var whitePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                using var subPaint = new SKPaint { Color = new SKColor(148, 163, 184), IsAntialias = true };

                var ordered = options.Lines.OrderBy(l => l.StartTimeSeconds).ToList();

                for (int f = 0; f < totalFrames; f++)
                {
                    ct.ThrowIfCancellationRequested();
                    double t = (double)f / fps;
                    var canvas = surface.Canvas;
                    canvas.Clear(new SKColor(12, 16, 32));

                    if (t < 3.0 || ordered.Count == 0)
                    {
                        canvas.DrawText(options.SongTitle, width / 2f, height * 0.44f, SKTextAlign.Center, mainFont, goldPaint);
                        canvas.DrawText(options.ArtistName, width / 2f, height * 0.54f, SKTextAlign.Center, subFont, cyanPaint);
                    }
                    else
                    {
                        int idx = ordered.FindLastIndex(l => l.StartTimeSeconds <= t + 0.25);
                        if (idx < 0) idx = 0;
                        var cur = ordered[idx];
                        var nxt = idx + 1 < ordered.Count ? ordered[idx + 1] : null;

                        if (options.IncludeInstrumentalCountdowns && nxt != null && t > cur.EndTimeSeconds + 0.5 && (nxt.StartTimeSeconds - cur.EndTimeSeconds) >= 8.0)
                        {
                            int rem = Math.Max(1, (int)Math.Ceiling(nxt.StartTimeSeconds - t));
                            if (rem <= 5)
                            {
                                canvas.DrawText($"♪ INSTRUMENTAL BREAK ({rem}...) ♪", width / 2f, height * 0.25f, SKTextAlign.Center, subFont, goldPaint);
                            }
                        }

                        var curPaint = (t >= cur.StartTimeSeconds && t <= Math.Max(cur.EndTimeSeconds, cur.StartTimeSeconds + 3.5))
                            ? goldPaint
                            : (cur.SingerAssignment == "Singer 2" ? pinkPaint : (cur.SingerAssignment == "Both (Duet)" ? emeraldPaint : cyanPaint));

                        canvas.DrawText(cur.LyricText, width / 2f, height * 0.45f, SKTextAlign.Center, mainFont, curPaint);
                        if (!string.IsNullOrWhiteSpace(cur.TranslationText))
                        {
                            canvas.DrawText(cur.TranslationText, width / 2f, (height * 0.45f) + 42f, SKTextAlign.Center, subFont, subPaint);
                        }

                        if (nxt != null)
                        {
                            canvas.DrawText(nxt.LyricText, width / 2f, height * 0.66f, SKTextAlign.Center, mainFont, whitePaint);
                            if (!string.IsNullOrWhiteSpace(nxt.TranslationText))
                            {
                                canvas.DrawText(nxt.TranslationText, width / 2f, (height * 0.66f) + 42f, SKTextAlign.Center, subFont, subPaint);
                            }
                        }
                    }

                    canvas.Flush();
                    using var pixels = surface.PeekPixels();
                    input.Write(pixels.GetPixelSpan());
                }
            }, ct));
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLen ? value : value[..maxLen];
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName.Trim();
    }
}
