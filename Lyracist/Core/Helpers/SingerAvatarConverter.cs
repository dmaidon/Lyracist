// Edited on Aug 6, 2026 @ 09:26:30 -> Fix WPF Freezable exception by utilizing local default pack URI and removing remote freeze
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
    private static readonly BitmapImage DefaultAvatar = CreateDefaultAvatar();

    private static BitmapImage CreateDefaultAvatar()
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri("pack://application:,,,/Assets/mic_128.png", UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Singer singer)
        {
            if (singer.AvatarType == "Gravatar" && !string.IsNullOrEmpty(singer.AvatarSource))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri($"https://www.gravatar.com/avatar/{singer.AvatarSource}?d=identicon&s=150", UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    // Do not freeze remote images as they load asynchronously and cannot be frozen synchronously.
                    return bitmap;
                }
                catch
                {
                    return DefaultAvatar;
                }
            }
            else if (singer.AvatarType == "Uploaded" && !string.IsNullOrEmpty(singer.AvatarSource))
            {
                try
                {
                    string fullPath = Path.Combine(Globals.AvatarsDir, singer.AvatarSource);
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
                    return DefaultAvatar;
                }
            }
        }
        return DefaultAvatar;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
