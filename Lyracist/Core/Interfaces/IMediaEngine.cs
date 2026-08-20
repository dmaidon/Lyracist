// Edited on Aug 20, 2026 @ 09:52:45 -> Add SongEnded event to IMediaEngine for natural song finish notification
using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Lyracist.Core.Interfaces;

public interface IMediaEngine
{
    Task LoadSong(string path);
    Task Play();
    Task Pause();
    Task Stop();
    void Seek(double position);
    event Action<ImageSource>? FrameReady;

    /// <summary>Fires when a karaoke track transitions from stopped/paused into playing.</summary>
    event Action? Started;

    /// <summary>Fires whenever Stop() is called (song finished or was cut short).</summary>
    event Action? Stopped;

    /// <summary>Fires when a song finishes naturally (EndReached from media backend).</summary>
    event Action? SongEnded;

    double Volume { get; set; }
    double Speed { get; set; }
    int Pitch { get; set; }

    double Treble { get; set; }
    double Mid { get; set; }
    double Bass { get; set; }
    double Compressor { get; set; }
    double Limiter { get; set; }

    string? ActiveSingerName { get; set; }
    string? ActiveDuetPartnerName { get; set; }
    bool EnableKillVocal { get; set; }
    void UpdateAudioParameters();
}
