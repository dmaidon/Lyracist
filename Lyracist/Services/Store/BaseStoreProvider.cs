// Created on Sep 6, 2026 @ 12:46:15 -> Implement BaseStoreProvider abstract class with default false detection and shared helper methods
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Lyracist.Core.Helpers;
using Lyracist.Data.Services;
using Lyracist.Shared;

namespace Lyracist.Services.Store;

/// <summary>
/// Abstract base class for store provider plugins providing default detection fallbacks and common inspection helpers.
/// </summary>
public abstract class BaseStoreProvider : IStoreProvider
{
    public abstract string Name { get; }
    public abstract ProviderSource Source { get; }

    public abstract Uri BuildSearchUri(string query);

    public virtual bool DetectFromFilename(string filename) => false;

    public virtual bool DetectFromId3(FFprobeResult metadata) => false;

    public virtual bool DetectFromZip(string zipPath) => false;

    public virtual bool DetectFromCdgHeader(byte[] header) => false;

    public virtual bool DetectFromMp4(FFprobeResult metadata) => false;

    /// <summary>
    /// Safely opens a ZIP archive, extracts normalized forward-slash entry names, and tests a predicate against them.
    /// </summary>
    protected bool MatchesZipEntries(string zipPath, Func<IReadOnlyList<string>, bool> predicate)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            return false;

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries
                .Select(e => e.FullName.Replace('\\', '/').ToLowerInvariant())
                .ToList();
            return predicate(entries);
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", $"BaseStoreProvider ({Name}) failed reading ZIP entries for: {zipPath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Iterates through extracted ID3 tags, executing a predicate against each key/value pair.
    /// </summary>
    protected bool MatchesId3Tags(FFprobeResult metadata, Func<string, string, bool> predicate)
    {
        if (metadata?.Tags == null || metadata.Tags.Count == 0)
            return false;

        foreach (var (key, val) in metadata.Tags)
        {
            string upperKey = key.ToUpperInvariant();
            string upperVal = (val ?? string.Empty).ToUpperInvariant();
            if (predicate(upperKey, upperVal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Combines common MP4 text tags (Title, Artist, Comment, and general tag values) into an uppercase string and tests against a predicate.
    /// </summary>
    protected bool MatchesMp4Metadata(FFprobeResult metadata, Func<string, bool> predicate)
    {
        if (metadata == null)
            return false;

        string combined = $"{metadata.TitleTag} {metadata.ArtistTag} {metadata.CommentTag} {string.Join(" ", metadata.Tags.Values)}".ToUpperInvariant();
        return predicate(combined);
    }
}
