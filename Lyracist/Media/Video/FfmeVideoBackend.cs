using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Unosquare.FFME;
using Unosquare.FFME.Common;

namespace Lyracist.Media.Video;

public class FfmeVideoBackend : IVideoBackend, IDisposable
{
    private MediaElement? _mediaElement;
    private WriteableBitmap? _bitmap;
    private bool _isDisposed;
    private double _volume = 100.0;
    private double _speed = 1.0;
    private int _pitchShift = 0;

    public event EventHandler<VideoFrame>? FrameReady;

    public TimeSpan Position => _mediaElement != null ? _mediaElement.Position : TimeSpan.Zero;
    public bool IsPlaying => _mediaElement != null && _mediaElement.IsPlaying;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 100.0);
            _mediaElement?.Volume = _volume / 100.0;
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.5, 2.0);
            _mediaElement?.SpeedRatio = _speed;
        }
    }

    public int Pitch
    {
        get => _pitchShift;
        set => _pitchShift = Math.Clamp(value, -6, 6);
    }

    public double Treble { get; set; } = 0.0;
    public double Mid { get; set; } = 0.0;
    public double Bass { get; set; } = 0.0;
    public double Compressor { get; set; } = 0.0;
    public double Limiter { get; set; } = 0.0;
    public bool EnableKillVocal { get; set; } = false;

    public FfmeVideoBackend()
    {
        // FFME requires pointing to FFmpeg directory prior to initialization
        if (string.IsNullOrEmpty(Library.FFmpegDirectory))
        {
            Library.FFmpegDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        // MediaElement is a WPF FrameworkElement and must be instantiated on the UI dispatcher thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _mediaElement = new MediaElement
            {
                LoadedBehavior = MediaPlaybackState.Manual,
                UnloadedBehavior = MediaPlaybackState.Manual,
                Volume = _volume / 100.0
            };
            _mediaElement.MediaOpening += (s, e) =>
            {
                try
                {
                    string filters = Lyracist.Data.Services.FFmpegService.BuildAudioFilterString(
                        Treble,
                        Mid,
                        Bass,
                        0.0,
                        Pitch,
                        1.0,
                        Compressor / 100.0,
                        Limiter
                    );

                    if (!string.IsNullOrEmpty(filters))
                    {
                        e.Options.AudioFilter = filters;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to apply FFmpeg audio filters: {ex.Message}");
                }
            };
            _mediaElement.RenderingVideo += OnRenderingVideo;
        });
    }

    public async Task LoadAsync(string path)
    {
        if (_mediaElement == null || string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        await _mediaElement.Open(new Uri(path));

        // Re-apply properties
        _mediaElement.Volume = _volume / 100.0;
        _mediaElement.SpeedRatio = _speed;
    }

    public async Task PlayAsync()
    {
        if (_mediaElement != null)
        {
            await _mediaElement.Play();
        }
    }

    public async Task PauseAsync()
    {
        if (_mediaElement != null)
        {
            await _mediaElement.Pause();
        }
    }

    public async Task StopAsync()
    {
        if (_mediaElement != null)
        {
            await _mediaElement.Stop();
        }
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (_mediaElement != null)
        {
            await _mediaElement.Seek(position);
        }
    }

    private void OnRenderingVideo(object? sender, RenderingVideoEventArgs e)
    {
        if (_isDisposed) return;

        var buffer = e.Bitmap;
        IntPtr scan0 = buffer.Scan0;
        int stride = buffer.Stride;
        int height = buffer.PixelHeight;
        int width = buffer.PixelWidth;
        int size = stride * height;

        // Dispatch frame pixel copy onto UI thread dispatcher to prevent cross-threading access exceptions
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_isDisposed) return;

            try
            {
                if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
                {
                    _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                }

                _bitmap.Lock();
                unsafe
                {
                    Buffer.MemoryCopy(
                        (void*)scan0,
                        (void*)_bitmap.BackBuffer,
                        size,
                        size
                    );
                }
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                _bitmap.Unlock();

                FrameReady?.Invoke(this, new VideoFrame(_bitmap, Position));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFME custom rendering exception: {ex.Message}");
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_mediaElement != null)
        {
            _mediaElement.RenderingVideo -= OnRenderingVideo;
            // Stop and close mediaElement inside UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _mediaElement.Close().GetAwaiter().GetResult();
            });
        }

        GC.SuppressFinalize(this);
    }
}