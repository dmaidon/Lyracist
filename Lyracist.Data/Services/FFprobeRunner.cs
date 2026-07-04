using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lyracist.Data.Services
{
    public class FFprobeResult
    {
        public double Duration { get; set; }
        public string AudioCodec { get; set; } = string.Empty;
        public string VideoCodec { get; set; } = string.Empty;
    }

    public class FFprobeRunner
    {
        public static string FFprobePath { get; set; } = "ffprobe";

        public static async Task<FFprobeResult> ProbeFile(string filePath)
        {
            var result = new FFprobeResult();

            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"FFprobe failed: File does not exist: {filePath}");
                return result;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFprobePath,
                    Arguments = $"-v error -show_entries format=duration -show_streams -print_format json \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    using var doc = JsonDocument.Parse(output);
                    var root = doc.RootElement;

                    // Extract format duration
                    if (root.TryGetProperty("format", out var formatElement) &&
                        formatElement.TryGetProperty("duration", out var durationProp))
                    {
                        string durStr = durationProp.GetString() ?? string.Empty;
                        if (double.TryParse(durStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dur))
                        {
                            result.Duration = dur;
                        }
                    }

                    // Extract codecs from streams
                    if (root.TryGetProperty("streams", out var streamsElement) && streamsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var stream in streamsElement.EnumerateArray())
                        {
                            if (stream.TryGetProperty("codec_type", out var typeProp) &&
                                stream.TryGetProperty("codec_name", out var nameProp))
                            {
                                string type = typeProp.GetString() ?? string.Empty;
                                string codec = nameProp.GetString() ?? string.Empty;

                                if (type == "audio" && string.IsNullOrEmpty(result.AudioCodec))
                                {
                                    result.AudioCodec = codec;
                                }
                                else if (type == "video" && string.IsNullOrEmpty(result.VideoCodec))
                                {
                                    result.VideoCodec = codec;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"FFprobe process exited with code {process.ExitCode}. Error: {error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception executing FFprobe for {filePath}: {ex.Message}");
            }

            return result;
        }
    }
}
