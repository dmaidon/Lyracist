// Edited on Sep 6, 2026 @ 13:35:00 -> Fix search URL to ?s={query}&post_type=product to prevent 410
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lyracist.Data.Services;

namespace Lyracist.Services.Store.Providers;

/// <summary>
/// Store plugin for Sunfly Karaoke commercial backing tracks.
/// </summary>
public sealed class SunflyProvider : BaseStoreProvider
{
    public override string Name => "Sunfly";
    public override ProviderSource Source => ProviderSource.Sunfly;

    public override Uri BuildSearchUri(string query)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed)
            ? new Uri("https://www.sunflykaraoke.com/")
            : new Uri($"https://www.sunflykaraoke.com/?s={Uri.EscapeDataString(trimmed)}&post_type=product");
    }

    public override bool DetectFromFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;
        string lower = filename.ToLowerInvariant();
        return lower.Contains("sunfly") ||
               Regex.IsMatch(lower, @"\bsf[-\s]?\d+");
    }

    public override bool DetectFromId3(FFprobeResult metadata)
    {
        return MatchesId3Tags(metadata, (key, val) =>
            key.Contains("TXXX:SF") || key == "SF" || val.Contains("SUNFLY") || val == "SF");
    }

    public override bool DetectFromZip(string zipPath)
    {
        return MatchesZipEntries(zipPath, entries =>
            entries.Any(e => Path.GetFileName(e).StartsWith("sf", StringComparison.OrdinalIgnoreCase) || e.Contains("sunfly")));
    }

    public override bool DetectFromCdgHeader(byte[] header)
    {
        return header != null && header.Length >= 2 && header[0] == 0x03 && header[1] == 0x0C;
    }

    public override bool DetectFromMp4(FFprobeResult metadata)
    {
        return MatchesMp4Metadata(metadata, combined => combined.Contains("SUNFLY"));
    }
}
