namespace Lyracist.Services.Media.Cdg;

public enum CdgCommand : byte
{
    MemoryPreset = 1,
    BorderPreset = 2,
    TileBlockNormal = 6,
    ScrollPreset = 20,
    ScrollCopy = 24,
    DefineTransparentColor = 28,
    LoadColorTableLow = 30,
    LoadColorTableHigh = 31,
    TileBlockXor = 38
}
