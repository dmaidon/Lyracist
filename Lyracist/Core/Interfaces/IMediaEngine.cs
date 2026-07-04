using System;
using System.Windows.Media;

namespace Lyracist.Core.Interfaces;

public interface IMediaEngine
{
    void LoadSong(string path);
    void Play();
    void Pause();
    void Stop();
    void Seek(double position);
    event Action<ImageSource>? FrameReady;

    double Volume { get; set; }
    double Speed { get; set; }
    int Pitch { get; set; }
}
