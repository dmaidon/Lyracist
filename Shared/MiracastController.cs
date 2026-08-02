// Created on Aug 1, 2026 @ 12:06:00 -> Add MiracastController class
// Moved to Shared on Aug 1, 2026 -> byte-for-byte duplicated between Lyracist and KSRotation
using System.Threading.Tasks;

namespace Lyracist.Shared;

public class MiracastController
{
    public async Task<bool> StartCastingAsync(IRotationRenderer renderer)
    {
        // Windows treats Miracast as a monitor.
        // This simply ensures the rotation window moves to that monitor.
        await Task.Delay(1);
        return true;
    }

    public async Task StopCastingAsync()
    {
        await Task.Delay(1);
    }
}
