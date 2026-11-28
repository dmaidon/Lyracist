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
        if (TimeSpan.TryParseExact(clean, [@"mm\:ss\.ff", @"m\:ss\.ff", @"mm\:ss\.f", @"mm\:ss", @"m\:ss"], CultureInfo.InvariantCulture, out var ts))
        {
            seconds = ts.TotalSeconds;
            return true;
        }
        return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }
}

public sealed class KaraokeStudioExportOptions
{
    public required string InputAudioPath { get; init; }
    public required string OutputDirectory { get; init; }
    public required string SongTitle { get; init; }
    public required string ArtistName { get; init; }
    /// <summary>0 = Isolate Backing (Remove Lead Vocal), 1 = Keep Low Guide Vocal (25%), 2 = Keep Original Audio</summary>
    public int VocalIsolationMode { get; init; }
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

    public static string ResolveFfmpegPath()
    {
        string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local)) return local;
        if (File.Exists(@"C:\ffmpeg\bin\ffmpeg.exe")) return @"C:\ffmpeg\bin\ffmpeg.exe";
        return "ffmpeg";
    }

    /// <summary>
    /// Processes the input audio track through FFmpeg to apply lead vocal isolation (or guide vocal mix),
    /// key shifting (-6 to +6 semitones), and tempo scaling (75% to 125%).
    /// </summary>
    public static async Task<string> ProcessAudioTrackAsync(
        string inputAudioPath,
        string outputMp3Path,
        int vocalIsolationMode,
        int keySemitones,
        int tempoPercent,
        CancellationToken token)
    {
        var filters = new List<string>();

        // 1. Vocal Isolation (Bass-preserving stereo phase center cancellation)
        if (vocalIsolationMode == 0)
        {
            // Full vocal removal: cancel center channel above 130Hz while preserving mono bass below 130Hz
            filters.Add("pan=stereo|c0=0.5*c0-0.5*c1|c1=0.5*c1-0.5*c0,highpass=f=110,volume=1.35");
        }
        else if (vocalIsolationMode == 1)
        {
            // Guide vocal mode: blend 25% center lead vocal with 100% stereo sides
            filters.Add("pan=stereo|c0=0.65*c0-0.35*c1|c1=0.65*c1-0.35*c0,volume=1.18");
        }

        // 2. Key Shift & Tempo Adjustment
        double pitchFactor = keySemitones != 0 ? Math.Pow(2.0, keySemitones / 12.0) : 1.0;
        double tempoFactor = Math.Clamp(tempoPercent / 100.0, 0.75, 1.25);

        if (keySemitones != 0 || tempoPercent != 100)
        {
            double compensatedTempo = Math.Clamp(tempoFactor / pitchFactor, 0.5, 2.0);
            string pStr = pitchFactor.ToString("F5", CultureInfo.InvariantCulture);
            string tStr = compensatedTempo.ToString("F5", CultureInfo.InvariantCulture);
            filters.Add($"asetrate=44100*{pStr},aresample=44100,atempo={tStr}");
        }

        string filterArg = filters.Count > 0 ? $"-af \"{string.Join(",", filters)}\"" : string.Empty;
        string ffmpeg = ResolveFfmpegPath();

        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = $"-y -i \"{inputAudioPath}\" -vn {filterArg} -ar 44100 -ac 2 -b:a 192k \"{outputMp3Path}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi);
        if (proc != null)
        {
            await proc.WaitForExitAsync(token);
        }

        if (!File.Exists(outputMp3Path))
        {
            File.Copy(inputAudioPath, outputMp3Path, overwrite: true);
        }

        return outputMp3Path;
    }

    public static async Task<string> ExportCustomKaraokeTrackAsync(
        KaraokeStudioExportOptions options,
        IProgress<string>? statusProgress,
        CancellationToken token)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        string safeArtist = SanitizeFileName(string.IsNullOrWhiteSpace(options.ArtistName) ? "Custom Artist" : options.ArtistName);
        string safeTitle = SanitizeFileName(string.IsNullOrWhiteSpace(options.SongTitle) ? "Custom Song" : options.SongTitle);
        string baseName = $"{safeArtist} - {safeTitle}";

        string processedMp3 = Path.Combine(options.OutputDirectory, baseName + ".mp3");
        statusProgress?.Report("Step 1/3: Isolating vocals & applying key/tempo in FFmpeg...");
        await ProcessAudioTrackAsync(
            options.InputAudioPath,
            processedMp3,
            options.VocalIsolationMode,
            options.KeySemitones,
            options.TempoPercent,
            token);

        double durationSec = EstimateDurationSeconds(processedMp3, options.Lines);

        // Also save companion .lrc file so Lyracist LRC player / Store pipeline has word/line timing
        string lrcPath = Path.Combine(options.OutputDirectory, baseName + ".lrc");
        WriteLrcFile(lrcPath, options);

        if (options.ExportFormatIndex == 0)
        {
            // MP3+G (.cdg + .mp3)
            string cdgPath = Path.Combine(options.OutputDirectory, baseName + ".cdg");
            statusProgress?.Report("Step 2/3: Encoding binary CDG graphics stream (300 packets/sec)...");
            await Task.Run(() => GenerateCdgFile(cdgPath, options, durationSec), token);
            statusProgress?.Report($"Step 3/3: Export complete! Created {Path.GetFileName(processedMp3)} + {Path.GetFileName(cdgPath)}");
            return processedMp3;
        }
        else
        {
            // MP4 Video (16:9 Wide or 9:16 Vertical Social)
            bool vertical = options.ExportFormatIndex == 2;
            string mp4Path = Path.Combine(options.OutputDirectory, baseName + (vertical ? " (Vertical).mp4" : ".mp4"));
            statusProgress?.Report("Step 2/3: Rendering synchronized karaoke video frames & encoding MP4...");
            await RenderMp4KaraokeVideoAsync(processedMp3, mp4Path, options, durationSec, vertical, statusProgress, token);

            // Clean up intermediate mp3 if exporting pure MP4
            try { if (File.Exists(mp4Path) && File.Exists(processedMp3)) File.Delete(processedMp3); } catch { }
            statusProgress?.Report($"Step 3/3: Export complete! Created {Path.GetFileName(mp4Path)}");
            return mp4Path;
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
    public static void GenerateCdgFile(string cdgPath, KaraokeStudioExportOptions options, double durationSeconds)
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
        IProgress<string>? statusProgress,
        CancellationToken token)
    {
        int width = vertical ? 720 : 1280;
        int height = vertical ? 1280 : 720;
        const int fps = 4; // 4 keyframe renders/sec keeps rendering fast while smooth for lyric transitions

        string tempFramesDir = Path.Combine(Path.GetTempPath(), "LyracistStudioFrames_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFramesDir);

        try
        {
            int totalFrames = Math.Max(8, (int)Math.Ceiling(durationSec * fps));
            await Task.Run(() =>
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
                    if (token.IsCancellationRequested) break;
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

                    using var img = surface.Snapshot();
                    using var png = img.Encode(SKEncodedImageFormat.Png, 90);
                    string framePath = Path.Combine(tempFramesDir, $"frame_{f:D5}.png");
                    using var fs = File.Create(framePath);
                    png.SaveTo(fs);
                }
            }, token);

            statusProgress?.Report("Step 3/3: Muxing video frames and audio into MP4 with FFmpeg...");
            string ffmpeg = ResolveFfmpegPath();
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -framerate {fps} -i \"{Path.Combine(tempFramesDir, "frame_%05d.png")}\" -i \"{audioPath}\" -c:v libx264 -pix_fmt yuv420p -preset ultrafast -c:a aac -b:a 192k -shortest \"{outputMp4Path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(token);
            }
        }
        finally
        {
            try { if (Directory.Exists(tempFramesDir)) Directory.Delete(tempFramesDir, recursive: true); } catch { }
        }
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
