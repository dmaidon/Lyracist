using System;

namespace Lyracist.Services.Media.Cdg;

public struct CdgTile
{
    public byte Color0 { get; }
    public byte Color1 { get; }
    public byte Row { get; }
    public byte Col { get; }
    public byte[] Data { get; }

    public CdgTile(ReadOnlySpan<byte> data)
    {
        Color0 = (byte)(data[0] & 0x0F);
        Color1 = (byte)(data[1] & 0x0F);
        Row = (byte)(data[2] & 0x1F); // 0-17
        Col = (byte)(data[3] & 0x3F); // 0-49

        Data = new byte[12];
        data.Slice(4, 12).CopyTo(Data);
    }

    public void Apply(CdgState state, bool isXor)
    {
        // Safe check for grid sizes: 50 columns x 18 rows
        if (Row >= 18 || Col >= 50) return;

        int xStart = Col * 6;
        int yStart = Row * 12;

        for (int dy = 0; dy < 12; dy++)
        {
            byte rowByte = (byte)(Data[dy] & 0x3F); // Lower 6 bits represent pixels

            for (int dx = 0; dx < 6; dx++)
            {
                int bit = (rowByte >> (5 - dx)) & 1;
                byte colorIndex = (bit == 1) ? Color1 : Color0;

                int px = xStart + dx;
                int py = yStart + dy;

                if (px < CdgConstants.Width && py < CdgConstants.Height)
                {
                    if (isXor)
                    {
                        state.Pixels[px, py] ^= colorIndex;
                    }
                    else
                    {
                        state.Pixels[px, py] = colorIndex;
                    }
                }
            }
        }
    }
}
