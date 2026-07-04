using System;
using System.IO;

namespace Lyracist.Services.Media;

public class MediaTrack
{
    public MediaType Type { get; private set; }
    public string AudioPath { get; private set; } = "";
    public string CdgPath { get; private set; } = "";
    public string VideoPath { get; private set; } = "";

    public static MediaTrack FromPath(string path)
    {
        var ext = Path.GetExtension(path).ToLower();

        if (ext == ".mp4")
        {
            return new MediaTrack
            {
                Type = MediaType.Mp4,
                VideoPath = path
            };
        }

        if (ext == ".mp3")
        {
            var cdg = Path.ChangeExtension(path, ".cdg");
            return new MediaTrack
            {
                Type = MediaType.Mp3G,
                AudioPath = path,
                CdgPath = cdg
            };
        }

        throw new NotSupportedException("Unsupported karaoke format.");
    }
}