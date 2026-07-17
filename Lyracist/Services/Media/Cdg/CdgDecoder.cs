// Edited on Jul 17, 2026 @ 09:00:00 -> Off-thread CDG buffer decoding
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace Lyracist.Services.Media.Cdg;

public class CdgDecoder : ICDGDecoder
{
    private readonly List<CdgPacket> _packets = [];
    private readonly CdgState _state = new();
    private int _currentPacketIndex;

    private WriteableBitmap? _bitmap;
    private byte[]? _bgraBuffer;
    public readonly System.Threading.Lock RenderLock = new();

    public int TargetWidth { get; set; } = CdgConstants.Width;
    public int TargetHeight { get; set; } = CdgConstants.Height;

    public List<CdgPacket> Packets => _packets;

    public async Task LoadAsync(string cdgPath)
    {
        _packets.Clear();
        _currentPacketIndex = 0;
        _state.Reset();

        if (string.IsNullOrEmpty(cdgPath) || !System.IO.File.Exists(cdgPath))
        {
            return;
        }

        try
        {
            using var fileStream = System.IO.File.OpenRead(cdgPath);
            using var memoryStream = App.MemoryStreamManager.GetStream();
            await fileStream.CopyToAsync(memoryStream);

            byte[] fileBytes = memoryStream.GetBuffer();
            int length = (int)memoryStream.Length;
            int packetCount = length / CdgConstants.PacketSize;

            for (int i = 0; i < packetCount; i++)
            {
                int offset = i * CdgConstants.PacketSize;

                // CDG specification: command and instruction bytes use only lower 6 bits (mask 0x3F)
                byte command = (byte)(fileBytes[offset] & CdgConstants.CommandMask);
                byte instruction = (byte)(fileBytes[offset + 1] & CdgConstants.CommandMask);

                // Payload starts after the 4-byte header (Command, Instruction, 2 bytes Parity)
                ReadOnlySpan<byte> data = new(fileBytes, offset + 4, CdgConstants.DataSize);

                // Only process CDG graphic command (0x09)
                if (command == CdgConstants.CommandCdg)
                {
                    var packet = new CdgPacket(command, instruction, data)
                    {
                        Timestamp = i * (1.0 / CdgConstants.PacketsPerSecond)
                    };
                    _packets.Add(packet);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading CDG file: {ex.Message}");
        }
    }

    public void ResetState()
    {
        _currentPacketIndex = 0;
        _state.Reset();
    }

    public WriteableBitmap GetNextFrame(TimeSpan audioPosition)
    {
        // 1. Calculate the target packet based on playback timing (300 packets per second)
        int targetIndex = (int)(audioPosition.TotalSeconds * CdgConstants.PacketsPerSecond);
        if (targetIndex < 0) targetIndex = 0;
        if (targetIndex > _packets.Count) targetIndex = _packets.Count;

        // 2. If the user seeked backwards, reset the state and restart parsing from packet 0
        if (targetIndex < _currentPacketIndex)
        {
            ResetState();
        }

        // 3. Process all packets sequentially up to the target timing index
        while (_currentPacketIndex < targetIndex)
        {
            CdgPacket packet = _packets[_currentPacketIndex];
            ApplyPacket(packet);
            _currentPacketIndex++;
        }

        // 4. Render and scale the current canvas state to a WriteableBitmap
        return RenderToBitmap();
    }

    public void ApplyPacket(CdgPacket packet)
    {
        CdgCommand cmd = (CdgCommand)packet.Instruction;

        switch (cmd)
        {
            case CdgCommand.MemoryPreset:
                {
                    byte color = (byte)(packet.Data[0] & 0x0F);
                    _state.Clear(color);
                }
                break;

            case CdgCommand.BorderPreset:
                {
                    byte color = (byte)(packet.Data[0] & 0x0F);
                    _state.ClearBorder(color);
                }
                break;

            case CdgCommand.TileBlockNormal:
                new CdgTile(packet.Data).Apply(_state, isXor: false);
                break;

            case CdgCommand.TileBlockXor:
                new CdgTile(packet.Data).Apply(_state, isXor: true);
                break;

            case CdgCommand.ScrollPreset:
                {
                    byte color = (byte)(packet.Data[0] & 0x0F);
                    int hScroll = packet.Data[1];
                    int vScroll = packet.Data[2];

                    int hScrollCmd = (hScroll & 0x30) >> 4;
                    int vScrollCmd = (vScroll & 0x30) >> 4;

                    int hShift = (hScrollCmd == 1) ? 6 : (hScrollCmd == 2 ? -6 : 0);
                    int vShift = (vScrollCmd == 1) ? 12 : (vScrollCmd == 2 ? -12 : 0);

                    _state.Scroll(color, hShift, vShift, isCopy: false);
                }
                break;

            case CdgCommand.ScrollCopy:
                {
                    int hScroll = packet.Data[1];
                    int vScroll = packet.Data[2];

                    int hScrollCmd = (hScroll & 0x30) >> 4;
                    int vScrollCmd = (vScroll & 0x30) >> 4;

                    int hShift = (hScrollCmd == 1) ? 6 : (hScrollCmd == 2 ? -6 : 0);
                    int vShift = (vScrollCmd == 1) ? 12 : (vScrollCmd == 2 ? -12 : 0);

                    _state.Scroll(0, hShift, vShift, isCopy: true);
                }
                break;

            case CdgCommand.DefineTransparentColor:
                _state.Palette.TransparentColorIndex = packet.Data[0] & 0x0F;
                break;

            case CdgCommand.LoadColorTableLow:
                _state.Palette.LoadColors(packet.Data, isHigh: false);
                break;

            case CdgCommand.LoadColorTableHigh:
                _state.Palette.LoadColors(packet.Data, isHigh: true);
                break;
        }
    }

    public void RenderToBuffer()
    {
        lock (RenderLock)
        {
            int width = TargetWidth;
            int height = TargetHeight;
            int size = width * height * 4;

            if (_bgraBuffer == null || _bgraBuffer.Length != size)
            {
                _bgraBuffer = new byte[size];
            }

            bool noScale = width == CdgConstants.Width && height == CdgConstants.Height;
            bool chromaEnabled = Lyracist.Core.Helpers.AppSettings.IsCdgChromaKeyEnabled;
            byte bgIndex = chromaEnabled ? _state.Pixels[0, 0] : (byte)0;
            int transIndex = _state.Palette.TransparentColorIndex;

            if (noScale)
            {
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * width * 4;
                    for (int x = 0; x < width; x++)
                    {
                        byte colorIndex = _state.Pixels[x, y];
                        
                        bool isTransparent = colorIndex == transIndex || (chromaEnabled && colorIndex == bgIndex);
                        byte alpha = isTransparent ? (byte)0 : (byte)255;
                        Color color = _state.Palette.Colors[colorIndex];

                        byte r = isTransparent ? (byte)0 : color.R;
                        byte g = isTransparent ? (byte)0 : color.G;
                        byte b = isTransparent ? (byte)0 : color.B;

                        int colOffset = rowOffset + (x * 4);
                        _bgraBuffer[colOffset] = b;
                        _bgraBuffer[colOffset + 1] = g;
                        _bgraBuffer[colOffset + 2] = r;
                        _bgraBuffer[colOffset + 3] = alpha;
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * width * 4;
                    int srcY = (y * CdgConstants.Height) / height;
                    if (srcY >= CdgConstants.Height) srcY = CdgConstants.Height - 1;

                    for (int x = 0; x < width; x++)
                    {
                        int srcX = (x * CdgConstants.Width) / width;
                        if (srcX >= CdgConstants.Width) srcX = CdgConstants.Width - 1;

                        byte colorIndex = _state.Pixels[srcX, srcY];
                        
                        bool isTransparent = colorIndex == transIndex || (chromaEnabled && colorIndex == bgIndex);
                        byte alpha = isTransparent ? (byte)0 : (byte)255;
                        Color color = _state.Palette.Colors[colorIndex];

                        byte r = isTransparent ? (byte)0 : color.R;
                        byte g = isTransparent ? (byte)0 : color.G;
                        byte b = isTransparent ? (byte)0 : color.B;

                        int colOffset = rowOffset + (x * 4);
                        _bgraBuffer[colOffset] = b;
                        _bgraBuffer[colOffset + 1] = g;
                        _bgraBuffer[colOffset + 2] = r;
                        _bgraBuffer[colOffset + 3] = alpha;
                    }
                }
            }
        }
    }

    public void CopyToBitmap(WriteableBitmap bitmap)
    {
        if (bitmap == null) return;

        lock (RenderLock)
        {
            if (_bgraBuffer == null) return;

            bitmap.Lock();
            try
            {
                unsafe
                {
                    byte* backBuffer = (byte*)bitmap.BackBuffer;
                    int width = bitmap.PixelWidth;
                    int height = bitmap.PixelHeight;
                    int size = width * height * 4;

                    fixed (byte* src = _bgraBuffer)
                    {
                        Buffer.MemoryCopy(src, backBuffer, size, size);
                    }
                }
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
            finally
            {
                bitmap.Unlock();
            }
        }
    }

    public WriteableBitmap RenderToBitmap()
    {
        int width = TargetWidth;
        int height = TargetHeight;

        if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        }

        RenderToBuffer();
        CopyToBitmap(_bitmap);

        return _bitmap;
    }
}
