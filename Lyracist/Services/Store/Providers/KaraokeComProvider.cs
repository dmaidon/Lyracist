// Created on Sep 6, 2026 @ 12:47:15 -> Implement KaraokeComProvider plugin for Karaoke.com store
using System;
using System.Linq;
using Lyracist.Data.Services;

namespace Lyracist.Services.Store.Providers;

/// <summary>
/// Store plugin for Karaoke.com online downloads and digital backing tracks.
/// </summary>
public sealed class KaraokeComProvider : BaseStoreProvider
{
    public override string Name => "Karaoke.com";
    public override ProviderSource Source => ProviderSource.KaraokeCom;

    public override Uri BuildSearchUri(string query)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed)
            ? new Uri("https://karaoke.com/")
            : new Uri($"https://karaoke.com/search?type=product&q={Uri.EscapeDataString(trimmed)}");
    }

    public override bool DetectFromFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;
        string lower = filename.ToLowerInvariant();
        return lower.Contains("karaoke.com") || lower.Contains("karaokedotcom");
    }

    public override bool DetectFromId3(FFprobeResult metadata)
    {
        return MatchesId3Tags(metadata, (key, val) =>
            key.Contains("TXXX:KCOM") || key == "KCOM" || key == "KARAOKECOM" || val.Contains("KARAOKE.COM") || val == "KCOM");
    }

    public override bool DetectFromZip(string zipPath)
    {
        return MatchesZipEntries(zipPath, entries =>
            entries.Any(e => e.Contains("kcom") || e.Contains("karaokecom") || e.Contains("karaoke.com")));
    }

    public override bool DetectFromCdgHeader(byte[] header)
    {
        return false;
    }

    public override bool DetectFromMp4(FFprobeResult metadata)
    {
        return MatchesMp4Metadata(metadata, combined =>
            combined.Contains("KARAOKE.COM") || combined.Contains("KARAOKEDOTCOM"));
    }
}
