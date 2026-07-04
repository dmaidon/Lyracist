namespace Lyracist.Services.Media.Cdg;

public static class CdgConstants
{
    public const int Width = 300;
    public const int Height = 216;
    
    public const int BorderLeft = 6;
    public const int BorderRight = 294;
    public const int BorderTop = 12;
    public const int BorderBottom = 204;
    
    public const int PacketSize = 24;
    public const int DataSize = 16;
    
    public const byte CommandMask = 0x3F;
    public const byte CommandCdg = 0x09;
    
    public const int PacketsPerSecond = 300;
}
