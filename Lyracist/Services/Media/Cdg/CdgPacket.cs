using System;

namespace Lyracist.Services.Media.Cdg;

public class CdgPacket
{
    public byte Command { get; set; }
    public byte Instruction { get; set; }
    public byte[] Data { get; } = new byte[CdgConstants.DataSize];
    public double Timestamp { get; set; }

    public CdgPacket(byte command, byte instruction, ReadOnlySpan<byte> data)
    {
        Command = command;
        Instruction = instruction;
        data.CopyTo(Data);
    }
}
