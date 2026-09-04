// Edited on Sep 3, 2026 @ 23:48:10 -> Add HasSungInLastRound property to IRotationSinger interface
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
        bool HasSungInLastRound { get; set; }
    }
}

