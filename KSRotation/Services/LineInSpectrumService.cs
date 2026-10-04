// Created on Oct 3, 2026 @ 14:55:00 -> Line-in / microphone spectrum capture (WASAPI) feeding the shared SpectrumAnalyzer for the on-screen synth bars
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lyracist.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace KSRotation.Services
{
    /// <summary>An audio input (mixer line-in, USB interface, microphone) the DJ can pick for the spectrum bars.</summary>
    public sealed record SpectrumInputDevice(string Id, string Name);

    /// <summary>
    /// Captures an input device (not system output - KSRotation plays no music itself) and exposes
    /// smoothed frequency bands. One shared instance feeds every screen that shows the bars.
    /// </summary>
    public sealed class LineInSpectrumService : IDisposable
    {
        public static LineInSpectrumService Instance { get; } = new();

        /// <summary>Id of the "system default input" entry in the device picker.</summary>
        public const string DefaultDeviceId = "";

        private readonly SpectrumAnalyzer _analyzer = new();
        private readonly object _lock = new();
#pragma warning disable CS0618 // WasapiCapture is the straightforward event-based capture; fine for a spectrum feed
        private WasapiCapture? _capture;
#pragma warning restore CS0618
        private string _deviceId = DefaultDeviceId;
        private int _sampleRate = 48000;
        private float[] _monoScratch = new float[2048];

        public bool IsCapturing { get; private set; }

        /// <summary>Last start failure (device unplugged, mic access denied...) or null; shown in Settings.</summary>
        public string? LastError { get; private set; }

        private string _style = SpectrumBarStyles.Default;

        /// <summary>Bar color theme shared by every screen (see <see cref="SpectrumBarStyles.Names"/>).</summary>
        public string Style
        {
            get => _style;
            set
            {
                string normalized = SpectrumBarStyles.Normalize(value);
                if (normalized == _style) return;
                _style = normalized;
                StyleChanged?.Invoke();
            }
        }

        /// <summary>Raised on the calling thread when <see cref="Style"/> changes so open screens re-colour live.</summary>
        public event Action? StyleChanged;

        public float Sensitivity
        {
            get => _analyzer.Sensitivity;
            set => _analyzer.Sensitivity = value;
        }

        /// <summary>Bar opacity shared by every screen (0.1 - 1).</summary>
        public double Opacity { get; set; } = 0.85;

        public static IReadOnlyList<SpectrumInputDevice> GetInputDevices()
        {
            var list = new List<SpectrumInputDevice> { new(DefaultDeviceId, "System default input") };
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                foreach (var d in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
                {
                    list.Add(new SpectrumInputDevice(d.ID, d.FriendlyName));
                    d.Dispose();
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("LineInSpectrumService.GetInputDevices", ex);
            }
            return list;
        }

        /// <summary>Starts, restarts (device changed) or stops capture. Cheap to call repeatedly.</summary>
        public void Configure(bool enabled, string? deviceId)
        {
            deviceId ??= DefaultDeviceId;
            lock (_lock)
            {
                if (!enabled)
                {
                    StopLocked();
                    return;
                }

                if (IsCapturing && deviceId == _deviceId) return;

                StopLocked();
                _deviceId = deviceId;
                StartLocked();
            }
        }

        private void StartLocked()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using MMDevice device = string.IsNullOrEmpty(_deviceId)
                    ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
                    : enumerator.GetDevice(_deviceId);

#pragma warning disable CS0618
                _capture = new WasapiCapture(device);
#pragma warning restore CS0618
                _sampleRate = _capture.WaveFormat.SampleRate;
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                IsCapturing = true;
                LastError = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LineInSpectrumService] Unable to start capture: {ex.Message}");
                LoggerService.LogError("LineInSpectrumService.Start", ex);
                LastError = ex.Message;
                _capture?.Dispose();
                _capture = null;
                IsCapturing = false;
            }
        }

        private void StopLocked()
        {
            if (_capture != null)
            {
                try { _capture.StopRecording(); } catch { }
                _capture.Dispose();
                _capture = null;
            }
            IsCapturing = false;
            _analyzer.Reset();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            var capture = _capture;
            if (capture == null || e.BytesRecorded <= 0) return;

            int count = MonoSampleConverter.ToMono(capture.WaveFormat, e.Buffer, e.BytesRecorded, ref _monoScratch);
            if (count > 0) _analyzer.WriteSamples(_monoScratch.AsSpan(0, count));
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            IsCapturing = false;
            if (e.Exception != null) LastError = e.Exception.Message;
        }

        public float[] GetBands(int count) => _analyzer.GetBands(count, _sampleRate);

        public void Dispose()
        {
            lock (_lock) StopLocked();
        }
    }
}
