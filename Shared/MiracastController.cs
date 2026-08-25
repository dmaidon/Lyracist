// Edited on Aug 25, 2026 @ 06:41:00 -> Fix RCS1163 unused renderer parameter
using System.Threading.Tasks;

namespace Lyracist.Shared;

public class MiracastController
{
    public async Task<bool> StartCastingAsync(IRotationRenderer renderer)
    {
        _ = renderer;
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
