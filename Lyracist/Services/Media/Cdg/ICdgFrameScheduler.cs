using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Lyracist.Services.Media.Cdg;

public interface ICdgFrameScheduler
{
    void LoadPackets(List<CdgPacket> packets);
    void Reset();
    void Update(TimeSpan audioPosition);
    void UpdateBackground(TimeSpan audioPosition, CdgDecoder cdg);
    WriteableBitmap? GetFrame();
}
