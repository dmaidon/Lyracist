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
        // MP3+G TO MP4 CONVERSION
        // ==========================================

        public async Task<bool> ConvertMp3GToMp4(string mp3Path, string cdgPath, string outputPath)
        {
            if (!File.Exists(mp3Path) || !File.Exists(cdgPath))
            {
                Debug.WriteLine($"Conversion failed: Missing input files: {mp3Path} or {cdgPath}");
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

                // Capture outputs asynchronously to prevent hang
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine($"FFmpeg exit code: {process.ExitCode}. Error output: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception converting MP3+G to MP4: {ex.Message}");
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
                Debug.WriteLine($"Conversion failed: ZIP file not found: {zipPath}");
                return false;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "LyracistConvert_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string mp3Path = string.Empty;
                string cdgPath = string.Empty;

                using (var archive = ZipFile.OpenRead(zipPath))
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
                    Debug.WriteLine($"Conversion failed: ZIP file does not contain matching CDG and audio: {zipPath}");
                    return false;
                }

                return await ConvertMp3GToMp4(mp3Path, cdgPath, outputPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception converting ZIP to MP4: {ex.Message}");
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
                Debug.WriteLine($"Thumbnail extraction failed: Video not found: {videoPath}");
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

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception extracting thumbnail from {videoPath}: {ex.Message}");
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
            if (ext == ".zip")
            {
                try
                {
                    using var archive = ZipFile.OpenRead(filePath);
                    bool hasCdg = archive.Entries.Any(e => Path.GetExtension(e.FullName).ToLowerInvariant() == ".cdg");
                    bool hasAudio = archive.Entries.Any(e =>
                    {
                        string entryExt = Path.GetExtension(e.FullName).ToLowerInvariant();
                        return entryExt == ".mp3" || entryExt == ".wav";
                    });
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
    }
}
