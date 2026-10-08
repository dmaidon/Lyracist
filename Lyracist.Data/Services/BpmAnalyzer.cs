// Created on Oct 7, 2026 @ 19:55:00 -> Managed audio BPM analysis via onset-envelope autocorrelation and folding
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lyracist.Data.Services;

/// <summary>
/// Managed audio BPM analyzer.
/// Decodes a 60-second window from the middle of an audio file via ffmpeg into mono 11025 Hz PCM,
/// calculates an onset-strength energy envelope, and computes autocorrelation to determine the tempo.
/// Folds candidates into the standard 70-180 BPM range and assesses confidence before returning a rounded BPM.
/// </summary>
public static class BpmAnalyzer
{
    public const int DefaultSampleRate = 11025;
    public const int DefaultWindowSeconds = 60;
    public const int MinFoldedBpm = 70;
    public const int MaxFoldedBpm = 180;
    public const double MinConfidenceProminence = 2.2;

    /// <summary>
    /// Decodes audio via ffmpeg and detects BPM. Returns null if confidence is low, file is missing,
    /// or analysis cannot determine a strong periodic beat.
    /// </summary>
    public static async Task<int?> AnalyzeBpmAsync(string filePath, double? durationSeconds = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            double duration = durationSeconds ?? 0;
            if (duration <= 0)
            {
                var probe = await FFprobeRunner.ProbeFile(filePath);
                duration = probe.Duration;
            }

            double startSec = 0;
            double windowSec = DefaultWindowSeconds;
            if (duration > DefaultWindowSeconds + 15)
            {
                startSec = (duration - DefaultWindowSeconds) / 2.0;
            }
            else if (duration > 15)
            {
                startSec = 5.0;
                windowSec = Math.Max(10.0, duration - 10.0);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = FFmpegService.FFmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (startSec > 0)
            {
                startInfo.ArgumentList.Add("-ss");
                startInfo.ArgumentList.Add(startSec.ToString("F2", CultureInfo.InvariantCulture));
            }

            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(windowSec.ToString("F2", CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-vn");
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(DefaultSampleRate.ToString());
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("s16le");
            startInfo.ArgumentList.Add("-");

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var ms = new MemoryStream();
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cancellationToken);

            byte[] pcmBytes = ms.ToArray();
            int sampleCount = pcmBytes.Length / 2;
            if (sampleCount < DefaultSampleRate * 5)
            {
                return null; // Less than 5s of decoded audio
            }

            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short val = BitConverter.ToInt16(pcmBytes, i * 2);
                samples[i] = val / 32768f;
            }

            return AnalyzeSamples(samples, DefaultSampleRate);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Lyracist.Shared.Globals.LogError("Lyracist", $"BPM analysis failed for {filePath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Pure managed BPM detection from mono floating-point audio samples.
    /// </summary>
    public static int? AnalyzeSamples(ReadOnlySpan<float> samples, int sampleRate = DefaultSampleRate)
    {
        if (samples.Length < sampleRate * 4)
            return null;

        const int hopSize = 128;
        const int frameSize = 512;
        int frameCount = (samples.Length - frameSize) / hopSize;
        if (frameCount < 100)
            return null;

        double frameRate = (double)sampleRate / hopSize;

        // 1. Calculate frame RMS energy
        double[] energy = new double[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            int offset = i * hopSize;
            double sumSq = 0.0;
            for (int j = 0; j < frameSize; j++)
            {
                float s = samples[offset + j];
                sumSq += s * s;
            }
            energy[i] = Math.Sqrt(sumSq / frameSize);
        }

        // 2. Onset-strength envelope (half-wave rectified difference)
        double[] onset = new double[frameCount];
        double maxOnset = 0.0;
        double sumOnset = 0.0;
        for (int i = 1; i < frameCount; i++)
        {
            double diff = energy[i] - energy[i - 1];
            onset[i] = diff > 0 ? diff : 0.0;
            if (onset[i] > maxOnset) maxOnset = onset[i];
            sumOnset += onset[i];
        }

        double meanOnset = sumOnset / frameCount;
        // If there are no distinct bursts/transients (e.g. continuous uniform noise or drone), return null
        if (meanOnset < 1e-7 || maxOnset < 8.0 * meanOnset)
            return null;

        // 3. Local mean subtraction across ~0.5s window
        int avgWindow = Math.Max(5, (int)(frameRate * 0.5));
        int halfW = avgWindow / 2;
        double[] relOnset = new double[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            int start = Math.Max(0, i - halfW);
            int end = Math.Min(frameCount - 1, i + halfW);
            double sum = 0.0;
            for (int k = start; k <= end; k++)
            {
                sum += onset[k];
            }
            double mean = sum / (end - start + 1);
            relOnset[i] = Math.Max(0.0, onset[i] - mean);
        }

        // 4. Zero-mean the onset envelope to eliminate non-zero DC baseline in autocorrelation
        double sumRel = 0.0;
        for (int i = 0; i < frameCount; i++) sumRel += relOnset[i];
        double meanRel = sumRel / frameCount;

        double[] centered = new double[frameCount];
        double var0 = 0.0;
        for (int i = 0; i < frameCount; i++)
        {
            centered[i] = relOnset[i] - meanRel;
            var0 += centered[i] * centered[i];
        }
        var0 /= frameCount;
        if (var0 < 1e-9)
            return null; // Silent or non-fluctuating signal

        // 5. Autocorrelation over lags corresponding to tempos 50..220 BPM
        int minLag = Math.Max(2, (int)Math.Round(frameRate * 60.0 / 220.0));
        int maxLag = (int)Math.Round(frameRate * 60.0 / 50.0);
        if (maxLag >= frameCount / 2) maxLag = frameCount / 2 - 1;
        if (minLag >= maxLag) return null;

        double[] r = new double[maxLag + 2];
        double maxR = -1.0;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double sum = 0.0;
            int count = frameCount - lag;
            for (int j = 0; j < count; j++)
            {
                sum += centered[j] * centered[j + lag];
            }
            // Normalized correlation coefficient (-1.0 to 1.0)
            r[lag] = (sum / count) / var0;
            if (r[lag] > maxR) maxR = r[lag];
        }

        // Noise rejection: random noise has max correlation < 0.15; periodic rhythm has sharp peaks >= 0.20
        if (maxR < 0.20)
            return null;

        // Interpolation helper for fractional lags
        double GetR(double lag)
        {
            int l0 = (int)Math.Floor(lag);
            int l1 = l0 + 1;
            if (l0 < minLag || l1 > maxLag) return 0.0;
            double frac = lag - l0;
            return r[l0] * (1.0 - frac) + r[l1] * frac;
        }

        // 6. Score candidate BPMs folded into [MinFoldedBpm, MaxFoldedBpm] (70..180)
        double bestScore = -1.0;
        int bestBpm = 0;
        double sumScores = 0.0;
        double sumSqScores = 0.0;
        int candidateCount = 0;

        for (int bpm = MinFoldedBpm; bpm <= MaxFoldedBpm; bpm++)
        {
            double lag = frameRate * 60.0 / bpm;
            double score = Math.Max(0.0, GetR(lag));

            // Add subharmonic (double lag, half tempo) and harmonic (half lag, double tempo)
            double doubleLag = lag * 2.0;
            if (doubleLag <= maxLag)
                score += 0.5 * Math.Max(0.0, GetR(doubleLag));

            double halfLag = lag * 0.5;
            if (halfLag >= minLag)
                score += 0.3 * Math.Max(0.0, GetR(halfLag));

            sumScores += score;
            sumSqScores += score * score;
            candidateCount++;

            if (score > bestScore)
            {
                bestScore = score;
                bestBpm = bpm;
            }
        }

        if (candidateCount == 0 || bestScore <= 0.0)
            return null;

        double meanScore = sumScores / candidateCount;
        double variance = (sumSqScores / candidateCount) - (meanScore * meanScore);
        double stdDev = variance > 0 ? Math.Sqrt(variance) : 0.0;

        // Confidence: peak prominence over mean
        double prominence = stdDev > 1e-9 ? (bestScore - meanScore) / stdDev : 0.0;
        if (prominence < MinConfidenceProminence)
        {
            return null; // Ambiguous or flat autocorrelation
        }

        return bestBpm;
    }
}
