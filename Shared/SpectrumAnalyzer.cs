// Created on Oct 3, 2026 @ 14:40:00 -> Shared FFT spectrum analyzer (dB-scaled bands with auto-gain) used by Lyracist loopback capture and KSRotation line-in capture
using System;

namespace Lyracist.Shared;

/// <summary>
/// Turns a stream of mono audio samples into smoothed, 0..1 logarithmic frequency bands for a
/// bar-graph visualizer. Capture-agnostic: callers feed it samples (WASAPI loopback in Lyracist,
/// a line-in/mic in KSRotation) and poll <see cref="GetBands"/> once per rendered frame.
/// </summary>
public sealed class SpectrumAnalyzer
{
    private const int FftSize = 1024;
    private const int BufferSize = 4096;
    private const float DbFloor = 60f;

    private readonly float[] _sampleBuffer = new float[BufferSize];
    private int _writePos;
    private readonly Lock _bufferLock = new();

    private readonly float[] _fftReal = new float[FftSize];
    private readonly float[] _fftImag = new float[FftSize];
    private readonly float[] _hannWindow = new float[FftSize];
    private float[] _smoothedBars = [];
    private float[] _bandLevels = [];
    private float _gainRef = 0.35f;
    private long _lastDataTicks = long.MinValue;

    /// <summary>Higher values make quiet input fill more of the display (0.25 - 4, default 1).</summary>
    public float Sensitivity { get; set; } = 1f;

    public SpectrumAnalyzer()
    {
        for (int i = 0; i < FftSize; i++)
        {
            _hannWindow[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1))));
        }
    }

    /// <summary>Appends mono samples (-1..1). Safe to call from the capture thread.</summary>
    public void WriteSamples(ReadOnlySpan<float> mono)
    {
        if (mono.IsEmpty) return;
        lock (_bufferLock)
        {
            foreach (float s in mono)
            {
                _sampleBuffer[_writePos] = s;
                _writePos = (_writePos + 1) % BufferSize;
            }
            _lastDataTicks = Environment.TickCount64;
        }
    }

    /// <summary>Clears buffered audio so the bars fall away (e.g. when capture stops).</summary>
    public void Reset()
    {
        lock (_bufferLock)
        {
            Array.Clear(_sampleBuffer);
            _lastDataTicks = long.MinValue;
        }
    }

    public float[] GetBands(int count, int sampleRate)
    {
        if (count <= 0) return [];

        if (_smoothedBars.Length != count)
        {
            _smoothedBars = new float[count];
            _bandLevels = new float[count];
        }

        float sensitivity = Math.Clamp(Sensitivity, 0.25f, 4f);
        bool stale = _lastDataTicks == long.MinValue || Environment.TickCount64 - _lastDataTicks > 250;
        if (stale)
        {
            Decay(count);
            return _smoothedBars;
        }

        float sumSquares = 0f;
        lock (_bufferLock)
        {
            int readIdx = (_writePos - FftSize + BufferSize) % BufferSize;
            for (int i = 0; i < FftSize; i++)
            {
                float s = _sampleBuffer[(readIdx + i) % BufferSize];
                sumSquares += s * s;
                _fftReal[i] = s * _hannWindow[i];
                _fftImag[i] = 0f;
            }
        }

        float rms = (float)Math.Sqrt(sumSquares / FftSize);
        if (rms < 0.0005f / sensitivity)
        {
            Decay(count);
            return _smoothedBars;
        }

        ComputeFft(_fftReal, _fftImag);

        // Sensitivity shifts the whole display up or down in dB (4x = +24 dB, 0.25x = -24 dB) so a
        // weak source such as a distant mic can rise above the display floor, not just fill the height.
        float sensitivityDb = 40f * (float)Math.Log10(sensitivity);

        if (sampleRate <= 0) sampleRate = 48000;
        double binWidth = (double)sampleRate / FftSize;
        const double minFreq = 32.0;
        const double maxFreq = 16000.0;
        int maxBin = FftSize / 2 - 1;

        for (int b = 0; b < count; b++)
        {
            double fStart = minFreq * Math.Pow(maxFreq / minFreq, (double)b / count);
            double fEnd = minFreq * Math.Pow(maxFreq / minFreq, (double)(b + 1) / count);

            int binStart = Math.Clamp((int)(fStart / binWidth), 1, maxBin);
            int binEnd = Math.Clamp((int)(fEnd / binWidth), binStart, maxBin);

            // Peak bin in the band (not the average): averaging washes narrow bass/kick peaks
            // out across wide high-frequency bands and makes the display look sluggish.
            float peakMag = 0f;
            for (int k = binStart; k <= binEnd; k++)
            {
                float r = _fftReal[k];
                float im = _fftImag[k];
                float m = (float)Math.Sqrt(r * r + im * im);
                if (m > peakMag) peakMag = m;
            }

            // Normalise to amplitude (Hann coherent gain = FftSize / 2), then to dB so bars follow
            // perceived loudness instead of clipping at 1.0 on any real music signal.
            float amplitude = peakMag * 2f / (FftSize * 0.5f);
            float db = 20f * (float)Math.Log10(Math.Max(amplitude, 1e-6f));
            float tilt = (float)b / count * 9f; // +dB tilt: highs carry less energy than bass
            _bandLevels[b] = Math.Clamp((db + tilt + sensitivityDb + DbFloor) / DbFloor, 0f, 1.5f);
        }

        // Auto-gain: follow the loudest band with a slow release so the display fills the height at
        // any playback volume while still showing dynamics (quiet passages = low bars). Sensitivity
        // lowers the floor of that reference so weak sources (a distant mic) still fill the display.
        float frameMax = 0f;
        for (int b = 0; b < count; b++) frameMax = Math.Max(frameMax, _bandLevels[b]);
        _gainRef = frameMax > _gainRef ? frameMax : _gainRef * 0.995f + frameMax * 0.005f;
        _gainRef = Math.Max(_gainRef, 0.35f / sensitivity);
        float norm = 0.95f / _gainRef;

        for (int b = 0; b < count; b++)
        {
            float target = Math.Clamp(_bandLevels[b] * norm, 0f, 1f);
            target *= target; // mild expansion separates beats from the sustained floor

            // Asymmetric smoothing: near-instant attack, quick-but-smooth release
            float coeff = target > _smoothedBars[b] ? 0.9f : 0.3f;
            _smoothedBars[b] += (target - _smoothedBars[b]) * coeff;
        }

        return _smoothedBars;
    }

    private void Decay(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _smoothedBars[i] *= 0.85f;
            if (_smoothedBars[i] < 0.002f) _smoothedBars[i] = 0f;
        }
    }

    private static void ComputeFft(float[] real, float[] imag)
    {
        int n = real.Length;
        int j = 0;

        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
            int k = n / 2;
            while (k <= j)
            {
                j -= k;
                k /= 2;
            }
            j += k;
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2.0 * Math.PI / len;
            double wStepReal = Math.Cos(angle);
            double wStepImag = Math.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                double wReal = 1.0;
                double wImag = 0.0;
                int halfLen = len / 2;

                for (int k = 0; k < halfLen; k++)
                {
                    int u = i + k;
                    int v = i + k + halfLen;

                    double tr = wReal * real[v] - wImag * imag[v];
                    double ti = wReal * imag[v] + wImag * real[v];

                    real[v] = (float)(real[u] - tr);
                    imag[v] = (float)(imag[u] - ti);
                    real[u] = (float)(real[u] + tr);
                    imag[u] = (float)(imag[u] + ti);

                    double nextWReal = wReal * wStepReal - wImag * wStepImag;
                    double nextWImag = wReal * wStepImag + wImag * wStepReal;
                    wReal = nextWReal;
                    wImag = nextWImag;
                }
            }
        }
    }
}
