// Created on Oct 3, 2026 @ 12:12:00 -> Real-time WASAPI Loopback Audio Spectrum Analyzer service with Radix-2 FFT
using System;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Lyracist.Services.Media;

public interface IAudioSpectrumService : IDisposable
{
    bool IsCapturing { get; }
    void Start();
    void Stop();
    float[] GetFrequencyBands(int count);
}

public sealed class AudioSpectrumService : IAudioSpectrumService
{
    private const int FftSize = 1024;
    private const int BufferSize = 4096;

    private readonly float[] _sampleBuffer = new float[BufferSize];
    private int _writePos;
    private readonly Lock _bufferLock = new();

#pragma warning disable CS0618 // WasapiLoopbackCapture is standard for system-wide loopback capture in NAudio
    private WasapiLoopbackCapture? _capture;
#pragma warning restore CS0618
    private bool _isCapturing;
    private bool _isDisposed;
    private DateTime _lastDataReceived = DateTime.MinValue;

    private readonly float[] _fftReal = new float[FftSize];
    private readonly float[] _fftImag = new float[FftSize];
    private readonly float[] _hannWindow = new float[FftSize];
    private float[] _smoothedBars = [];
    private float[] _bandLevels = [];
    private float _gainRef = 0.35f;
    private const float DbFloor = 60f;

    public bool IsCapturing => _isCapturing;

    public AudioSpectrumService()
    {
        // Pre-compute Hann window
        for (int i = 0; i < FftSize; i++)
        {
            _hannWindow[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (FftSize - 1))));
        }
    }

    public void Start()
    {
        if (_isDisposed || _isCapturing) return;

        lock (_bufferLock)
        {
            try
            {
                string customDevice = Lyracist.Core.Helpers.AppSettings.SelectedKaraokeAudioDevice;
                MMDevice? targetDevice = null;

                if (!string.IsNullOrEmpty(customDevice) && customDevice != "Default System Device")
                {
                    try
                    {
                        using var enumerator = new MMDeviceEnumerator();
                        targetDevice = enumerator.GetDevice(customDevice);
                    }
                    catch
                    {
                        targetDevice = null;
                    }
                }

#pragma warning disable CS0618
                _capture = targetDevice != null
                    ? new WasapiLoopbackCapture(targetDevice)
                    : new WasapiLoopbackCapture();
#pragma warning restore CS0618

                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                _isCapturing = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioSpectrumService] Unable to start WASAPI loopback: {ex.Message}");
                _capture?.Dispose();
                _capture = null;
                _isCapturing = false;
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || _capture == null) return;

        _lastDataReceived = DateTime.UtcNow;
        var format = _capture.WaveFormat;
        int channels = Math.Max(1, format.Channels);
        int bitsPerSample = format.BitsPerSample;

        lock (_bufferLock)
        {
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && bitsPerSample == 32)
            {
                int sampleCount = e.BytesRecorded / (4 * channels);
                for (int i = 0; i < sampleCount; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int byteOffset = (i * channels + ch) * 4;
                        if (byteOffset + 4 <= e.BytesRecorded)
                        {
                            sum += BitConverter.ToSingle(e.Buffer, byteOffset);
                        }
                    }
                    _sampleBuffer[_writePos] = sum / channels;
                    _writePos = (_writePos + 1) % BufferSize;
                }
            }
            else if (bitsPerSample == 16)
            {
                int sampleCount = e.BytesRecorded / (2 * channels);
                for (int i = 0; i < sampleCount; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        int byteOffset = (i * channels + ch) * 2;
                        if (byteOffset + 2 <= e.BytesRecorded)
                        {
                            short val = BitConverter.ToInt16(e.Buffer, byteOffset);
                            sum += val / 32768.0f;
                        }
                    }
                    _sampleBuffer[_writePos] = sum / channels;
                    _writePos = (_writePos + 1) % BufferSize;
                }
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;
    }

    public void Stop()
    {
        lock (_bufferLock)
        {
            if (!_isCapturing) return;

            try
            {
                _capture?.StopRecording();
            }
            catch { }

            _capture?.Dispose();
            _capture = null;
            _isCapturing = false;
        }
    }

    public float[] GetFrequencyBands(int count)
    {
        if (count <= 0) return [];

        if (_smoothedBars.Length != count)
        {
            _smoothedBars = new float[count];
            _bandLevels = new float[count];
        }

        // If capture stopped or no data received for 250ms, decay bars toward zero
        if (!_isCapturing || (DateTime.UtcNow - _lastDataReceived).TotalMilliseconds > 250)
        {
            for (int i = 0; i < count; i++)
            {
                _smoothedBars[i] *= 0.85f;
                if (_smoothedBars[i] < 0.002f) _smoothedBars[i] = 0f;
            }
            return _smoothedBars;
        }

        // Extract latest FftSize samples from circular buffer
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
        if (rms < 0.0005f)
        {
            for (int i = 0; i < count; i++)
            {
                _smoothedBars[i] *= 0.85f;
                if (_smoothedBars[i] < 0.002f) _smoothedBars[i] = 0f;
            }
            return _smoothedBars;
        }

        // Perform Radix-2 Cooley-Tukey FFT
        ComputeFft(_fftReal, _fftImag);

        // Group into logarithmic frequency bands
        int sampleRate = _capture?.WaveFormat.SampleRate ?? 48000;
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
            _bandLevels[b] = Math.Clamp((db + tilt + DbFloor) / DbFloor, 0f, 1.5f);
        }

        // Auto-gain: follow the loudest band with a slow release so the display fills the height at
        // any playback volume while still showing dynamics (quiet passages = low bars).
        float frameMax = 0f;
        for (int b = 0; b < count; b++) frameMax = Math.Max(frameMax, _bandLevels[b]);
        _gainRef = frameMax > _gainRef ? frameMax : _gainRef * 0.995f + frameMax * 0.005f;
        _gainRef = Math.Max(_gainRef, 0.35f);
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}
