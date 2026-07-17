// Created on Jul 17, 2026 @ 09:00:00 -> Shared rotation singer interface
namespace Lyracist.Shared
{
    public interface IRotationSinger
    {
        bool IsCurrent { get; set; }
        bool IsNext { get; set; }
        bool IsInactive { get; set; }
        bool IsPaused { get; set; }
    }
}
