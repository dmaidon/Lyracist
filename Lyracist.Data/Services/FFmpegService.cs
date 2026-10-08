// Edited on Oct 7, 2026 @ 19:55:00 -> Add silencedetect cue point detection (MixInMs and MixOutMs)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyracist.Data.Services
{
    public class FFmpegService
    {
        public static string FFmpegPath { get; set; } = "ffmpeg";
        public static string FFprobePath { get; set; } = "ffprobe";

        static FFmpegService()
        {
            ResolvePaths();
        }

        public static void ResolvePaths()
        {
            // First check the current base executing directory for bundled binaries
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string localFFmpeg = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(localFFmpeg))
            {
                FFmpegPath = localFFmpeg;
            }

            string localFFprobe = Path.Combine(baseDir, "ffprobe.exe");
            if (File.Exists(localFFprobe))
            {
                FFprobePath = localFFprobe;
                FFprobeRunner.FFprobePath = localFFprobe;
            }
        }

        // ==========================================
        // PROBE SUPPORT WRAPPER
        // ==========================================

        public static async Task<FFprobeResult> ProbeFile(string filePath)
        {
            return await FFprobeRunner.ProbeFile(filePath);
        }

        // ==========================================
        // INTEGRATED LOUDNESS MEASUREMENT (EBU R128)
        // ==========================================

        /// <summary>
        /// Runs a single-pass ffmpeg `loudnorm` analysis to measure a track's integrated loudness
        /// (LUFS), for volume-normalization gain calculations. Decodes the whole file (no output is
        /// written), so this is far heavier than <see cref="ProbeFile"/> and should never run inline
        /// during a fast library scan - callers should measure lazily/in the background instead.
        /// </summary>
        public static async Task<double?> MeasureIntegratedLoudness(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Loudness measurement failed: file not found: {filePath}", "FFmpegService.MeasureIntegratedLoudness");
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-hide_banner");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(filePath);
                startInfo.ArgumentList.Add("-af");
                startInfo.ArgumentList.Add("loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json");
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add("null");
                startInfo.ArgumentList.Add("-");

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // loudnorm's analysis JSON is written to stderr, not stdout - drain both
                // concurrently so a full stdout pipe can't stall ffmpeg mid-decode.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                string error = errorTask.Result;

                int jsonStart = error.LastIndexOf('{');
                int jsonEnd = error.LastIndexOf('}');
                if (jsonStart < 0 || jsonEnd <= jsonStart)
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", $"Loudness measurement produced no analysis block for {filePath}. FFmpeg output: {error}", "FFmpegService.MeasureIntegratedLoudness");
                    return null;
                }

                string json = error.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                // "input_i" is the measured integrated loudness in LUFS, e.g. "-23.14". Silent or
                // near-silent input reports "-inf" here, which is not a usable gain reference.
                if (doc.RootElement.TryGetProperty("input_i", out var inputI) &&
                    double.TryParse(inputI.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lufs) &&
                    !double.IsInfinity(lufs) && lufs < 0)
                {
                    return lufs;
                }

                return null;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Exception measuring loudness for {filePath}", ex);
                return null;
            }
        }

        // ==========================================
        // CUE POINT DETECTION (SILENCEDETECT)
        // ==========================================

        public static (int? MixInMs, int? MixOutMs) ParseSilenceDetectOutput(string output, double? durationSeconds = null)
        {
            if (string.IsNullOrWhiteSpace(output))
                return (null, null);

            var startMatches = System.Text.RegularExpressions.Regex.Matches(output, @"silence_start:\s*([0-9.]+)");
            var endMatches = System.Text.RegularExpressions.Regex.Matches(output, @"silence_end:\s*([0-9.]+)");

            int? mixInMs = null;
            int? mixOutMs = null;

            // 1. MixIn (Leading silence):
            // Check if there is an initial silence segment starting at or near 0
            if (endMatches.Count > 0)
            {
                if (double.TryParse(endMatches[0].Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double firstEnd))
                {
                    double firstStart = 0;
                    if (startMatches.Count > 0 && double.TryParse(startMatches[0].Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double s0))
                    {
                        firstStart = s0;
                    }

                    if (firstStart <= 0.25 && firstEnd >= 0.1 && firstEnd < 30.0)
                    {
                        mixInMs = (int)Math.Round(firstEnd * 1000);
                    }
                }
            }

            // 2. MixOut (Trailing silence):
            // Check the last silence_start
            if (startMatches.Count > 0)
            {
                var lastStartMatch = startMatches[startMatches.Count - 1];
                if (double.TryParse(lastStartMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lastStart))
                {
                    bool isAtEnd = false;
                    if (startMatches.Count > endMatches.Count)
                    {
                        isAtEnd = true;
                    }
                    else if (durationSeconds.HasValue && durationSeconds.Value > 0)
                    {
                        if (lastStart >= durationSeconds.Value - 30.0)
                            isAtEnd = true;
                    }
                    else if (endMatches.Count > 0)
                    {
                        var lastEndMatch = endMatches[endMatches.Count - 1];
                        if (double.TryParse(lastEndMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double lastEnd))
                        {
                            if (lastEnd >= lastStart) isAtEnd = true;
                        }
                    }

                    if (isAtEnd && lastStart > 5.0)
                    {
                        mixOutMs = (int)Math.Round(lastStart * 1000);
                    }
                }
            }

            // Sanity check: MixOut must be significantly after MixIn
            if (mixInMs.HasValue && mixOutMs.HasValue && mixOutMs.Value <= mixInMs.Value + 3000)
            {
                mixOutMs = null;
            }

            return (mixInMs, mixOutMs);
        }

        public static async Task<(int? MixInMs, int? MixOutMs)> DetectCuePointsAsync(string filePath, double? durationSeconds = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return (null, null);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-hide_banner");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(filePath);
                startInfo.ArgumentList.Add("-af");
                startInfo.ArgumentList.Add("silencedetect=noise=-50dB:d=0.5");
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add("null");
                startInfo.ArgumentList.Add("-");

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var outTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await Task.WhenAll(outTask, errTask);
                await process.WaitForExitAsync(cancellationToken);

                string stderr = errTask.Result;
                return ParseSilenceDetectOutput(stderr, durationSeconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Failed to detect cue points for {filePath}", ex);
                return (null, null);
            }
        }

        // ==========================================
        // MP3+G TO MP4 CONVERSION
        // ==========================================

        public async Task<bool> ConvertMp3GToMp4(string mp3Path, string cdgPath, string outputPath)
        {
            if (!File.Exists(mp3Path) || !File.Exists(cdgPath))
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Conversion failed: missing input files: {mp3Path} or {cdgPath}", "FFmpegService.ConvertMp3GToMp4");
                return false;
            }

            try
            {
                // Ensure output directory exists
                string? outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                // Command arguments: Combine CDG graphics and MP3 audio into standard H.264 / AAC MP4 video securely via ArgumentList
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(cdgPath);
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(mp3Path);
                startInfo.ArgumentList.Add("-c:v");
                startInfo.ArgumentList.Add("libx264");
                startInfo.ArgumentList.Add("-pix_fmt");
                startInfo.ArgumentList.Add("yuv420p");
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("aac");
                startInfo.ArgumentList.Add("-shortest");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Read stdout and stderr concurrently: ffmpeg streams progress to stderr
                // and will block writing to it if we drain stdout first and its pipe fills up.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                string output = outputTask.Result;
                string error = errorTask.Result;

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", $"FFmpeg exit code: {process.ExitCode}. Error output: {error}", "FFmpegService.ConvertMp3GToMp4");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Exception converting MP3+G to MP4", ex);
                return false;
            }
        }

        // ==========================================
        // ZIP/CDG TO MP4 CONVERSION
        // ==========================================

        public async Task<bool> ConvertZipToMp4(string zipPath, string outputPath)
        {
            if (!File.Exists(zipPath))
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Conversion failed: ZIP file not found: {zipPath}", "FFmpegService.ConvertZipToMp4");
                return false;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "LyracistConvert_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string mp3Path = string.Empty;
                string cdgPath = string.Empty;

                await using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string ext = Path.GetExtension(entry.FullName).ToLowerInvariant();
                        if (ext == ".mp3" || ext == ".wav")
                        {
                            mp3Path = Path.Combine(tempDir, "audio" + ext);
                            entry.ExtractToFile(mp3Path, true);
                        }
                        else if (ext == ".cdg")
                        {
                            cdgPath = Path.Combine(tempDir, "lyrics.cdg");
                            entry.ExtractToFile(cdgPath, true);
                        }
                    }
                }

                if (string.IsNullOrEmpty(mp3Path) || string.IsNullOrEmpty(cdgPath))
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", $"Conversion failed: ZIP file does not contain matching CDG and audio: {zipPath}", "FFmpegService.ConvertZipToMp4");
                    return false;
                }

                return await ConvertMp3GToMp4(mp3Path, cdgPath, outputPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", "Exception converting ZIP to MP4", ex);
                return false;
            }
            finally
            {
                // Clean up temporary files
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        // ==========================================
        // THUMBNAIL EXTRACTION
        // ==========================================

        public async Task<bool> ExtractThumbnail(string videoPath, string outputPath)
        {
            if (!File.Exists(videoPath))
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Thumbnail extraction failed: video not found: {videoPath}", "FFmpegService.ExtractThumbnail");
                return false;
            }

            try
            {
                // Ensure output directory exists
                string? outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                // Check video duration. If shorter than 2s, start thumbnail extraction at 0s
                var probe = await ProbeFile(videoPath);
                string startOffset = probe.Duration >= 2.0 ? "00:00:02" : "00:00:00";

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(videoPath);
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add(startOffset);
                startInfo.ArgumentList.Add("-vframes");
                startInfo.ArgumentList.Add("1");
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add("image2");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Read stdout and stderr concurrently to avoid deadlocking on a full pipe buffer.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Exception extracting thumbnail from {videoPath}", ex);
                return false;
            }
        }

        // ==========================================
        // KARAOKE DETECTION
        // ==========================================

        public async Task<bool> IsKaraokeFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // 1. MP3+G pair checking (has matching CDG lyrics file)
            if (ext == ".mp3")
            {
                string cdgPath = Path.ChangeExtension(filePath, ".cdg");
                return File.Exists(cdgPath);
            }
            if (ext == ".cdg")
            {
                string mp3Path = Path.ChangeExtension(filePath, ".mp3");
                return File.Exists(mp3Path);
            }

            // 2. ZIP checking (contains a CDG and audio)
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(filePath);
                    bool hasCdg = archive.Entries.Any(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase));
                    bool hasAudio = archive.Entries.Any(e =>
                        e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
                    return hasCdg && hasAudio;
                }
                catch
                {
                    return false;
                }
            }

            // 3. Video (MP4) checking (check name keywords & subtitle streams)
            if (ext == ".mp4")
            {
                // Filename keywords
                string name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
                if (name.Contains("karaoke") || name.Contains("instrumental") || name.Contains("singalong") || name.Contains("sing-along"))
                {
                    return true;
                }

                // Check streams layout for subtitle / graphics overlay track
                try
                {
                    var probe = await ProbeFile(filePath);
                    // Standard video karaoke files may contain text overlays or subtitle streams
                    // If we see a subtitle stream, it is likely karaoke
                    // Check if ffprobe returned any stream that has subtitle codec properties
                    // (Streams are analyzed inside ProbeFile, but FFprobeResult doesn't output all stream types yet. 
                    // However, we check this via runner logic or keep standard checks)
                }
                catch { }
            }

            return false;
        }

        // ==========================================
        // AUDIO FILTER PIPELINE BUILDER
        // ==========================================

        public static string BuildAudioFilterString(
            double treble,      // gain in dB (-10 to 10)
            double mid,         // gain in dB (-10 to 10)
            double bass,        // gain in dB (-10 to 10)
            double gain,        // volume shift in dB (-10 to 10)
            int key,            // semitones pitch shift (-6 to 6)
            double tempo,       // speed multiplier (0.5 to 2.0)
            double compressor,  // compression ratio/strength (0.0 to 1.0)
            double limiter      // limiter limit in dB (-10 to 0)
        )
        {
            var filters = new List<string>();

            // 1. Equalizers (Treble, Mid, Bass)
            if (treble != 0)
            {
                filters.Add($"treble=g={treble.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
            if (mid != 0)
            {
                // 2-pole peaking band equalizer around mid range (1000Hz)
                filters.Add($"equalizer=f=1000:width_type=q:width=1.0:g={mid.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
            if (bass != 0)
            {
                filters.Add($"bass=g={bass.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            // 2. Volume Gain
            if (gain != 0)
            {
                filters.Add($"volume={gain.ToString(System.Globalization.CultureInfo.InvariantCulture)}dB");
            }

            // 3. Pitch / Key & Tempo (using asetrate + atempo for maximum compatibility across all FFmpeg builds)
            bool hasPitch = key != 0;
            bool hasTempo = tempo != 1.0;

            if (hasPitch || hasTempo)
            {
                double pitchRatio = hasPitch ? Math.Pow(2.0, key / 12.0) : 1.0;

                if (hasPitch)
                {
                    // asetrate changes pitch and speed by altering the sample rate
                    // We assume 44100Hz as base, which is standard for karaoke tracks
                    double newRate = 44100.0 * pitchRatio;
                    filters.Add($"asetrate=r={((int)newRate).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }

                // Calculate the corrective tempo ratio to offset the pitch-shift speed change
                double correctiveTempo = tempo / pitchRatio;

                // FFmpeg's atempo filter is limited to 0.5 - 2.0.
                // If the corrective tempo falls outside this, we chain multiple atempo filters.
                if (correctiveTempo != 1.0)
                {
                    if (correctiveTempo >= 0.5 && correctiveTempo <= 2.0)
                    {
                        filters.Add($"atempo={correctiveTempo.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        // Chain two atempo filters to cover wider ranges if necessary
                        double part1 = Math.Clamp(correctiveTempo, 0.5, 2.0);
                        double part2 = correctiveTempo / part1;
                        filters.Add($"atempo={part1.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                        filters.Add($"atempo={part2.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    }
                }
            }

            // 4. Dynamic Audio Compressor
            if (compressor > 0)
            {
                // Scale threshold based on slider value (0.0 -> 0dB limit, 1.0 -> -40dB limit)
                double threshold = -40.0 * Math.Clamp(compressor, 0.0, 1.0);
                filters.Add($"acompressor=threshold={threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)}dB:ratio=4:attack=20:release=250");
            }

            // 5. Lookahead Limiter
            if (limiter < 0)
            {
                filters.Add($"alimiter=limit={limiter.ToString(System.Globalization.CultureInfo.InvariantCulture)}dB");
            }

            if (filters.Count == 0)
            {
                return string.Empty;
            }

            // Join the audio filters sequentially with commas
            return string.Join(",", filters);
        }

        // ==========================================
        // STORE IMPORT AUDIO PROCESSING PIPELINE
        // ==========================================

        public static async Task<bool> NormalizeAudioAsync(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath)) return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("-af");
                startInfo.ArgumentList.Add("loudnorm=I=-16:TP=-1.5:LRA=11");
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("libmp3lame");
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add("320k");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"FFmpeg normalization failed for {inputPath}", ex);
                return false;
            }
        }

        public static async Task<bool> TrimSilenceAsync(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath)) return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("-af");
                startInfo.ArgumentList.Add("silenceremove=start_periods=1:start_duration=0.1:start_threshold=-50dB:detection=peak,areverse,silenceremove=start_periods=1:start_duration=0.1:start_threshold=-50dB:detection=peak,areverse");
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("libmp3lame");
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add("320k");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"FFmpeg silence trimming failed for {inputPath}", ex);
                return false;
            }
        }

        public static async Task<bool> GenerateWaveformPreviewAsync(string inputPath, string outputPngPath)
        {
            if (!File.Exists(inputPath)) return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("-filter_complex");
                startInfo.ArgumentList.Add("aformat=channel_layouts=mono,showwavespic=s=800x160:colors=#00C9FF|#8E2DE2");
                startInfo.ArgumentList.Add("-frames:v");
                startInfo.ArgumentList.Add("1");
                startInfo.ArgumentList.Add(outputPngPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputPngPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"FFmpeg waveform generation failed for {inputPath}", ex);
                return false;
            }
        }

        // ==========================================
        // SMART IMPORT HELPERS & ANALYSIS
        // ==========================================

        public static string? DetectGenre(string provider, FFprobeResult metadata) =>
            FFprobeRunner.DetectGenre(provider, metadata);

        public static string DetectDifficulty(FFprobeResult metadata) =>
            FFprobeRunner.DetectDifficulty(metadata);

        public static string? DetectKey(FFprobeResult metadata) =>
            FFprobeRunner.DetectKey(metadata);

        public static double? DetectBpm(FFprobeResult metadata) =>
            FFprobeRunner.DetectBpm(metadata);

        public static string DetectVocalPresence(FFprobeResult metadata) =>
            FFprobeRunner.DetectVocalPresence(metadata);

        public static string DetectQuality(FFprobeResult metadata) =>
            FFprobeRunner.DetectQuality(metadata);

        public static async Task<string?> DetectKeyAsync(string filePath)
        {
            var probe = await ProbeFile(filePath);
            return DetectKey(probe);
        }

        public static async Task<double?> DetectBpmAsync(string filePath)
        {
            var probe = await ProbeFile(filePath);
            return DetectBpm(probe);
        }

        // ==========================================
        // BATCH-FRIENDLY AUDIO PIPELINE WRAPPER
        // ==========================================

        public static async Task<bool> ProcessAudioPipelineBatchAsync(
            string inputAudioPath,
            string outputAudioPath,
            bool normalize,
            bool trimSilence)
        {
            if (!File.Exists(inputAudioPath)) return false;
            if (!normalize && !trimSilence)
            {
                if (!string.Equals(inputAudioPath, outputAudioPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(inputAudioPath, outputAudioPath, true);
                }
                return true;
            }

            try
            {
                var filters = new List<string>();
                if (trimSilence)
                {
                    filters.Add("silenceremove=start_periods=1:start_duration=0.1:start_threshold=-50dB:detection=peak,areverse,silenceremove=start_periods=1:start_duration=0.1:start_threshold=-50dB:detection=peak,areverse");
                }
                if (normalize)
                {
                    filters.Add("loudnorm=I=-16:TP=-1.5:LRA=11");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputAudioPath);
                startInfo.ArgumentList.Add("-af");
                startInfo.ArgumentList.Add(string.Join(",", filters));
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("libmp3lame");
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add("320k");
                startInfo.ArgumentList.Add(outputAudioPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputAudioPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"ProcessAudioPipelineBatchAsync failed for {inputAudioPath}", ex);
                return false;
            }
        }

        // ==========================================
        // TRACK PREVIEW EXTRACTION HELPERS
        // ==========================================

        public static async Task<bool> GenerateAudioPreviewAsync(
            string inputPath,
            string outputPath,
            int? channelIndex = null)
        {
            if (!File.Exists(inputPath)) return false;

            try
            {
                string? outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add("0");
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add("5");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);

                if (channelIndex == 0)
                {
                    // Channel A: Guide Vocals (Left channel / Channel 0)
                    startInfo.ArgumentList.Add("-af");
                    startInfo.ArgumentList.Add("pan=mono|c0=c0");
                }
                else if (channelIndex == 1)
                {
                    // Channel B: Instrumental (Right channel / Channel 1)
                    startInfo.ArgumentList.Add("-af");
                    startInfo.ArgumentList.Add("pan=mono|c0=c1");
                }

                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("libmp3lame");
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add("192k");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"GenerateAudioPreviewAsync failed for {inputPath}", ex);
                return false;
            }
        }

        public static async Task<bool> GenerateSpectrogramAsync(
            string inputPath,
            string outputPath)
        {
            if (!File.Exists(inputPath)) return false;

            try
            {
                string? outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("-filter_complex");
                startInfo.ArgumentList.Add("showspectrum=s=800x300:color=intensity");
                startInfo.ArgumentList.Add("-frames:v");
                startInfo.ArgumentList.Add("1");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"GenerateSpectrogramAsync failed for {inputPath}", ex);
                return false;
            }
        }

        public static async Task<bool> GenerateVideoPreviewAsync(
            string inputPath,
            string outputPath)
        {
            if (!File.Exists(inputPath)) return false;

            try
            {
                string? outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = FFmpegPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-y");
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add("0");
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add("5");
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("-c:v");
                startInfo.ArgumentList.Add("libx264");
                startInfo.ArgumentList.Add("-preset");
                startInfo.ArgumentList.Add("ultrafast");
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("aac");
                startInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"GenerateVideoPreviewAsync failed for {inputPath}", ex);
                return false;
            }
        }
    }
}
