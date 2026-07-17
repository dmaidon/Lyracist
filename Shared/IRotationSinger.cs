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
