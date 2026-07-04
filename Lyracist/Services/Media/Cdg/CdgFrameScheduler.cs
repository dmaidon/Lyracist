using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Lyracist.Services.Media.Cdg;

public class CdgFrameScheduler : ICdgFrameScheduler
{
    private readonly ICDGDecoder _cdgDecoder;
    private List<CdgPacket> _packets = new();
    private int _currentPacketIndex;
    private DateTime _lastFrameTime = DateTime.MinValue;
    private WriteableBitmap? _lastFrame;
    private readonly TimeSpan _frameInterval;

    public int TargetFrameRate { get; set; } = 60;

    public CdgFrameScheduler(ICDGDecoder cdgDecoder)
    {
        _cdgDecoder = cdgDecoder;
        _frameInterval = TimeSpan.FromSeconds(1.0 / TargetFrameRate);
    }

    public void LoadPackets(List<CdgPacket> packets)
    {
        _packets = packets;
        if (_cdgDecoder is CdgDecoder cdg)
        {
            cdg.Packets.Clear();
            cdg.Packets.AddRange(packets);
        }
    }

    public void Reset()
    {
        _currentPacketIndex = 0;
        _lastFrameTime = DateTime.MinValue;
        _lastFrame = null;
        if (_cdgDecoder is CdgDecoder cdg)
        {
            cdg.ResetState();
        }
    }

    public void Update(TimeSpan audioPosition)
    {
        if (_packets == null || _packets.Count == 0) return;

        // 1. Calculate the target packet based on playback timing (300 packets per second)
        int targetIndex = (int)(audioPosition.TotalSeconds * CdgConstants.PacketsPerSecond);
        if (targetIndex < 0) targetIndex = 0;
        if (targetIndex > _packets.Count) targetIndex = _packets.Count;

        // 2. Handle seeks backward by resetting and starting over
        if (targetIndex < _currentPacketIndex)
        {
            Reset();
        }

        // 3. Process subcode packet blocks sequentially up to the target index
        if (_cdgDecoder is CdgDecoder cdg)
        {
            while (_currentPacketIndex < targetIndex)
            {
                CdgPacket packet = _packets[_currentPacketIndex];
                cdg.ApplyPacket(packet);
                _currentPacketIndex++;
            }

            // 4. Render the frame at the requested target frame rate limit
            DateTime now = DateTime.UtcNow;
            if (now - _lastFrameTime >= _frameInterval)
            {
                _lastFrame = cdg.RenderToBitmap();
                _lastFrameTime = now;
            }
        }
    }

    public WriteableBitmap? GetFrame()
    {
        return _lastFrame;
    }
}
