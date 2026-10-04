// Created on Oct 3, 2026 @ 14:46:00 -> Shared PCM/float to mono converter for spectrum capture (16-bit PCM, 24-bit PCM, 32-bit float)
using System;
using NAudio.Wave;

namespace Lyracist.Shared;

public static class MonoSampleConverter
{
    /// <summary>Downmixes interleaved capture data to mono floats in <paramref name="mono"/> (grown as needed).
    /// Returns the number of mono samples written, or 0 for an unsupported format.</summary>
    public static int ToMono(WaveFormat format, byte[] buffer, int bytesRecorded, ref float[] mono)
    {
        int channels = Math.Max(1, format.Channels);
        int bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample <= 0) return 0;

        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            (format is WaveFormatExtensible ext && ext.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);
        if (!(isFloat && bytesPerSample == 4) && !(!isFloat && (bytesPerSample == 2 || bytesPerSample == 3))) return 0;

        int frames = bytesRecorded / (bytesPerSample * channels);
        if (mono.Length < frames) mono = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;
            for (int ch = 0; ch < channels; ch++)
            {
                int offset = (i * channels + ch) * bytesPerSample;
                sum += bytesPerSample switch
                {
                    4 => BitConverter.ToSingle(buffer, offset),
                    3 => ((buffer[offset] << 8 | buffer[offset + 1] << 16 | buffer[offset + 2] << 24) >> 8) / 8388608f,
                    _ => BitConverter.ToInt16(buffer, offset) / 32768f,
                };
            }
            mono[i] = sum / channels;
        }
        return frames;
    }
}
