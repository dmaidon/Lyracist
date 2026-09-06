// Edited on Sep 6, 2026 @ 13:35:00 -> Fix search URL to songshop/cat/search.php to prevent 404
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Lyracist.Data.Services;

namespace Lyracist.Services.Store.Providers;

/// <summary>
/// Store plugin for Party Tyme Karaoke (Sybersound) commercial backing tracks.
/// </summary>
public sealed class PartyTymeProvider : BaseStoreProvider
{
    public override string Name => "Party Tyme";
    public override ProviderSource Source => ProviderSource.PartyTyme;

    public override Uri BuildSearchUri(string query)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(trimmed)
            ? new Uri("https://www.partytyme.net/")
            : new Uri($"https://www.partytyme.net/songshop/cat/search.php?search_what=all&search_keyword={Uri.EscapeDataString(trimmed)}&submit=GO");
    }

    public override bool DetectFromFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;
        string lower = filename.ToLowerInvariant();
        return lower.Contains("party tyme") ||
               lower.Contains("partytyme") ||
               lower.Contains("sybersound") ||
               Regex.IsMatch(lower, @"\bpt[-\s]?\d+");
    }

    public override bool DetectFromId3(FFprobeResult metadata)
    {
        return MatchesId3Tags(metadata, (key, val) =>
            key.Contains("TXXX:PT") || key == "PT" || val.Contains("PARTY TYME") || val == "PT" || val.Contains("SYBERSOUND"));
    }

    public override bool DetectFromZip(string zipPath)
    {
        return MatchesZipEntries(zipPath, entries =>
        {
            bool hasKaraokeFolder = entries.Any(e => e.Contains("karaoke/"));
            bool hasPtName = entries.Any(e => e.Contains("_pt.") || e.Contains("- pt.") || e.Contains("party tyme") || Regex.IsMatch(e, @"\bpt[-\s]?\d+"));
            return hasKaraokeFolder || hasPtName;
        });
    }

    public override bool DetectFromCdgHeader(byte[] header)
    {
        return header != null && header.Length >= 2 && header[0] == 0x02 && header[1] == 0x0A;
    }

    public override bool DetectFromMp4(FFprobeResult metadata)
    {
        return MatchesMp4Metadata(metadata, combined =>
            combined.Contains("PARTY TYME") || combined.Contains("PARTYTYME") || combined.Contains("SYBERSOUND"));
    }
}
