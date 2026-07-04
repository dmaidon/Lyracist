using System;
using System.Threading.Tasks;

namespace Lyracist.Media.Video;

public interface IVideoBackend
{
    event EventHandler<VideoFrame>? FrameReady;

    Task LoadAsync(string path);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();

    TimeSpan Position { get; }
    bool IsPlaying { get; }

    double Volume { get; set; }
    double Speed { get; set; }
    int Pitch { get; set; }
}
