// Edited on Aug 18, 2026 @ 13:24:00 -> Add IsRotationStart property to IRotationSinger interface
namespace Lyracist.Shared
{
    public interface IRotationSinger
    {
        bool IsCurrent { get; set; }
        bool IsNext { get; set; }
        bool IsInactive { get; set; }
        bool IsPaused { get; set; }
        bool IsMusic { get; set; }
        bool IsRotationStart { get; set; }
    }
}

