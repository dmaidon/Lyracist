using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Lyracist.Services.Media.Cdg;

public class CdgFrameScheduler : ICdgFrameScheduler
{
    private readonly ICDGDecoder _cdgDecoder;
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
        _packets = [.. packets];
    }

    public void Reset()
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

    public void Update(TimeSpan audioPosition)
    {
        if (_packets == null || _packets.Count == 0) return;

        double targetSeconds = audioPosition.TotalSeconds;

        // Handle seeks backward by resetting and starting over
        if (_currentPacketIndex > 0 && targetSeconds < _packets[_currentPacketIndex - 1].Timestamp)
        {
            Reset();
        }

        // Process subcode packet blocks sequentially up to the target timestamp
        if (_cdgDecoder is CdgDecoder cdg)
        {
            while (_currentPacketIndex < _packets.Count && _packets[_currentPacketIndex].Timestamp <= targetSeconds)
            {
                CdgPacket packet = _packets[_currentPacketIndex];
                cdg.ApplyPacket(packet);
                _currentPacketIndex++;
            }

            // Render the frame at the requested target frame rate limit
            DateTime now = DateTime.UtcNow;
            if (now - _lastFrameTime >= _frameInterval)
            {
                _lastFrame = cdg.RenderToBitmap();
                _lastFrameTime = now;
            }
        }
    }

    public void UpdateBackground(TimeSpan audioPosition, CdgDecoder cdg)
    {
        if (_packets == null || _packets.Count == 0) return;

        double targetSeconds = audioPosition.TotalSeconds;

        // Handle seeks backward by resetting and starting over
        if (_currentPacketIndex > 0 && targetSeconds < _packets[_currentPacketIndex - 1].Timestamp)
        {
            Reset();
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

    public WriteableBitmap? GetFrame()
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
