// Created on Sep 6, 2026 @ 12:46:00 -> Define IStoreProvider interface and ProviderSource enum for Store Plugin API
using System;
using Lyracist.Data.Services;

namespace Lyracist.Services.Store;

/// <summary>
/// Identifies the commercial or local provider origin of a karaoke track.
/// </summary>
public enum ProviderSource
{
    Local,
    KaraokeVersion,
    PartyTyme,
    Sunfly,
    KaraokeCom
}

/// <summary>
/// Modular plugin interface representing a commercial karaoke provider.
/// Encapsulates search URI construction and multi-vector file fingerprinting.
/// </summary>
public interface IStoreProvider
{
    /// <summary>
    /// Gets the user-friendly display name of the provider (e.g. "Karaoke Version").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the provider source enumeration value.
    /// </summary>
    ProviderSource Source { get; }

    /// <summary>
    /// Constructs a direct deep search URI for the specified query string.
    /// </summary>
    /// <param name="query">Song title, artist, or keywords.</param>
    /// <returns>Formatted search Uri for the provider.</returns>
    Uri BuildSearchUri(string query);

    /// <summary>
    /// Evaluates filename and title/artist heuristic patterns for provider signatures.
    /// </summary>
    /// <param name="filename">File name, path, or compound metadata title/artist text.</param>
    /// <returns>True if the filename matches provider heuristics; otherwise false.</returns>
    bool DetectFromFilename(string filename);

    /// <summary>
    /// Evaluates ID3 audio tag metadata (e.g., TXXX frames) for provider signatures.
    /// </summary>
    /// <param name="metadata">Extracted FFprobe stream and tag metadata.</param>
    /// <returns>True if ID3 tags identify this provider; otherwise false.</returns>
    bool DetectFromId3(FFprobeResult metadata);

    /// <summary>
    /// Inspects an archive's internal directory hierarchy, entry file names, and layout for provider signatures.
    /// </summary>
    /// <param name="zipPath">Full path to the ZIP archive on disk.</param>
    /// <returns>True if internal ZIP layout matches provider signatures; otherwise false.</returns>
    bool DetectFromZip(string zipPath);

    /// <summary>
    /// Evaluates the initial binary header bytes of a CDG file for provider magic fingerprints.
    /// </summary>
    /// <param name="header">Initial bytes read from the CDG file (at least 2 bytes).</param>
    /// <returns>True if header magic bytes match this provider; otherwise false.</returns>
    bool DetectFromCdgHeader(byte[] header);

    /// <summary>
    /// Evaluates MP4 container tags and metadata for provider signatures.
    /// </summary>
    /// <param name="metadata">Extracted FFprobe stream and tag metadata.</param>
    /// <returns>True if MP4 container metadata identifies this provider; otherwise false.</returns>
    bool DetectFromMp4(FFprobeResult metadata);
}
