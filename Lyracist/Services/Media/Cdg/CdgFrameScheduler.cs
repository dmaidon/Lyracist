// Edited on Aug 6, 2026 @ 07:01:27 -> Lock shared scheduler state against the Reset()/UpdateBackground() race, remove dead Update() method
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Lyracist.Services.Media.Cdg;

public class CdgFrameScheduler : ICdgFrameScheduler
{
    private readonly ICDGDecoder _cdgDecoder;

    // UpdateBackground() runs on a background thread while Reset() and GetFrame() can be
    // called from the UI thread (e.g. Stop()/LoadSong() vs. the in-flight decode Task.Run
    // in MediaEngine.OnPlaybackTick) — all access to the fields below must go through this lock.
    private readonly System.Threading.Lock _syncRoot = new();
    private List<CdgPacket> _packets = [];
    private int _currentPacketIndex;
    private DateTime _lastFrameTime = DateTime.MinValue;
    private WriteableBitmap? _lastFrame;
    private readonly TimeSpan _frameInterval;
    private bool _needBitmapCopy;

    public int TargetFrameRate { get; set; } = 60;

    public CdgFrameScheduler(ICDGDecoder cdgDecoder)
    {
        _cdgDecoder = cdgDecoder;
        _frameInterval = TimeSpan.FromSeconds(1.0 / TargetFrameRate);
    }

    public void LoadPackets(List<CdgPacket> packets)
    {
        lock (_syncRoot)
        {
            _packets = [.. packets];
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _currentPacketIndex = 0;
            _lastFrameTime = DateTime.MinValue;
            _lastFrame = null;
            _needBitmapCopy = false;
            if (_cdgDecoder is CdgDecoder cdg)
            {
                cdg.ResetState();
            }
        }
    }

    public void UpdateBackground(TimeSpan audioPosition, CdgDecoder cdg)
    {
        lock (_syncRoot)
        {
            if (_packets == null || _packets.Count == 0) return;

            double targetSeconds = audioPosition.TotalSeconds;

            // Handle seeks backward by resetting and starting over
            if (_currentPacketIndex > 0 && targetSeconds < _packets[_currentPacketIndex - 1].Timestamp)
            {
                _currentPacketIndex = 0;
                _lastFrameTime = DateTime.MinValue;
                _lastFrame = null;
                _needBitmapCopy = false;
                cdg.ResetState();
            }

            while (_currentPacketIndex < _packets.Count && _packets[_currentPacketIndex].Timestamp <= targetSeconds)
            {
                CdgPacket packet = _packets[_currentPacketIndex];
                cdg.ApplyPacket(packet);
                _currentPacketIndex++;
            }

            DateTime now = DateTime.UtcNow;
            if (now - _lastFrameTime >= _frameInterval)
            {
                cdg.RenderToBuffer();
                _lastFrameTime = now;
                _needBitmapCopy = true;
            }
        }
    }

    public WriteableBitmap? GetFrame()
    {
        lock (_syncRoot)
        {
            if (_needBitmapCopy && _cdgDecoder is CdgDecoder cdg)
            {
                int width = cdg.TargetWidth;
                int height = cdg.TargetHeight;
                if (_lastFrame == null || _lastFrame.PixelWidth != width || _lastFrame.PixelHeight != height)
                {
                    _lastFrame = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null);
                }
                cdg.CopyToBitmap(_lastFrame);
                _needBitmapCopy = false;
            }
            return _lastFrame;
        }
    }
}
