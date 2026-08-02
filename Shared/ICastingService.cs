// Created on Aug 1, 2026 @ 12:03:00 -> Add ICastingService interface
// Moved to Shared on Aug 1, 2026 @ 12:04:00 -> byte-for-byte duplicated between Lyracist and KSRotation
using System.Threading.Tasks;

namespace Lyracist.Shared;

public interface ICastingService
{
    Task<bool> CastRotationAsync(DisplayTarget target, ChromecastDevice? device = null);
    Task StopCastingAsync();
    bool IsCasting { get; }
    DisplayTarget CurrentTarget { get; }
    ChromecastDevice? SelectedDevice { get; set; }
}
