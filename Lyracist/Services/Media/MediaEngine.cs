using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lyracist.Core.Interfaces;
using Lyracist.Media.Video;
using Lyracist.Services.Media.Cdg;

namespace Lyracist.Services.Media;

public class MediaEngine : Lyracist.Core.Interfaces.IMediaEngine
{
    private readonly ICDGDecoder _cdgDecoder;
    private readonly ICdgFrameScheduler _scheduler;
    private readonly IVideoBackend _video;
    private DispatcherTimer? _timer;
    private double _position;
    private bool _isPlaying;
    private DateTime _lastTickTime;
    private bool _isMp4Mode;

    public event Action<ImageSource>? FrameReady;

    public double Volume
    {
        get => _video.Volume;
        set => _video.Volume = value;
    }

    public double Speed
    {
        get => _video.Speed;
        set => _video.Speed = value;
    }

    public int Pitch
    {
        get => _video.Pitch;
        set => _video.Pitch = value;
    }

    public MediaEngine(ICDGDecoder cdgDecoder, ICdgFrameScheduler scheduler, IVideoBackend video)
    {
        _cdgDecoder = cdgDecoder;
        _scheduler = scheduler;
        _video = video;

        _video.FrameReady += OnVideoFrameReady;
        InitializePlaybackTimer();
    }

    private void InitializePlaybackTimer()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _timer.Tick += OnPlaybackTick;
    }

    private void OnVideoFrameReady(object? sender, VideoFrame frame)
    {
        if (_isMp4Mode)
        {
            FrameReady?.Invoke(frame.Bitmap);
        }
    }

    private void OnPlaybackTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _isMp4Mode) return;

        // Sync CDG frame scheduler directly using the backend's audio position
        var audioPosition = _video.Position;
        _scheduler.Update(audioPosition);
        var frame = _scheduler.GetFrame();

        if (frame != null)
        {
            FrameReady?.Invoke(frame);
        }
    }

    public void LoadSong(string path)
    {
        Stop();
        _position = 0;

        if (string.IsNullOrEmpty(path)) return;

        _isMp4Mode = path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        // Always load the file (mp4 or mp3 audio) in the unmanaged video player to play the audio track
        Task.Run(async () => await _video.LoadAsync(path));

        if (!_isMp4Mode)
        {
            // CDG files share the same folder and filename base as the audio file (.mp3)
            string cdgPath = Path.ChangeExtension(path, ".cdg");

            // Load the CDG packets in a task wrapper to keep UI responsive
            Task.Run(async () =>
            {
                await _cdgDecoder.LoadAsync(cdgPath);
                if (_cdgDecoder is CdgDecoder cdg)
                {
                    _scheduler.LoadPackets(cdg.Packets);
                    _scheduler.Reset();
                }
            });
        }
    }

    public void Play()
    {
        if (_isPlaying) return;
        _isPlaying = true;

        // Start playback on video/audio backend
        Task.Run(async () => await _video.PlayAsync());

        if (!_isMp4Mode)
        {
            _lastTickTime = DateTime.UtcNow;
            _timer?.Start();
        }
    }

    public void Pause()
    {
        _isPlaying = false;

        Task.Run(async () => await _video.PauseAsync());

        if (!_isMp4Mode)
        {
            _timer?.Stop();
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _position = 0;

        Task.Run(async () => await _video.StopAsync());

        if (!_isMp4Mode)
        {
            _timer?.Stop();
            _scheduler.Reset();
        }
    }

    public void Seek(double position)
    {
        _position = position;
        if (!_isMp4Mode)
        {
            _lastTickTime = DateTime.UtcNow;
        }
    }
}
