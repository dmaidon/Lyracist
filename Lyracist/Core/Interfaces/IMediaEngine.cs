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

    /// <summary>Fires when a karaoke track transitions from stopped/paused into playing.</summary>
    event Action? Started;

    /// <summary>Fires whenever Stop() is called (song finished or was cut short).</summary>
    event Action? Stopped;

    double Volume { get; set; }
    double Speed { get; set; }
    int Pitch { get; set; }

    double Treble { get; set; }
    double Mid { get; set; }
    double Bass { get; set; }
    double Compressor { get; set; }
    double Limiter { get; set; }

    string? ActiveSingerName { get; set; }
    void UpdateAudioParameters();
}
