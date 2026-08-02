// Created on Aug 1, 2026 @ 12:01:00 -> Add DisplayTarget enum for casting targets
// Moved to Shared on Aug 1, 2026 -> Lyracist and KSRotation had byte-for-byte identical copies
namespace Lyracist.Shared;

public enum DisplayTarget
{
    Monitor,
    Miracast,
    Chromecast,
    BrowserCast,
    AirPlay,
    WirelessHDMI
}
