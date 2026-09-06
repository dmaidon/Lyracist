// Edited on Sep 6, 2026 @ 11:20:00 -> Add Width/Height and Smart Import detection helpers (Genre, Difficulty, Key, BPM, VocalPresence, Quality)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        public int BitrateKbps { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int AudioStreamCount { get; set; }
        public bool HasDualAudio => AudioStreamCount >= 2;
        public string VideoStreamInfo { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static class FFprobeRunner
    {
        public static string FFprobePath { get; set; } = "ffprobe";

        public static async Task<FFprobeResult> ProbeFile(string filePath)
        {
            var result = new FFprobeResult();

            if (!File.Exists(filePath))
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"FFprobe failed: file does not exist: {filePath}", "FFprobeRunner.ProbeFile");
                return result;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = FFprobePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-v");
                startInfo.ArgumentList.Add("error");
                startInfo.ArgumentList.Add("-show_format");
                startInfo.ArgumentList.Add("-show_streams");
                startInfo.ArgumentList.Add("-print_format");
                startInfo.ArgumentList.Add("json");
                startInfo.ArgumentList.Add(filePath);

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Read stdout and stderr concurrently to avoid deadlocking on a full pipe buffer.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                string output = outputTask.Result;
                string error = errorTask.Result;

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

                        if (formatElement.TryGetProperty("bit_rate", out var brProp))
                        {
                            string brStr = brProp.GetString() ?? string.Empty;
                            if (long.TryParse(brStr, out long br) && br > 0)
                            {
                                result.BitrateKbps = (int)(br / 1000);
                            }
                        }

                        if (formatElement.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in tagsElement.EnumerateObject())
                            {
                                result.Tags[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();
                            }

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
                            if (stream.TryGetProperty("tags", out var streamTags) && streamTags.ValueKind == JsonValueKind.Object)
                            {
                                foreach (var prop in streamTags.EnumerateObject())
                                {
                                    if (!result.Tags.ContainsKey(prop.Name))
                                        result.Tags[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();
                                }
                            }

                            if (stream.TryGetProperty("codec_type", out var typeProp) &&
                                stream.TryGetProperty("codec_name", out var nameProp))
                            {
                                string type = typeProp.GetString() ?? string.Empty;
                                string codec = nameProp.GetString() ?? string.Empty;

                                if (type == "audio")
                                {
                                    result.AudioStreamCount++;
                                    if (string.IsNullOrEmpty(result.AudioCodec))
                                    {
                                        result.AudioCodec = codec;
                                        if (stream.TryGetProperty("sample_rate", out var srProp) && int.TryParse(srProp.GetString(), out int sr))
                                            result.SampleRate = sr;
                                        if (stream.TryGetProperty("channels", out var chProp) && chProp.TryGetInt32(out int ch))
                                            result.Channels = ch;
                                    }
                                }
                                else if (type == "video" && string.IsNullOrEmpty(result.VideoCodec))
                                {
                                    result.VideoCodec = codec;
                                    int w = stream.TryGetProperty("width", out var wProp) && wProp.TryGetInt32(out int width) ? width : 0;
                                    int h = stream.TryGetProperty("height", out var hProp) && hProp.TryGetInt32(out int height) ? height : 0;
                                    result.Width = w;
                                    result.Height = h;
                                    if (w > 0 && h > 0)
                                    {
                                        result.VideoStreamInfo = $"{w}x{h} ({codec})";
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Lyracist.Shared.Globals.LogError("Lyracist", $"FFprobe process exited with code {process.ExitCode}. Error: {error}", "FFprobeRunner.ProbeFile");
                }
            }
            catch (Exception ex)
            {
                Lyracist.Shared.Globals.LogError("Lyracist", $"Exception executing FFprobe for {filePath}", ex);
            }

            return result;
        }

        // ==========================================
        // SMART IMPORT DETECTION & HEURISTICS
        // ==========================================

        private static readonly string[] KvGenres = ["Pop", "Rock", "Country", "Soul", "R&B", "Jazz"];
        private static readonly string[] PtGenres = ["Pop", "Rock", "Hip-Hop", "Gospel"];
        private static readonly string[] SunflyGenres = ["Pop", "Rock", "Dance"];
        private static readonly string[] KcomGenres = ["Pop", "Rock", "Country", "R&B", "Standards"];

        public static string? DetectGenre(string provider, FFprobeResult metadata)
        {
            var candidateList = provider switch
            {
                "Karaoke Version" => KvGenres,
                "Party Tyme" => PtGenres,
                "Sunfly" => SunflyGenres,
                "Karaoke.com" => KcomGenres,
                _ => [.. KvGenres, .. PtGenres, .. SunflyGenres]
            };

            string raw = metadata.GenreTag;
            if (string.IsNullOrWhiteSpace(raw) && metadata.Tags.TryGetValue("genre", out var gTag)) raw = gTag;
            if (string.IsNullOrWhiteSpace(raw) && metadata.Tags.TryGetValue("style", out var sTag)) raw = sTag;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                string clean = raw.Trim();
                foreach (var g in candidateList)
                {
                    if (clean.Contains(g, StringComparison.OrdinalIgnoreCase)) return g;
                }
                return clean;
            }

            // Fallback: check comments or title
            string combined = $"{metadata.CommentTag} {metadata.TitleTag}".ToLowerInvariant();
            foreach (var g in candidateList)
            {
                if (combined.Contains(g.ToLowerInvariant())) return g;
            }

            return null;
        }

        public static string DetectDifficulty(FFprobeResult metadata)
        {
            if (metadata.Tags.TryGetValue("difficulty", out var diffTag) || metadata.Tags.TryGetValue("level", out diffTag))
            {
                if (diffTag.Contains("easy", StringComparison.OrdinalIgnoreCase) || diffTag.Contains("beginner", StringComparison.OrdinalIgnoreCase)) return "Easy";
                if (diffTag.Contains("hard", StringComparison.OrdinalIgnoreCase) || diffTag.Contains("expert", StringComparison.OrdinalIgnoreCase) || diffTag.Contains("advanced", StringComparison.OrdinalIgnoreCase)) return "Hard";
                if (diffTag.Contains("med", StringComparison.OrdinalIgnoreCase)) return "Medium";
            }

            int score = 0;
            double? bpm = DetectBpm(metadata);
            if (bpm.HasValue)
            {
                if (bpm.Value > 140 || bpm.Value < 70) score += 2;
                else if (bpm.Value >= 90 && bpm.Value <= 125) score += 0;
                else score += 1;
            }

            if (metadata.Duration > 260) score += 2;
            else if (metadata.Duration > 200) score += 1;

            if (metadata.Tags.TryGetValue("vocal_range", out var vr) && (vr.Contains("wide", StringComparison.OrdinalIgnoreCase) || vr.Contains("3 oct", StringComparison.OrdinalIgnoreCase)))
            {
                score += 2;
            }

            if (score >= 4) return "Hard";
            if (score >= 2) return "Medium";
            return "Easy";
        }

        public static string? DetectKey(FFprobeResult metadata)
        {
            string? raw = null;
            if (metadata.Tags.TryGetValue("initialkey", out var ik)) raw = ik;
            else if (metadata.Tags.TryGetValue("TKEY", out var tk)) raw = tk;
            else if (metadata.Tags.TryGetValue("key", out var k)) raw = k;

            if (!string.IsNullOrWhiteSpace(raw)) return raw.Trim();

            string comment = $"{metadata.CommentTag} {(metadata.Tags.TryGetValue("comment", out var c) ? c : "")}";
            var match = Regex.Match(comment, @"(?:key[:\s]+(?:of\s+)?)([A-G][#b]?(?:m|maj|min|minor|major)?)\b", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value.Trim();

            return null;
        }

        public static double? DetectBpm(FFprobeResult metadata)
        {
            string? raw = null;
            if (metadata.Tags.TryGetValue("TBPM", out var tb)) raw = tb;
            else if (metadata.Tags.TryGetValue("bpm", out var b)) raw = b;
            else if (metadata.Tags.TryGetValue("tempo", out var t)) raw = t;

            if (!string.IsNullOrWhiteSpace(raw) && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedBpm) && parsedBpm > 20 && parsedBpm < 300)
            {
                return Math.Round(parsedBpm);
            }

            string comment = $"{metadata.CommentTag} {(metadata.Tags.TryGetValue("comment", out var c) ? c : "")}";
            var match = Regex.Match(comment, @"(\d{2,3}(?:\.\d+)?)\s*(?:bpm|tempo)", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cBpm) && cBpm > 20 && cBpm < 300)
            {
                return Math.Round(cBpm);
            }

            return null;
        }

        public static string DetectVocalPresence(FFprobeResult metadata)
        {
            if (metadata.HasDualAudio) return "guide vocals";

            string combined = $"{metadata.TitleTag} {metadata.CommentTag}".ToLowerInvariant();
            foreach (var kvp in metadata.Tags)
            {
                combined += " " + kvp.Value.ToLowerInvariant();
            }

            if (combined.Contains("guide vocal") || combined.Contains("lead vocal") || combined.Contains("with vocal") || combined.Contains("+ vocal"))
                return "guide vocals";

            if (combined.Contains("backing vocal") || combined.Contains("bgv") || combined.Contains("chorus") || combined.Contains("harmony") || combined.Contains("with bv"))
                return "background vocals";

            if (combined.Contains("instrumental") || combined.Contains("karaoke") || combined.Contains("no vocal") || combined.Contains("backing track"))
                return "no vocals";

            return "no vocals";
        }

        public static string DetectQuality(FFprobeResult metadata)
        {
            bool isVideo = !string.IsNullOrEmpty(metadata.VideoCodec) || !string.IsNullOrEmpty(metadata.VideoStreamInfo);

            if (isVideo)
            {
                int h = metadata.Height;
                int w = metadata.Width;
                string info = metadata.VideoStreamInfo.ToLowerInvariant();

                bool is1080p = h >= 1080 || w >= 1920 || info.Contains("1080") || info.Contains("1920");
                bool is720p = h >= 720 || w >= 1280 || info.Contains("720") || info.Contains("1280");

                if (is1080p) return "High";
                if (is720p) return metadata.BitrateKbps >= 192 ? "High" : "Medium";
                return "Low";
            }

            int br = metadata.BitrateKbps;
            int sr = metadata.SampleRate;
            int ch = metadata.Channels;
            string codec = metadata.AudioCodec.ToLowerInvariant();

            if (codec.Contains("flac") || codec.Contains("alac") || codec.Contains("wav")) return "High";
            if (br >= 256 && sr >= 44100 && ch >= 2) return "High";
            if (br >= 160 && sr >= 44100 && ch >= 2) return "Medium";
            if (br >= 128 && codec.Contains("aac")) return "Medium";

            return "Low";
        }
    }
}
