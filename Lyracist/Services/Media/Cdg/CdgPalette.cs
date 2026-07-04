using System;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Lyracist.Services.Media.Cdg;

public class CdgPalette
{
    public Color[] Colors { get; } = new Color[16];
    public int TransparentColorIndex { get; set; } = -1;

    public CdgPalette()
    {
        Reset();
    }

    public void Reset()
    {
        TransparentColorIndex = -1;
        // Default to all black
        for (int i = 0; i < Colors.Length; i++)
        {
            Colors[i] = Color.FromRgb(0, 0, 0);
        }
    }

    public void LoadColors(ReadOnlySpan<byte> data, bool isHigh)
    {
        for (int i = 0; i < 8; i++)
        {
            int byte1 = data[i * 2] & 0x3F;
            int byte2 = data[i * 2 + 1] & 0x3F;
            
            // Extract 4-bit channels
            int r = (byte1 >> 2) & 0x0F;
            int g = ((byte1 & 0x03) << 2) | ((byte2 >> 4) & 0x03);
            int b = byte2 & 0x0F;
            
            // Map 4-bit (0-15) to 8-bit (0-255)
            byte r8 = (byte)(r * 17);
            byte g8 = (byte)(g * 17);
            byte b8 = (byte)(b * 17);
            
            int index = isHigh ? (8 + i) : i;
            Colors[index] = Color.FromRgb(r8, g8, b8);
        }
    }
}
