namespace Lyracist.Shared;

/// <summary>Plain projection of a System.Windows.Forms.Screen, shared by KSRotation and Lyracist.</summary>
public record MonitorInfo(int Index, string DeviceName, bool IsPrimary, int X, int Y, int Width, int Height);
