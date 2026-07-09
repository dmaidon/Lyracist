using System;
using System.Windows.Media.Imaging;

namespace Lyracist.Media.Video;

public class VideoFrame(WriteableBitmap bitmap, TimeSpan position)
{
    public WriteableBitmap Bitmap { get; set; } = bitmap;
    public TimeSpan Position { get; set; } = position;
}
