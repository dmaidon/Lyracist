using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;

namespace Lyracist.Media.Video;

public class LibVlcVideoBackend : IVideoBackend, IDisposable
{
    private readonly LibVLC? _libVLC;
    private readonly LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
    private WriteableBitmap? _bitmap;
    private IntPtr _pixelBuffer = IntPtr.Zero;
    private uint _width;
    private uint _height;
    private uint _pitch;
    private uint _lines;
    private bool _isDisposed;

    // Guards _pixelBuffer/_pitch/_lines/_width/_height against concurrent access:
    // VideoFormatCallback frees and reallocates the buffer on a stream-setup thread
    // while LockCallback/DisplayCallback read/write it on the decode thread.
    // Without this, a mid-stream format change (e.g. loading new media) can free
    // the buffer while a decode is still writing to it (use-after-free).
    private readonly Lock _bufferLock = new();
    private double _volume = 100.0;
    private double _speed = 1.0;
    private int _pitchShift = 0;

    private static readonly uint[] BassBands = [0, 1, 2];
    private static readonly uint[] MidBands = [3, 4, 5, 6];
    private static readonly uint[] TrebleBands = [7, 8, 9];

    private readonly Equalizer _equalizer = new();
    private double _treble = 0.0;
    private double _mid = 0.0;
    private double _bass = 0.0;
    private double _compressor = 0.0;
    private double _limiter = 0.0;
    private bool _enableKillVocal = false;

    public event EventHandler<VideoFrame>? FrameReady;

    public TimeSpan Position => _mediaPlayer != null ? TimeSpan.FromMilliseconds(_mediaPlayer.Time) : TimeSpan.Zero;
    public bool IsPlaying => _mediaPlayer != null && _mediaPlayer.IsPlaying;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 100.0);
            _mediaPlayer?.Volume = (int)_volume;
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = Math.Clamp(value, 0.5, 2.0);
            _mediaPlayer?.SetRate((float)_speed);
        }
    }

    public int Pitch
    {
        get => _pitchShift;
        set => _pitchShift = Math.Clamp(value, -6, 6); // Note: Native pitch transposition simulation
    }

    private void UpdateEqualizer()
    {
        if (_equalizer == null) return;

        if (_enableKillVocal)
        {
            // Cut vocal bands completely (-20dB represents complete suppression in LibVLC)
            foreach (uint band in MidBands) _equalizer.SetAmp(-20.0f, band);
            // Boost Bass and Treble slightly to emphasize accompaniment tracks
            foreach (uint band in BassBands) _equalizer.SetAmp((float)Math.Clamp(_bass + 4.0, -20.0, 20.0), band);
            foreach (uint band in TrebleBands) _equalizer.SetAmp((float)Math.Clamp(_treble + 2.0, -20.0, 20.0), band);
        }
        else
        {
            foreach (uint band in BassBands) _equalizer.SetAmp((float)_bass, band);
            foreach (uint band in MidBands) _equalizer.SetAmp((float)_mid, band);
            foreach (uint band in TrebleBands) _equalizer.SetAmp((float)_treble, band);
        }

        _mediaPlayer?.SetEqualizer(_equalizer);
    }

    public bool EnableKillVocal
    {
        get => _enableKillVocal;
        set
        {
            if (_enableKillVocal != value)
            {
                _enableKillVocal = value;
                UpdateEqualizer();
            }
        }
    }

    public double Treble
    {
        get => _treble;
        set
        {
            _treble = Math.Clamp(value, -20.0, 20.0);
            UpdateEqualizer();
        }
    }

    public double Mid
    {
        get => _mid;
        set
        {
            _mid = Math.Clamp(value, -20.0, 20.0);
            UpdateEqualizer();
        }
    }

    public double Bass
    {
        get => _bass;
        set
        {
            _bass = Math.Clamp(value, -20.0, 20.0);
            UpdateEqualizer();
        }
    }

    public double Compressor
    {
        get => _compressor;
        set => _compressor = value;
    }

    public double Limiter
    {
        get => _limiter;
        set => _limiter = value;
    }

    public LibVlcVideoBackend()
    {
        Lyracist.Core.Helpers.AppLogger.InitializeLibVlc();

        _libVLC = new LibVLC();
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

        // Bind raw video decoding pipeline callbacks
        _mediaPlayer.SetVideoFormatCallbacks(VideoFormatCallback, VideoCleanupCallback);
        _mediaPlayer.SetVideoCallbacks(LockCallback, UnlockCallback, DisplayCallback);

        // Apply default volume and equalizer
        _mediaPlayer.Volume = (int)_volume;
        UpdateEqualizer();
    }

    public Task LoadAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return Task.CompletedTask;
        }

        if (_mediaPlayer != null && _libVLC != null)
        {
            var media = new LibVLCSharp.Shared.Media(_libVLC, new Uri(path));
            if (_pitchShift != 0)
            {
                media.AddOption($":audio-filter=pitch");
                media.AddOption($":pitch-shift={_pitchShift}");
            }
            _mediaPlayer.Media = media;

            // Re-apply rate, volume, and equalizer settings
            _mediaPlayer.Volume = (int)_volume;
            _mediaPlayer.SetRate((float)_speed);
            UpdateEqualizer();
        }

        return Task.CompletedTask;
    }

    public Task PlayAsync()
    {
        _mediaPlayer?.Play();
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _mediaPlayer?.Pause();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _mediaPlayer?.Stop();
        return Task.CompletedTask;
    }

    public Task SeekAsync(TimeSpan position)
    {
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Time = (long)position.TotalMilliseconds;
        }
        return Task.CompletedTask;
    }

    private uint VideoFormatCallback(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        // Force RV32 pixel format (4 bytes per pixel: BGRA)
        byte[] chromaBytes = Encoding.ASCII.GetBytes("RV32");
        Marshal.Copy(chromaBytes, 0, chroma, Math.Min(chromaBytes.Length, 4));

        pitches = width * 4;
        lines = height;

        lock (_bufferLock)
        {
            _width = width;
            _height = height;
            _pitch = pitches;
            _lines = lines;

            int bufferSize = (int)(_pitch * _lines);

            // Deallocate old unmanaged pixel buffer
            if (_pixelBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pixelBuffer);
            }

            // Allocate the unmanaged buffer for frame pixels
            _pixelBuffer = Marshal.AllocHGlobal(bufferSize);
        }

        // Allocate WriteableBitmap on the main UI dispatcher thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _bitmap = new WriteableBitmap((int)_width, (int)_height, 96, 96, PixelFormats.Bgra32, null);
        });

        return 1; // Success
    }

    private void VideoCleanupCallback(ref IntPtr opaque)
    {
        // Cleanup callbacks
    }

    private IntPtr LockCallback(IntPtr opaque, IntPtr planes)
    {
        // Narrowly scoped: only protects the pointer hand-off against a concurrent
        // buffer swap in VideoFormatCallback. Deliberately does NOT span into
        // UnlockCallback — Lock/Unlock are separate native callback invocations with
        // no try/finally guarantee between them, so holding a Monitor across that gap
        // risks a permanent deadlock if Unlock is ever skipped (e.g. Pause interrupting
        // mid-frame). This was tried and caused exactly that hang.
        lock (_bufferLock)
        {
            Marshal.WriteIntPtr(planes, 0, _pixelBuffer);
        }
        return IntPtr.Zero;
    }

    private void UnlockCallback(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        // Frame unlocked
    }

    private void DisplayCallback(IntPtr opaque, IntPtr picture)
    {
        if (_bitmap == null || _pixelBuffer == IntPtr.Zero || _isDisposed) return;

        var app = System.Windows.Application.Current;
        if (app == null) return;

        var dispatcher = app.Dispatcher;
        if (dispatcher == null) return;

        // Snapshot the buffer pointer/dimensions under a narrowly-scoped lock, then
        // release before dispatching to the UI thread. Never hold _bufferLock across
        // Dispatcher.Invoke — if any other code path (e.g. Dispose) ever needs this
        // lock from the UI thread, holding it across a blocking cross-thread Invoke
        // is a deadlock waiting to happen.
        IntPtr pixelBuffer;
        uint pitch, lines, width, height;
        lock (_bufferLock)
        {
            pixelBuffer = _pixelBuffer;
            pitch = _pitch;
            lines = _lines;
            width = _width;
            height = _height;
        }

        if (pixelBuffer == IntPtr.Zero) return;

        try
        {
            // Perform fast memory copying inside UI thread dispatcher asynchronously to prevent cross-threading deadlocks
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isDisposed || _bitmap == null) return;

                try
                {
                    _bitmap.Lock();
                    int size = (int)(pitch * lines);
                    unsafe
                    {
                        Buffer.MemoryCopy(
                            (void*)pixelBuffer,
                            (void*)_bitmap.BackBuffer,
                            size,
                            size
                        );
                    }
                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, (int)width, (int)height));
                    _bitmap.Unlock();

                    FrameReady?.Invoke(this, new VideoFrame(_bitmap, Position));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LibVLC custom rendering exception: {ex.Message}");
                }
            }));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LibVLC DisplayCallback dispatcher invocation exception: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Stop/dispose the player first so no further Lock/Unlock/Format callbacks
        // can fire before we free the buffer below.
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _equalizer.Dispose();
        _libVLC?.Dispose();

        lock (_bufferLock)
        {
            if (_pixelBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pixelBuffer);
                _pixelBuffer = IntPtr.Zero;
            }
        }

        GC.SuppressFinalize(this);
    }
}