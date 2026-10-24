// Edited on Oct 24, 2026 @ 14:00:00 -> Pitched ticks, synthesized spin/result sounds, volume scaling, shared WAV writer
using System;
using System.IO;

namespace ScaryokeWheel;

public static class ScaryokeAudio
{
    private const short Silence = 0;

    /// <summary>Wraps 16-bit mono PCM samples in a WAV container.</summary>
    private static MemoryStream BuildWav(short[] samples, int sampleRate)
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + (samples.Length * 2));
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);
            foreach (short sample in samples)
            {
                writer.Write(sample);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static short Scale(double value, double volume) =>
        (short)Math.Clamp(value * Math.Clamp(volume, 0, 1), short.MinValue, short.MaxValue);

    /// <summary>
    /// A very short click. <paramref name="pitch"/> scales its frequency (1 = original), so slower
    /// ticks near the end of a spin can sound lower.
    /// </summary>
    public static MemoryStream CreateTickStream(double pitch = 1.0, double volume = 1.0)
    {
        const int sampleRate = 11025;
        const int sampleCount = 120; // ~10 ms
        var samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            double fade = (double)(sampleCount - i) / sampleCount;
            samples[i] = Scale(Math.Sin(i * 1.8 * pitch) * fade * 16000, volume);
        }
        return BuildWav(samples, sampleRate);
    }

    /// <summary>A rising "whoosh" played as the wheel starts to turn.</summary>
    public static MemoryStream CreateSpinStartStream(double volume = 1.0)
    {
        const int sampleRate = 22050;
        const double duration = 0.45;
        int count = (int)(sampleRate * duration);
        var random = new Random(7);
        var samples = new short[count];
        double phase = 0;

        for (int i = 0; i < count; i++)
        {
            double t = (double)i / count;
            double freq = 180 + (900 * t * t); // rising sweep
            phase += 2 * Math.PI * freq / sampleRate;
            double tone = Math.Sin(phase) * 0.5;
            double noise = ((random.NextDouble() * 2.0) - 1.0) * 0.35 * t;
            double env = Math.Min(t / 0.1, 1.0) * (1.0 - (t * 0.5));
            samples[i] = Scale((tone + noise) * env * 14000, volume);
        }
        return BuildWav(samples, sampleRate);
    }

    /// <summary>A two-note chime played when the wheel stops on a regular category.</summary>
    public static MemoryStream CreateResultStream(double volume = 1.0)
    {
        const int sampleRate = 22050;
        double[] notes = [523.25, 783.99]; // C5 then G5
        const double noteDuration = 0.28;
        int noteSamples = (int)(sampleRate * noteDuration);
        var samples = new short[noteSamples * notes.Length];

        for (int n = 0; n < notes.Length; n++)
        {
            for (int i = 0; i < noteSamples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Exp(-t * 7.0) * Math.Min(t / 0.01, 1.0);
                double wave = Math.Sin(2 * Math.PI * notes[n] * t) + (0.3 * Math.Sin(2 * Math.PI * notes[n] * 2 * t));
                samples[(n * noteSamples) + i] = Scale(wave * env * 12000, volume);
            }
        }
        return BuildWav(samples, sampleRate);
    }

    public static MemoryStream CreateEvilLaughStream(double volume = 1.0)
    {
        const int sampleRate = 22050;

        // Descending "ha ha ha" laughs
        const int numSyllables = 8;
        const double durationPerSyllable = 0.20; // seconds
        const double gapDuration = 0.04; // seconds
        const int sylSamples = (int)(sampleRate * durationPerSyllable);
        const int gapSamples = (int)(sampleRate * gapDuration);
        var samples = new short[numSyllables * (sylSamples + gapSamples)];
        var random = new Random();
        int pos = 0;

        for (int s = 0; s < numSyllables; s++)
        {
            double baseFreq = 170 - (s * 12); // descending pitch

            // Syllable burst
            for (int i = 0; i < sylSamples; i++)
            {
                double t = (double)i / sampleRate;
                // Pitch envelope: slides down rapidly
                double freq = baseFreq * (1.6 - (0.6 * (t / durationPerSyllable)));

                // Raspy frequency modulation (spooky vibrato)
                freq += 18 * Math.Sin(2 * Math.PI * 40 * t);

                double phase = 2 * Math.PI * freq * t;
                double wave = Math.Sin(phase);

                // Add noise component
                double noise = ((random.NextDouble() * 2.0) - 1.0) * 0.35;
                double val = wave + noise;

                // Envelope: fast attack, linear decay
                double env = t < 0.02 ? t / 0.02 : 1.0 - ((t - 0.02) / (durationPerSyllable - 0.02));
                env = Math.Clamp(env, 0, 1);

                samples[pos++] = Scale(val * env * 22000, volume);
            }

            // Gap silence
            for (int i = 0; i < gapSamples; i++)
            {
                samples[pos++] = Silence;
            }
        }
        return BuildWav(samples, sampleRate);
    }
}
