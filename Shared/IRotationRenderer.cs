// Created on Aug 1, 2026 @ 12:04:00 -> Add IRotationRenderer interface
// Moved to Shared on Aug 1, 2026 -> shared between Lyracist and KSRotation
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Lyracist.Shared;

public interface IRotationRenderer
{
    /// <summary>Captures the current rotation display frame, or null if the window isn't ready.</summary>
    Task<BitmapSource?> RenderRotationAsync();
}
