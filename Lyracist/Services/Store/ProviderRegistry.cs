// Created on Sep 6, 2026 @ 12:47:30 -> Implement ProviderRegistry central registry for store plugins
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lyracist.Core.Helpers;
using Lyracist.Data.Services;
using Lyracist.Services.Store.Providers;
using Lyracist.Shared;

namespace Lyracist.Services.Store;

/// <summary>
/// Central registry managing commercial karaoke store provider plugins and cross-vector detection heuristics.
/// </summary>
public sealed class ProviderRegistry
{
    private static readonly Lazy<ProviderRegistry> _lazy = new(() => new ProviderRegistry());
    public static ProviderRegistry Instance => _lazy.Value;

    private readonly List<IStoreProvider> _providers = new();
    private readonly object _lock = new();

    public ProviderRegistry()
    {
        // Register default commercial karaoke providers
        RegisterProvider(new KaraokeVersionProvider());
        RegisterProvider(new PartyTymeProvider());
        RegisterProvider(new SunflyProvider());
        RegisterProvider(new KaraokeComProvider());
    }

    /// <summary>
    /// Gets a snapshot of all currently registered store providers.
    /// </summary>
    public IReadOnlyList<IStoreProvider> Providers
    {
        get
        {
            lock (_lock)
            {
                return _providers.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Registers a new or custom store provider plugin.
    /// </summary>
    public void RegisterProvider(IStoreProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (_lock)
        {
            // If already registered with the same Source or Name, replace it
            _providers.RemoveAll(p => p.Source == provider.Source || string.Equals(p.Name, provider.Name, StringComparison.OrdinalIgnoreCase));
            _providers.Add(provider);
        }
    }

    /// <summary>
    /// Finds a registered provider by its enum source value.
    /// </summary>
    public IStoreProvider? GetProviderBySource(ProviderSource source)
    {
        lock (_lock)
        {
            return _providers.FirstOrDefault(p => p.Source == source);
        }
    }

    /// <summary>
    /// Finds a registered provider by its display name or common abbreviation.
    /// </summary>
    public IStoreProvider? GetProviderByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        string trimmed = name.Trim();
        lock (_lock)
        {
            return _providers.FirstOrDefault(p =>
                string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Source.ToString(), trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Detects a provider from filename heuristics or title/artist text.
    /// </summary>
    public IStoreProvider? DetectProviderFromFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;

        lock (_lock)
        {
            foreach (var provider in _providers)
            {
                if (provider.DetectFromFilename(filename))
                    return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects a provider from internal ZIP structure and contents.
    /// </summary>
    public IStoreProvider? DetectProviderFromZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) return null;

        lock (_lock)
        {
            foreach (var provider in _providers)
            {
                if (provider.DetectFromZip(zipPath))
                    return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects a provider from CDG magic header bytes.
    /// </summary>
    public IStoreProvider? DetectProviderFromCdg(byte[] header)
    {
        if (header == null || header.Length < 2) return null;

        lock (_lock)
        {
            foreach (var provider in _providers)
            {
                if (provider.DetectFromCdgHeader(header))
                    return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the initial 24 bytes of a CDG file from disk and tests against registered provider header fingerprints.
    /// </summary>
    public IStoreProvider? DetectProviderFromCdg(string cdgPath)
    {
        if (string.IsNullOrWhiteSpace(cdgPath) || !File.Exists(cdgPath)) return null;

        try
        {
            using var stream = new FileStream(cdgPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] header = new byte[24];
            int read = stream.Read(header, 0, 24);
            if (read < 2) return null;

            return DetectProviderFromCdg(header);
        }
        catch (Exception ex)
        {
            Globals.LogError("Lyracist", $"Failed reading CDG header for provider detection: {cdgPath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Detects a provider from extracted ID3 tag frames.
    /// </summary>
    public IStoreProvider? DetectProviderFromId3(FFprobeResult metadata)
    {
        if (metadata == null) return null;

        lock (_lock)
        {
            foreach (var provider in _providers)
            {
                if (provider.DetectFromId3(metadata))
                    return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects a provider from MP4 container metadata tags.
    /// </summary>
    public IStoreProvider? DetectProviderFromMp4(FFprobeResult metadata)
    {
        if (metadata == null) return null;

        lock (_lock)
        {
            foreach (var provider in _providers)
            {
                if (provider.DetectFromMp4(metadata))
                    return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects a provider from technical metadata (evaluates ID3 tags first, then MP4 metadata).
    /// </summary>
    public IStoreProvider? DetectProviderFromMetadata(FFprobeResult metadata)
    {
        if (metadata == null) return null;
        return DetectProviderFromId3(metadata) ?? DetectProviderFromMp4(metadata);
    }
}
