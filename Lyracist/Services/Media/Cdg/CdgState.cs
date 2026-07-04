using System;

namespace Lyracist.Services.Media.Cdg;

public class CdgState
{
    public byte[,] Pixels { get; } = new byte[CdgConstants.Width, CdgConstants.Height];
    public CdgPalette Palette { get; } = new CdgPalette();

    public CdgState()
    {
        Reset();
    }

    public void Reset()
    {
        Palette.Reset();
        Clear(0);
    }

    public void Clear(byte colorIndex)
    {
        for (int y = 0; y < CdgConstants.Height; y++)
        {
            for (int x = 0; x < CdgConstants.Width; x++)
            {
                Pixels[x, y] = colorIndex;
            }
        }
    }

    public void ClearBorder(byte colorIndex)
    {
        for (int y = 0; y < CdgConstants.Height; y++)
        {
            for (int x = 0; x < CdgConstants.Width; x++)
            {
                // Border checks
                if (x < CdgConstants.BorderLeft || x >= CdgConstants.BorderRight ||
                    y < CdgConstants.BorderTop || y >= CdgConstants.BorderBottom)
                {
                    Pixels[x, y] = colorIndex;
                }
            }
        }
    }

    public void Scroll(byte colorIndex, int hShift, int vShift, bool isCopy)
    {
        byte[,] temp = new byte[CdgConstants.Width, CdgConstants.Height];

        for (int y = 0; y < CdgConstants.Height; y++)
        {
            for (int x = 0; x < CdgConstants.Width; x++)
            {
                int srcX = x - hShift;
                int srcY = y - vShift;

                if (srcX >= 0 && srcX < CdgConstants.Width && srcY >= 0 && srcY < CdgConstants.Height)
                {
                    temp[x, y] = Pixels[srcX, srcY];
                }
                else
                {
                    if (isCopy)
                    {
                        // Wrap around
                        int wrapX = (srcX % CdgConstants.Width + CdgConstants.Width) % CdgConstants.Width;
                        int wrapY = (srcY % CdgConstants.Height + CdgConstants.Height) % CdgConstants.Height;
                        temp[x, y] = Pixels[wrapX, wrapY];
                    }
                    else
                    {
                        // Preset color
                        temp[x, y] = colorIndex;
                    }
                }
            }
        }

        Array.Copy(temp, Pixels, temp.Length);
    }
}
