// Created on Jul 27, 2026 @ 22:37:00 -> Model class for display monitor selection
namespace KSRotation.Models
{
    public sealed record MonitorItem
    {
        public string DeviceName { get; init; } = string.Empty;
        public string FriendlyName { get; init; } = string.Empty;
    }
}
