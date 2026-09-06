// Created on Sep 6, 2026 @ 12:46:30 -> Implement KaraokeVersionProvider plugin for Karaoke Version store
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lyracist.Data.Services;

namespace Lyracist.Services.Store.Providers;

/// <summary>
/// Store plugin for Karaoke-Version.com backing tracks and custom backing tracks.
/// </summary>
public sealed class KaraokeVersionProvider : BaseStoreProvider
{
    public override string Name => "Karaoke Version";
    public override ProviderSource Source => ProviderSource.KaraokeVersion;

    public override Uri BuildSearchUri(string query)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed)
            ? new Uri("https://www.karaoke-version.com/")
            : new Uri($"https://www.karaoke-version.com/search.html?q={Uri.EscapeDataString(trimmed)}");
    }

    public override bool DetectFromFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;
        string lower = filename.ToLowerInvariant();
        return lower.Contains("karaoke version") ||
               lower.Contains("karaoke-version") ||
               Regex.IsMatch(lower, @"\bkv[-\s]?\d+");
    }

    public override bool DetectFromId3(FFprobeResult metadata)
    {
        return MatchesId3Tags(metadata, (key, val) =>
            key.Contains("TXXX:KV") || key == "KV" || val.Contains("KARAOKE VERSION") || val == "KV");
    }

    public override bool DetectFromZip(string zipPath)
    {
        return MatchesZipEntries(zipPath, entries =>
        {
            bool hasCustomBackingTrack = entries.Any(e => e.Contains("custom_backing_track/"));
            bool hasKvName = entries.Any(e => e.Contains("karaoke version") || Regex.IsMatch(e, @"\bkv[-\s]?\d+"));
            bool hasGenericTrackPair = entries.Any(e => Path.GetFileName(e).Equals("track.mp3", StringComparison.OrdinalIgnoreCase)) &&
                                       entries.Any(e => Path.GetFileName(e).Equals("track.cdg", StringComparison.OrdinalIgnoreCase));

            return hasCustomBackingTrack || hasKvName || hasGenericTrackPair;
        });
    }

    public override bool DetectFromCdgHeader(byte[] header)
    {
        return header != null && header.Length >= 2 && header[0] == 0x01 && header[1] == 0x0F;
    }

    public override bool DetectFromMp4(FFprobeResult metadata)
    {
        return MatchesMp4Metadata(metadata, combined =>
            combined.Contains("KARAOKE VERSION") || combined.Contains("KARAOKE-VERSION"));
    }
}
