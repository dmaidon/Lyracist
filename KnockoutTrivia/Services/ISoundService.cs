// Created on Oct 31, 2026 @ 10:36:00 -> ISoundService and SoundService for synthesized game-show sound effects and audio snippet playback
using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;

namespace KnockoutTrivia.Services;

public interface ISoundService : IDisposable
{
    bool IsEnabled { get; set; }
    void PlayCountdownTick();
    void PlayRevealChime();
    void PlayStrikeBuzzer();
    void PlayShieldAbsorb();
    void PlayWheelTick();
    void PlaySuperStreakFanfare();
    void PlayWinnerFanfare();
    void PlayAudioSnippet(string? filePath);
    void StopAudioSnippet();
}

public sealed class SoundService : ISoundService
{
    private readonly byte[] _tickWav;
    private readonly byte[] _revealWav;
    private readonly byte[] _strikeWav;
    private readonly byte[] _shieldWav;
    private readonly byte[] _wheelTickWav;
    private readonly byte[] _superStreakWav;
    private readonly byte[] _winnerWav;
    private MediaPlayer? _snippetPlayer;
    private bool _disposed;

    public bool IsEnabled { get; set; } = true;

    public SoundService()
    {
        _tickWav = GenerateToneSequence([(880.0, 45, 0.22)]);
        _wheelTickWav = GenerateToneSequence([(1200.0, 22, 0.18)]);
        _revealWav = GenerateToneSequence([(523.25, 90, 0.28), (659.25, 90, 0.28), (783.99, 180, 0.30)]);
        _strikeWav = GenerateToneSequence([(165.0, 140, 0.35), (110.0, 240, 0.38)]);
        _shieldWav = GenerateToneSequence([(740.0, 80, 0.28), (587.33, 140, 0.25)]);
        _superStreakWav = GenerateToneSequence([(523.25, 80, 0.30), (659.25, 80, 0.30), (783.99, 80, 0.30), (1046.50, 220, 0.35)]);
        _winnerWav = GenerateToneSequence([(523.25, 100, 0.32), (659.25, 100, 0.32), (783.99, 100, 0.32), (1046.50, 120, 0.35), (783.99, 80, 0.30), (1046.50, 300, 0.38)]);
    }

    public void PlayCountdownTick() => PlayWavBytes(_tickWav);
    public void PlayRevealChime() => PlayWavBytes(_revealWav);
    public void PlayStrikeBuzzer() => PlayWavBytes(_strikeWav);
    public void PlayShieldAbsorb() => PlayWavBytes(_shieldWav);
    public void PlayWheelTick() => PlayWavBytes(_wheelTickWav);
    public void PlaySuperStreakFanfare() => PlayWavBytes(_superStreakWav);
    public void PlayWinnerFanfare() => PlayWavBytes(_winnerWav);

    public void PlayAudioSnippet(string? filePath)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(filePath)) return;

        try
        {
            string resolved = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

            if (!File.Exists(resolved)) return;

            RunOnUI(() =>
            {
                StopAudioSnippetInternal();
                _snippetPlayer = new MediaPlayer();
                _snippetPlayer.Open(new Uri(resolved, UriKind.Absolute));
                _snippetPlayer.Play();
            });
        }
        catch
        {
            // Ignore audio device errors on headless machines
        }
    }

    public void StopAudioSnippet()
    {
        try
        {
            RunOnUI(StopAudioSnippetInternal);
        }
        catch { }
    }

    private void StopAudioSnippetInternal()
    {
        if (_snippetPlayer != null)
        {
            try
            {
                _snippetPlayer.Stop();
                _snippetPlayer.Close();
            }
            catch { }
            _snippetPlayer = null;
        }
    }

    private void PlayWavBytes(byte[] wavData)
    {
        if (!IsEnabled || _disposed) return;

        try
        {
            using var ms = new MemoryStream(wavData, writable: false);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch
        {
            // Ignore when no audio output device is present
        }
    }

    private static byte[] GenerateToneSequence((double FrequencyHz, int DurationMs, double Volume)[] notes)
    {
        const int sampleRate = 22050;
        int totalSamples = 0;
        foreach (var n in notes)
        {
            totalSamples += (sampleRate * n.DurationMs) / 1000;
        }

        int dataChunkSize = totalSamples * 2; // 16-bit mono
        using var ms = new MemoryStream(44 + dataChunkSize);
        using var bw = new BinaryWriter(ms);

        // RIFF WAVE Header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataChunkSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16); // PCM chunk size
        bw.Write((short)1); // AudioFormat = 1 (PCM)
        bw.Write((short)1); // NumChannels = 1
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // ByteRate
        bw.Write((short)2); // BlockAlign
        bw.Write((short)16); // BitsPerSample
        bw.Write("data"u8);
        bw.Write(dataChunkSize);

        foreach (var (freq, durationMs, volume) in notes)
        {
            int noteSamples = (sampleRate * durationMs) / 1000;
            for (int i = 0; i < noteSamples; i++)
            {
                double t = (double)i / sampleRate;
                // Exponential decay envelope to prevent clicks
                double env = Math.Min(1.0, i / 80.0) * Math.Pow(1.0 - ((double)i / noteSamples), 0.75);
                double raw = Math.Sin(2.0 * Math.PI * freq * t);
                short sample = (short)(raw * volume * env * short.MaxValue);
                bw.Write(sample);
            }
        }

        return ms.ToArray();
    }

    private static void RunOnUI(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAudioSnippet();
    }
}

