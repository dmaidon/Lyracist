// Edited on Sep 6, 2026 @ 08:37:30 -> Add static ResolveAvatarImage and ResolveSingerAvatar helpers
using System;
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

    public static BitmapImage? ResolveAvatarImage(string? avatarType, string? avatarSource, bool fallbackToDefault = false)
    {
        if (avatarType == "Gravatar" && !string.IsNullOrEmpty(avatarSource))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"https://www.gravatar.com/avatar/{avatarSource}?d=identicon&s=150", UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                // Do not freeze remote images as they load asynchronously and cannot be frozen synchronously.
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
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = new MemoryStream(File.ReadAllBytes(fullPath));
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                return fallbackToDefault ? DefaultAvatar : null;
            }
        }

        return fallbackToDefault ? DefaultAvatar : null;
    }

    public static BitmapImage? ResolveSingerAvatar(Singer? singer, bool fallbackToDefault = false)
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
