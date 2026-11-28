// Created on Nov 28, 2026 -> Shared FFmpeg process runner: drains stderr, kills on cancel, checks exit code
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyracist.Data.Services
{
    public static class FfmpegRunner
    {
        private const int ErrorTailLength = 2000;

        public static string ResolveFfmpegPath()
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(local)) return local;
            if (File.Exists(@"C:\ffmpeg\bin\ffmpeg.exe")) return @"C:\ffmpeg\bin\ffmpeg.exe";
            return "ffmpeg";
        }

        /// <summary>
        /// Builds a key-shift/tempo filter chain. Resamples to 44.1 kHz before asetrate so 48 kHz (etc.)
        /// sources keep the right speed and pitch. Returns empty when nothing needs to change.
        /// </summary>
        public static string BuildPitchTempoFilter(int keySemitones, double tempoFactor)
        {
            bool tempoChanged = Math.Abs(tempoFactor - 1.0) > 0.001;
            if (keySemitones == 0 && !tempoChanged) return string.Empty;

            var parts = new List<string>();
            double pitch = Math.Pow(2.0, keySemitones / 12.0);
            if (keySemitones != 0)
            {
                parts.Add("aresample=44100");
                parts.Add($"asetrate={Math.Round(44100 * pitch).ToString(CultureInfo.InvariantCulture)}");
                parts.Add("aresample=44100");
            }

            // A single atempo stage only goes down to 0.5, so chain stages when needed.
            double tempo = tempoFactor / pitch;
            while (tempo < 0.5)
            {
                parts.Add("atempo=0.5");
                tempo /= 0.5;
            }
            parts.Add($"atempo={tempo.ToString("F5", CultureInfo.InvariantCulture)}");
            return string.Join(",", parts);
        }

        /// <summary>
        /// Runs FFmpeg to completion. Error output is read while the process runs (an unread pipe fills
        /// up and stalls FFmpeg). Throws on a non-zero exit code; kills the process if cancelled.
        /// </summary>
        public static async Task RunAsync(string arguments, CancellationToken token, Func<Stream, CancellationToken, Task>? writeInput = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ResolveFfmpegPath(),
                Arguments = "-nostats -loglevel error " + arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = writeInput != null
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg could not be started.");
            var errorTail = new StringBuilder();
            Task drain = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    errorTail.AppendLine(line);
                    if (errorTail.Length > ErrorTailLength) errorTail.Remove(0, errorTail.Length - ErrorTailLength);
                }
            }, CancellationToken.None);

            try
            {
                if (writeInput != null)
                {
                    // Kill on cancel so a write blocked on a full pipe can't hang forever.
                    using var killOnCancel = token.Register(() => TryKill(process));
                    try
                    {
                        await writeInput(process.StandardInput.BaseStream, token).ConfigureAwait(false);
                        process.StandardInput.Close();
                    }
                    catch (IOException)
                    {
                        // FFmpeg exited early; the exit code check below reports why.
                        token.ThrowIfCancellationRequested();
                    }
                }

                await process.WaitForExitAsync(token).ConfigureAwait(false);
                await drain.ConfigureAwait(false);
            }
            catch
            {
                TryKill(process);
                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg failed (exit code {process.ExitCode}). {errorTail.ToString().Trim()}");
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }
}
