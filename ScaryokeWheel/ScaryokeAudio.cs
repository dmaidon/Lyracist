using System;
using System.IO;

namespace ScaryokeWheel;

public static class ScaryokeAudio
{
    public static MemoryStream CreateTickStream()
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            int sampleRate = 11025;
            writer.Write("RIFF".ToCharArray());
            writer.Write(0); // Placeholder
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(sampleRate); // Sample rate
            writer.Write(sampleRate * 2); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(0); // Placeholder

            // Ticking sound: a very short click wave (~10 ms)
            int sampleCount = 120;
            for (int i = 0; i < sampleCount; i++)
            {
                double fade = (double)(sampleCount - i) / sampleCount;
                short value = (short)(Math.Sin(i * 1.8) * fade * 16000);
                writer.Write(value);
            }

            long endPos = ms.Position;
            ms.Position = 4;
            writer.Write((int)(endPos - 8));
            ms.Position = 40;
            writer.Write((int)(sampleCount * 2));
            ms.Position = endPos;
        }
        ms.Position = 0;
        return ms;
    }

    public static MemoryStream CreateEvilLaughStream()
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            int sampleRate = 22050;
            writer.Write("RIFF".ToCharArray());
            writer.Write(0); // Placeholder
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(0); // Placeholder

            // Descending "ha ha ha" laughs
            int numSyllables = 8;
            double durationPerSyllable = 0.20; // seconds
            double gapDuration = 0.04; // seconds
            int totalSamples = 0;
            var random = new Random();

            for (int s = 0; s < numSyllables; s++)
            {
                double baseFreq = 170 - (s * 12); // descending pitch
                int sylSamples = (int)(sampleRate * durationPerSyllable);
                int gapSamples = (int)(sampleRate * gapDuration);

                // Syllable burst
                for (int i = 0; i < sylSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    // Pitch envelope: slides down rapidly
                    double freq = baseFreq * (1.6 - 0.6 * (t / durationPerSyllable));
                    
                    // Raspy frequency modulation (spooky vibrato)
                    freq += 18 * Math.Sin(2 * Math.PI * 40 * t);

                    double phase = 2 * Math.PI * freq * t;
                    double wave = Math.Sin(phase);
                    
                    // Add noise component
                    double noise = (random.NextDouble() * 2.0 - 1.0) * 0.35;
                    double val = wave + noise;

                    // Envelope: fast attack, linear decay
                    double env;
                    if (t < 0.02)
                    {
                        env = t / 0.02;
                    }
                    else
                    {
                        env = 1.0 - ((t - 0.02) / (durationPerSyllable - 0.02));
                    }
                    env = Math.Clamp(env, 0, 1);

                    short sample = (short)(val * env * 22000);
                    writer.Write(sample);
                    totalSamples++;
                }

                // Gap silence
                for (int i = 0; i < gapSamples; i++)
                {
                    writer.Write((short)0);
                    totalSamples++;
                }
            }

            long endPos = ms.Position;
            ms.Position = 4;
            writer.Write((int)(endPos - 8));
            ms.Position = 40;
            writer.Write((int)(totalSamples * 2));
            ms.Position = endPos;
        }
        ms.Position = 0;
        return ms;
    }
}
