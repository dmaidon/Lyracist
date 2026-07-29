// Edited on Jul 28, 2026 @ 18:33:00 -> Add IsMusic property to IRotationSinger interface
namespace Lyracist.Shared
{
    public interface IRotationSinger
    {
        bool IsCurrent { get; set; }
        bool IsNext { get; set; }
        bool IsInactive { get; set; }
        bool IsPaused { get; set; }
        bool IsMusic { get; set; }
    }
}
