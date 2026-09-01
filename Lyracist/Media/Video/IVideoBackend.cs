// Edited on Aug 20, 2026 @ 09:52:00 -> Add EndReached event to IVideoBackend
using System;
using System.Threading.Tasks;

namespace Lyracist.Media.Video;

public interface IVideoBackend
{
    event EventHandler<VideoFrame>? FrameReady;
    event EventHandler? EndReached;

    Task LoadAsync(string path);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();
    Task SeekAsync(TimeSpan position);

    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    bool IsPlaying { get; }

    double Volume { get; set; }
    double Speed { get; set; }
    int Pitch { get; set; }

    double Treble { get; set; }
    double Mid { get; set; }
    double Bass { get; set; }
    double Compressor { get; set; }
    double Limiter { get; set; }
    bool EnableKillVocal { get; set; }
    string? AudioDeviceId { get; set; }
}
