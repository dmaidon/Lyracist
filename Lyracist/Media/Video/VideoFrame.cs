using System;
using System.Windows.Media.Imaging;

namespace Lyracist.Media.Video;

public class VideoFrame
{
    public WriteableBitmap Bitmap { get; set; }
    public TimeSpan Position { get; set; }

    public VideoFrame(WriteableBitmap bitmap, TimeSpan position)
    {
        Bitmap = bitmap;
        Position = position;
    }
}
