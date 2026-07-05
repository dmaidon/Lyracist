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
        public string TitleTag { get; set; } = string.Empty;
        public string ArtistTag { get; set; } = string.Empty;
        public string GenreTag { get; set; } = string.Empty;
        public string CommentTag { get; set; } = string.Empty;
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
                    Arguments = $"-v error -show_format -show_streams -print_format json \"{filePath}\"",
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

                    // Extract format duration and tags
                    if (root.TryGetProperty("format", out var formatElement))
                    {
                        if (formatElement.TryGetProperty("duration", out var durationProp))
                        {
                            string durStr = durationProp.GetString() ?? string.Empty;
                            if (double.TryParse(durStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dur))
                            {
                                result.Duration = dur;
                            }
                        }

                        if (formatElement.TryGetProperty("tags", out var tagsElement))
                        {
                            if (tagsElement.TryGetProperty("title", out var tProp))
                                result.TitleTag = tProp.GetString() ?? string.Empty;
                            else if (tagsElement.TryGetProperty("TITLE", out var tPropU))
                                result.TitleTag = tPropU.GetString() ?? string.Empty;

                            if (tagsElement.TryGetProperty("artist", out var aProp))
                                result.ArtistTag = aProp.GetString() ?? string.Empty;
                            else if (tagsElement.TryGetProperty("ARTIST", out var aPropU))
                                result.ArtistTag = aPropU.GetString() ?? string.Empty;

                            if (tagsElement.TryGetProperty("genre", out var gProp))
                                result.GenreTag = gProp.GetString() ?? string.Empty;
                            else if (tagsElement.TryGetProperty("GENRE", out var gPropU))
                                result.GenreTag = gPropU.GetString() ?? string.Empty;

                            if (tagsElement.TryGetProperty("comment", out var cProp))
                                result.CommentTag = cProp.GetString() ?? string.Empty;
                            else if (tagsElement.TryGetProperty("COMMENT", out var cPropU))
                                result.CommentTag = cPropU.GetString() ?? string.Empty;
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
