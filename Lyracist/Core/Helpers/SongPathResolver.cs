// Edited on Oct 30, 2026 @ 10:43:00 -> Add root/negative lookup caching and zero-I/O ResolveCachedOrOriginal for fast list mapping
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lyracist.Core.Helpers;

/// <summary>
/// Probes logical drives and configured music/karaoke directories to dynamically resolve
/// song file paths when drive letters shift (e.g. historical E:\ scan vs active F:\ mount).
/// </summary>
public static class SongPathResolver
{
    private static readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, long> _missingCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _rootsLock = new();
    private static List<string> _cachedRoots = [];
    private static long _rootsCacheTicks;
    private const long RootsCacheTtlMs = 5_000;
    private const long MissingCacheTtlMs = 15_000;

    /// <summary>
    /// Returns a previously resolved path from the in-memory cache if available, or the original path
    /// without performing synchronous disk I/O. Ideal for bulk list/search mapping.
    /// </summary>
    public static string ResolveCachedOrOriginal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return _cache.TryGetValue(path, out var cached) ? cached : path;
    }

    /// <summary>
    /// Clears cached path resolutions (e.g. after a library rescan).
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _missingCache.Clear();
        lock (_rootsLock)
        {
            _rootsCacheTicks = 0;
        }
    }

    /// <summary>
    /// Attempts to resolve a file path to an existing file on any connected drive or library directory.
    /// Returns the resolved existing path if found, or the original path if not found.
    /// </summary>
    public static string ResolveSongPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (File.Exists(path))
        {
            return path;
        }

        if (_cache.TryGetValue(path, out var cached) && File.Exists(cached))
        {
            return cached;
        }

        long now = Environment.TickCount64;
        if (_missingCache.TryGetValue(path, out long failedAt) && (now - failedAt) < MissingCacheTtlMs)
        {
            return path;
        }

        if (TryResolveInternal(path, out var resolved))
        {
            _cache[path] = resolved;
            _missingCache.TryRemove(path, out _);
            return resolved;
        }

        _missingCache[path] = now;
        return path;
    }

    /// <summary>
    /// Tries to resolve a song path to a file that exists on disk.
    /// </summary>
    public static bool TryResolveSongPath(string? path, out string resolvedPath)
    {
        resolvedPath = ResolveSongPath(path);
        return !string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath);
    }

    private static bool TryResolveInternal(string originalPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        // Strip drive letter if present (e.g. "E:\" or "E:/")
        string relativePath = Regex.Replace(originalPath, @"^[a-zA-Z]:[\\/]+", string.Empty);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        string fileName = Path.GetFileName(originalPath);
        string? parentDirName = null;
        try
        {
            var dir = Path.GetDirectoryName(originalPath);
            if (!string.IsNullOrEmpty(dir))
            {
                parentDirName = Path.GetFileName(dir);
            }
        }
        catch { }

        // Generate relative variations
        var relativeVariations = new List<string> { relativePath };

        if (relativePath.StartsWith("Music\\", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("Music/", StringComparison.OrdinalIgnoreCase))
        {
            relativeVariations.Add(relativePath.Substring(6).TrimStart('\\', '/'));
        }
        else
        {
            relativeVariations.Add(Path.Combine("Music", relativePath));
        }

        if (relativePath.StartsWith("Karaoke\\", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith("Karaoke/", StringComparison.OrdinalIgnoreCase))
        {
            relativeVariations.Add(relativePath.Substring(8).TrimStart('\\', '/'));
        }
        else
        {
            relativeVariations.Add(Path.Combine("Karaoke", relativePath));
        }

        // Collect search root candidates (cached briefly to avoid repeated DriveInfo enumeration)
        var roots = GetSearchRoots();

        // Probe each root with all relative variations
        foreach (var root in roots)
        {
            foreach (var rel in relativeVariations)
            {
                try
                {
                    string candidate = Path.Combine(root, rel);
                    if (File.Exists(candidate))
                    {
                        resolvedPath = candidate;
                        return true;
                    }
                }
                catch { }
            }

            // Fallback: match by immediate parent folder + filename (e.g. "Eagles\Take It Easy.mp3")
            if (!string.IsNullOrEmpty(parentDirName) && !string.IsNullOrEmpty(fileName))
            {
                string[] parentCandidates = new[]
                {
                    Path.Combine(root, parentDirName, fileName),
                    Path.Combine(root, "Music", parentDirName, fileName),
                    Path.Combine(root, "Karaoke", parentDirName, fileName)
                };

                foreach (var candidate in parentCandidates)
                {
                    try
                    {
                        if (File.Exists(candidate))
                        {
                            resolvedPath = candidate;
                            return true;
                        }
                    }
                    catch { }
                }
            }
        }

        return false;
    }

    private static List<string> GetSearchRoots()
    {
        long now = Environment.TickCount64;
        lock (_rootsLock)
        {
            if (_cachedRoots.Count > 0 && (now - _rootsCacheTicks) < RootsCacheTtlMs)
            {
                return _cachedRoots;
            }
        }

        var roots = new List<string>();

        // 1. Ready system drives
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    roots.Add(drive.RootDirectory.FullName);
                }
            }
        }
        catch { }

        // 2. Configured library directories from AppSettings
        try
        {
            if (AppSettings.LibraryDirectories != null)
            {
                foreach (var dir in AppSettings.LibraryDirectories)
                {
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !roots.Contains(dir, StringComparer.OrdinalIgnoreCase))
                    {
                        roots.Add(dir);
                    }
                }
            }
        }
        catch { }

        lock (_rootsLock)
        {
            _cachedRoots = roots;
            _rootsCacheTicks = now;
        }

        return roots;
    }
}
