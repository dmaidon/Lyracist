// Edited on Oct 2, 2026 @ 12:32:00 -> Use AvatarImageHelper for upright image decoding with EXIF orientation
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Lyracist.Models;
using Lyracist.Shared;

namespace Lyracist.Core.Helpers;

public class SingerAvatarConverter : IValueConverter
{
    private static BitmapImage? _defaultAvatar;
    private static bool _defaultAvatarAttempted;
    private static readonly object _lock = new();

    public static BitmapImage? DefaultAvatar
    {
        get
        {
            if (!_defaultAvatarAttempted)
            {
                lock (_lock)
                {
                    if (!_defaultAvatarAttempted)
                    {
                        _defaultAvatarAttempted = true;
                        _defaultAvatar = CreateDefaultAvatar();
                    }
                }
            }
            return _defaultAvatar;
        }
    }

    private static BitmapImage? CreateDefaultAvatar()
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri("pack://application:,,,/Assets/mic_128.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    // Decoded avatars keyed by source (plus the file's timestamp for uploads, so replacing an
    // avatar file under the same name is picked up). Every rotation update and every rotation-list
    // row used to re-read and re-decode the file, or start a fresh Gravatar download. Only used on
    // the UI thread: Gravatar bitmaps load asynchronously and can't be frozen, so they can't be
    // shared across threads.
    private static readonly Dictionary<string, BitmapSource> _imageCache = new(StringComparer.Ordinal);
    private const int MaxCachedImages = 256;

    private static bool OnUiThread => System.Windows.Application.Current?.Dispatcher.CheckAccess() == true;

    private static void CacheImage(string key, BitmapSource bitmap)
    {
        if (!OnUiThread) return;
        if (_imageCache.Count >= MaxCachedImages) _imageCache.Clear();
        _imageCache[key] = bitmap;
    }

    public static BitmapSource? ResolveAvatarImage(string? avatarType, string? avatarSource, bool fallbackToDefault = false)
    {
        if (avatarType == "Gravatar" && !string.IsNullOrEmpty(avatarSource))
        {
            string key = "G\u0001" + avatarSource;
            if (OnUiThread && _imageCache.TryGetValue(key, out var cached)) return cached;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"https://www.gravatar.com/avatar/{avatarSource}?d=identicon&s=150", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                // Do not freeze remote images as they load asynchronously and cannot be frozen synchronously.
                // A failed download must not stay cached, or the singer would show a blank avatar until restart.
                bitmap.DownloadFailed += (_, _) => _imageCache.Remove(key);
                CacheImage(key, bitmap);
                return bitmap;
            }
            catch
            {
                return fallbackToDefault ? DefaultAvatar : null;
            }
        }
        else if (avatarType == "Uploaded" && !string.IsNullOrEmpty(avatarSource))
        {
            try
            {
                string fullPath = Path.Combine(Globals.AvatarsDir, avatarSource);
                if (File.Exists(fullPath))
                {
                    string key = "U\u0001" + avatarSource + "\u0001" + File.GetLastWriteTimeUtc(fullPath).Ticks;
                    if (OnUiThread && _imageCache.TryGetValue(key, out var cached)) return cached;

                    var bitmap = AvatarImageHelper.LoadOrientedBitmapFromFile(fullPath);
                    if (bitmap != null)
                    {
                        CacheImage(key, bitmap);
                        return bitmap;
                    }
                }
            }
            catch
            {
                return fallbackToDefault ? DefaultAvatar : null;
            }
        }

        return fallbackToDefault ? DefaultAvatar : null;
    }

    public static BitmapSource? ResolveSingerAvatar(Singer? singer, bool fallbackToDefault = false)
    {
        if (singer == null) return fallbackToDefault ? DefaultAvatar : null;
        return ResolveAvatarImage(singer.AvatarType, singer.AvatarSource, fallbackToDefault);
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Singer singer)
        {
            return ResolveSingerAvatar(singer, fallbackToDefault: true);
        }
        return DefaultAvatar;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
