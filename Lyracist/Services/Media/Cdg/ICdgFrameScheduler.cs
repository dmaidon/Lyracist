// Edited on Aug 6, 2026 @ 07:01:27 -> Remove dead, UI-thread-unsafe Update() method (UpdateBackground() is the only caller path)
using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Lyracist.Services.Media.Cdg;

public interface ICdgFrameScheduler
{
    void LoadPackets(List<CdgPacket> packets);
    void Reset();
    void UpdateBackground(TimeSpan audioPosition, CdgDecoder cdg);
    WriteableBitmap? GetFrame();
}
