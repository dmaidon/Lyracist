// Edited on Aug 9, 2026 @ 10:21:00 -> Add IsStandard property to identify 4 default standard event banners
using System.Linq;

namespace Lyracist.Shared;

public class SpecialEventConfig
{
    public string EventName { get; set; } = string.Empty;
    public string BannerFileName { get; set; } = string.Empty;

    public bool IsStandard => DjBannerFileManager.StandardEventNames.Any(s => s.Equals(EventName, System.StringComparison.OrdinalIgnoreCase));
}
