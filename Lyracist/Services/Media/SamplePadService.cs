// Created on Oct 7, 2026 @ 19:46:00 -> Service managing 16-pad DJ sample / jingle playback with RAM caching and ducking
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Lyracist.Core.Helpers;
using Lyracist.Core.Interfaces;
using Lyracist.Data.Services;
using Lyracist.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Lyracist.Services.Media;

public class SamplePadService : ISamplePadService
{
    private const int TotalPadCount = 16;
    private const int SampleRate = 44100;
    private const int Channels = 2;

    private readonly IShowFlowService _showFlow;
    private readonly List<SamplePadItem> _pads = [];
    private readonly Dictionary<int, float[]> _cachedAudioBuffers = new();
    private readonly HashSet<int> _activePlayingSlots = [];
    private readonly object _stateLock = new();

    private readonly WaveFormat _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);
    private MemoryMixerSampleProvider? _mixer;
#pragma warning disable CS0618 // WasapiOut is standard for low-latency output in NAudio.Wasapi
    private WasapiOut? _wasapiOut;
#pragma warning restore CS0618
    private bool _isDisposed;

    public IReadOnlyList<SamplePadItem> Pads
    {
        get { lock (_stateLock) { return _pads.ToList(); } }
    }

    public bool IsPlaying
    {
        get { lock (_stateLock) { return _activePlayingSlots.Count > 0; } }
    }

    public event EventHandler? StateChanged;
    public event Action<int, bool>? PadPlayingChanged;

    private static readonly string[] DefaultColors =
    [
        "#2563EB", "#7C3AED", "#DB2777", "#D97706",
        "#059669", "#0891B2", "#4F46E5", "#E11D48",
        "#2563EB", "#7C3AED", "#DB2777", "#D97706",
        "#059669", "#0891B2", "#4F46E5", "#E11D48"
    ];

    private static readonly string[] DefaultHotkeys =
    [
        "F1", "F2", "F3", "F4",
        "F5", "F6", "F7", "F8",
        "F9", "F10", "F11", "F12",
        "", "", "", ""
    ];

    public SamplePadService(IShowFlowService showFlow)
    {
        _showFlow = showFlow;
        InitializeDefaultPads();
    }

    private void InitializeDefaultPads()
    {
        lock (_stateLock)
        {
            _pads.Clear();
            for (int i = 0; i < TotalPadCount; i++)
            {
                _pads.Add(new SamplePadItem
                {
                    SlotIndex = i,
                    Label = $"Pad {i + 1}",
                    FilePath = string.Empty,
                    Color = DefaultColors[i % DefaultColors.Length],
                    HotKey = DefaultHotkeys[i],
                    Volume = 1.0,
                    Order = i
                });
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadConfigAsync();
        EnsureAudioEngine();
    }

    private static string GetConfigPath()
    {
        string dir = Globals.SettingsDir;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "samplepad.json");
    }

    private async Task LoadConfigAsync()
    {
        string path = GetConfigPath();
        if (File.Exists(path))
        {
            try
            {
                string json = await File.ReadAllTextAsync(path);
                var loaded = JsonSerializer.Deserialize<List<SamplePadItem>>(json);
                if (loaded != null)
                {
                    lock (_stateLock)
                    {
                        foreach (var item in loaded)
                        {
                            if (item.SlotIndex >= 0 && item.SlotIndex < TotalPadCount)
                            {
                                _pads[item.SlotIndex] = item;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Globals.LogError("SamplePadService", "Failed to load samplepad.json", ex);
            }
        }

        // Preload valid assigned clips into RAM
        List<SamplePadItem> toLoad;
        lock (_stateLock) { toLoad = _pads.Where(p => !string.IsNullOrEmpty(p.FilePath) && File.Exists(p.FilePath)).ToList(); }

        foreach (var pad in toLoad)
        {
            await PreloadBufferAsync(pad.SlotIndex, pad.FilePath);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveConfig()
    {
        try
        {
            string path = GetConfigPath();
            List<SamplePadItem> snapshot;
            lock (_stateLock) { snapshot = _pads.ToList(); }
            AtomicJsonFile.Serialize(path, snapshot, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            Globals.LogError("SamplePadService", "Failed to save samplepad.json", ex);
        }
    }

    private async Task PreloadBufferAsync(int slotIndex, string filePath)
    {
        if (!File.Exists(filePath))
        {
            lock (_stateLock) { _cachedAudioBuffers.Remove(slotIndex); }
            return;
        }

        float[]? samples = await DecodeAudioToFloatPcmAsync(filePath);
        if (samples != null)
        {
            lock (_stateLock)
            {
                _cachedAudioBuffers[slotIndex] = samples;
            }
        }
    }

    private static async Task<float[]?> DecodeAudioToFloatPcmAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegService.FFmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("f32le");
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add(Channels.ToString());
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(SampleRate.ToString());
            startInfo.ArgumentList.Add("-");

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var ms = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(ms);
            await process.WaitForExitAsync();

            byte[] bytes = ms.ToArray();
            if (bytes.Length == 0) return null;

            float[] samples = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
            return samples;
        }
        catch (Exception ex)
        {
            Globals.LogError("SamplePadService", $"Failed to decode audio: {filePath}", ex);
            return null;
        }
    }

    public void AssignPad(int slotIndex, string filePath, string? label = null, string? color = null, string? hotkey = null)
    {
        if (slotIndex < 0 || slotIndex >= TotalPadCount) return;

        lock (_stateLock)
        {
            var pad = _pads[slotIndex];
            pad.FilePath = filePath;
            if (!string.IsNullOrEmpty(label)) pad.Label = label;
            else pad.Label = Path.GetFileNameWithoutExtension(filePath);

            if (!string.IsNullOrEmpty(color)) pad.Color = color;
            if (hotkey != null) pad.HotKey = hotkey;
        }

        SaveConfig();
        _ = PreloadBufferAsync(slotIndex, filePath).ContinueWith(_ =>
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }, TaskScheduler.Default);

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearPad(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalPadCount) return;

        lock (_stateLock)
        {
            var pad = _pads[slotIndex];
            pad.FilePath = string.Empty;
            pad.Label = $"Pad {slotIndex + 1}";
            _cachedAudioBuffers.Remove(slotIndex);
        }

        SaveConfig();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdatePad(SamplePadItem item)
    {
        if (item.SlotIndex < 0 || item.SlotIndex >= TotalPadCount) return;

        bool pathChanged = false;
        lock (_stateLock)
        {
            var existing = _pads[item.SlotIndex];
            pathChanged = !string.Equals(existing.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase);
            _pads[item.SlotIndex] = item;
        }

        SaveConfig();
        if (pathChanged && !string.IsNullOrEmpty(item.FilePath))
        {
            _ = PreloadBufferAsync(item.SlotIndex, item.FilePath).ContinueWith(_ =>
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }, TaskScheduler.Default);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureAudioEngine()
    {
        if (_wasapiOut != null && _mixer != null) return;

        try
        {
            _mixer = new MemoryMixerSampleProvider(_waveFormat);
            string targetDevice = !string.IsNullOrEmpty(AppSettings.SamplePadOutputDevice)
                ? AppSettings.SamplePadOutputDevice
                : AppSettings.SelectedBgmAudioDevice;

            MMDevice? endpoint = null;
            if (!string.IsNullOrEmpty(targetDevice) && targetDevice != "Default System Device")
            {
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    endpoint = enumerator.GetDevice(targetDevice);
                }
                catch
                {
                    endpoint = null;
                }
            }

#pragma warning disable CS0618
            if (endpoint != null)
            {
                _wasapiOut = new WasapiOut(endpoint, AudioClientShareMode.Shared, true, 50);
            }
            else
            {
                using var enumerator = new MMDeviceEnumerator();
                var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _wasapiOut = new WasapiOut(def, AudioClientShareMode.Shared, true, 50);
            }
#pragma warning restore CS0618

            _wasapiOut.Init(new FloatSampleToWaveProvider(_mixer));
            _wasapiOut.Play();
        }
        catch (Exception ex)
        {
            Globals.LogError("SamplePadService", "Failed to initialize WasapiOut audio engine", ex);
        }
    }

    public void SetOutputDevice(string deviceId)
    {
        try
        {
            StopAll();
            _wasapiOut?.Stop();
            _wasapiOut?.Dispose();
            _wasapiOut = null;
            _mixer = null;

            EnsureAudioEngine();
        }
        catch (Exception ex)
        {
            Globals.LogError("SamplePadService", $"Failed to set output device: {deviceId}", ex);
        }
    }

    public void PlayPad(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalPadCount) return;

        SamplePadItem pad;
        float[]? buffer;
        lock (_stateLock)
        {
            pad = _pads[slotIndex];
            _cachedAudioBuffers.TryGetValue(slotIndex, out buffer);
        }

        if (buffer == null || buffer.Length == 0 || pad.IsMissing)
        {
            return;
        }

        EnsureAudioEngine();
        if (_mixer == null) return;

        bool wasPlayingBefore;
        lock (_stateLock)
        {
            wasPlayingBefore = _activePlayingSlots.Count > 0;
            _activePlayingSlots.Add(slotIndex);
        }

        PadPlayingChanged?.Invoke(slotIndex, true);
        if (!wasPlayingBefore)
        {
            _showFlow.DuckFillIn();
        }

        var voice = new MemoryAudioVoice(buffer, _waveFormat, pad.Volume, () =>
        {
            bool hasRemainingVoices;
            lock (_stateLock)
            {
                _activePlayingSlots.Remove(slotIndex);
                hasRemainingVoices = _activePlayingSlots.Count > 0;
            }

            PadPlayingChanged?.Invoke(slotIndex, false);
            if (!hasRemainingVoices)
            {
                _showFlow.UnduckFillIn();
            }
        });

        _mixer.AddInput(voice);
    }

    public void StopAll()
    {
        lock (_stateLock)
        {
            _activePlayingSlots.Clear();
        }

        _mixer?.RemoveAllInputs();
        _showFlow.UnduckFillIn();

        for (int i = 0; i < TotalPadCount; i++)
        {
            PadPlayingChanged?.Invoke(i, false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        StopAll();
        _wasapiOut?.Stop();
        _wasapiOut?.Dispose();
        _wasapiOut = null;
        _mixer = null;

        GC.SuppressFinalize(this);
    }

    // ─── Mixer and Voice Providers ──────────────────────────────────────────

    private sealed class MemoryAudioVoice : ISampleProvider
    {
        private readonly float[] _samples;
        private readonly WaveFormat _waveFormat;
        private readonly double _volume;
        private int _position;
        private readonly Action _onEnded;
        private bool _endedNotified;

        public WaveFormat WaveFormat => _waveFormat;

        public MemoryAudioVoice(float[] samples, WaveFormat waveFormat, double volume, Action onEnded)
        {
            _samples = samples;
            _waveFormat = waveFormat;
            _volume = Math.Clamp(volume, 0.0, 2.0);
            _onEnded = onEnded;
        }

        public int Read(Span<float> buffer)
        {
            int available = _samples.Length - _position;
            if (available <= 0)
            {
                NotifyEnded();
                return 0;
            }

            int samplesToRead = Math.Min(available, buffer.Length);
            for (int i = 0; i < samplesToRead; i++)
            {
                buffer[i] = (float)(_samples[_position + i] * _volume);
            }
            _position += samplesToRead;

            if (_position >= _samples.Length)
            {
                NotifyEnded();
            }

            return samplesToRead;
        }

        private void NotifyEnded()
        {
            if (!_endedNotified)
            {
                _endedNotified = true;
                _onEnded();
            }
        }
    }

    private sealed class MemoryMixerSampleProvider : ISampleProvider
    {
        private readonly List<ISampleProvider> _inputs = [];
        private readonly object _lock = new();

        public WaveFormat WaveFormat { get; }

        public MemoryMixerSampleProvider(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;
        }

        public void AddInput(ISampleProvider input)
        {
            lock (_lock)
            {
                _inputs.Add(input);
            }
        }

        public void RemoveAllInputs()
        {
            lock (_lock)
            {
                _inputs.Clear();
            }
        }

        public int Read(Span<float> buffer)
        {
            buffer.Clear();
            lock (_lock)
            {
                if (_inputs.Count == 0)
                {
                    return buffer.Length;
                }

                float[] temp = new float[buffer.Length];
                for (int i = _inputs.Count - 1; i >= 0; i--)
                {
                    int read = _inputs[i].Read(temp);
                    for (int j = 0; j < read; j++)
                    {
                        buffer[j] = Math.Clamp(buffer[j] + temp[j], -1.0f, 1.0f);
                    }
                    if (read == 0)
                    {
                        _inputs.RemoveAt(i);
                    }
                }
            }
            return buffer.Length;
        }
    }

    private sealed class FloatSampleToWaveProvider : IWaveProvider
    {
        private readonly ISampleProvider _source;
        public WaveFormat WaveFormat => _source.WaveFormat;

        public FloatSampleToWaveProvider(ISampleProvider source)
        {
            _source = source;
        }

        public int Read(Span<byte> buffer)
        {
            int floatCount = buffer.Length / sizeof(float);
            float[] floatBuffer = new float[floatCount];
            int readSamples = _source.Read(floatBuffer);
            var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(floatBuffer.AsSpan(0, readSamples));
            byteSpan.CopyTo(buffer);
            return byteSpan.Length;
        }
    }
}
