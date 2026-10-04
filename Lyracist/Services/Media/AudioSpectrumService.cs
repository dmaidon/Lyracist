// Edited on Oct 3, 2026 @ 14:45:00 -> Delegate FFT/band scaling to Shared/SpectrumAnalyzer; this class now only owns WASAPI loopback capture
using System;
using System.Diagnostics;
using Lyracist.Shared;
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
    private readonly SpectrumAnalyzer _analyzer = new();
    private readonly object _captureLock = new();

#pragma warning disable CS0618 // WasapiLoopbackCapture is standard for system-wide loopback capture in NAudio
    private WasapiLoopbackCapture? _capture;
#pragma warning restore CS0618
    private bool _isCapturing;
    private bool _isDisposed;
    private int _sampleRate = 48000;
    private float[] _monoScratch = new float[2048];

    public bool IsCapturing => _isCapturing;

    public void Start()
    {
        if (_isDisposed || _isCapturing) return;

        lock (_captureLock)
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

                _sampleRate = _capture.WaveFormat.SampleRate;
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
        var capture = _capture;
        if (e.BytesRecorded <= 0 || capture == null) return;

        int count = MonoSampleConverter.ToMono(capture.WaveFormat, e.Buffer, e.BytesRecorded, ref _monoScratch);
        if (count > 0) _analyzer.WriteSamples(_monoScratch.AsSpan(0, count));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _isCapturing = false;
    }

    public void Stop()
    {
        lock (_captureLock)
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
            _analyzer.Reset();
        }
    }

    public float[] GetFrequencyBands(int count) => _analyzer.GetBands(count, _sampleRate);

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
    }
}
