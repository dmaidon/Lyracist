// Edited on Sep 6, 2026 @ 07:33:00 -> Add ActivePerformerKey and ActivePerformerTempo to IMediaEngine for per-performer audio recall
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
    void Seek(double positionSeconds);
    event Action<ImageSource>? FrameReady;

    /// <summary>Fires when a karaoke track transitions from stopped/paused into playing.</summary>
    event Action? Started;

    /// <summary>Fires whenever Stop() is called (song finished or was cut short).</summary>
    event Action? Stopped;

    /// <summary>Fires when a song finishes naturally (EndReached from media backend).</summary>
    event Action? SongEnded;

    /// <summary>Fires periodically while a song is loaded so a seek slider can track playback
    /// progress; not raised for every frame, just enough to look smooth in the UI.</summary>
    event Action? PositionChanged;

    /// <summary>Current playback position of the loaded song, in seconds.</summary>
    double Position { get; }

    /// <summary>Total length of the loaded song, in seconds, or 0 if unknown/nothing loaded.</summary>
    double Duration { get; }

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
    string? ActivePerformerKey { get; set; }
    double ActivePerformerTempo { get; set; }
    bool EnableKillVocal { get; set; }
    void UpdateAudioParameters();
}

