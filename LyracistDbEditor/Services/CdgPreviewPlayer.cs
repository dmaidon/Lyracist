// Created on Nov 27, 2026 @ 13:10:30 -> Built-in MP3+G CDG decoder and 10-band EQ / Key Shift audition player for LyracistDbEditor
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LyracistDbEditor;

/// <summary>
/// Self-contained CDG (MP3+G / ZIP+G) graphics decoder and audio audition player for LyracistDbEditor
/// supporting 300 packets/sec CDG rendering, key transposition (-6 to +6 semitones), and a 10-band graphic equalizer.
/// </summary>
public sealed class CdgPreviewPlayer : IDisposable
{
    public const int CdgWidth = 300;
    public const int CdgHeight = 216;
    private const int PacketSize = 24;
    private const int PacketsPerSecond = 300;

    private readonly MediaPlayer _mediaPlayer = new();
    private readonly DispatcherTimer _renderTimer;
    private byte[] _cdgBytes = [];
    private int _packetCount;
    private int _currentPacketIndex;

    private readonly byte[,] _pixels = new byte[CdgWidth, CdgHeight];
    private readonly Color[] _palette = new Color[16];
    private byte _borderColorIndex;
    private WriteableBitmap? _bitmap;
    private readonly byte[] _bgraBuffer = new byte[CdgWidth * CdgHeight * 4];

    private string? _sourceFilePath;
    private string? _rawAudioPath;
    private string? _activeAudioPath;
    private string? _tempDir;
    private bool _isPlaying;
    private CancellationTokenSource? _dspCts;

    public static readonly int[] EqFrequenciesHz = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    public event Action<ImageSource?>? FrameUpdated;
    public event Action? PlaybackStateChanged;

    public bool IsPlaying => _isPlaying;
    public TimeSpan Position => _mediaPlayer.Position;
    public TimeSpan Duration => _mediaPlayer.NaturalDuration.HasTimeSpan ? _mediaPlayer.NaturalDuration.TimeSpan : TimeSpan.Zero;
    public bool HasCdg => _packetCount > 0;

    public CdgPreviewPlayer()
    {
        ResetPaletteAndCanvas();
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _renderTimer.Tick += OnRenderTimerTick;
        _mediaPlayer.MediaEnded += (_, _) =>
        {
            _isPlaying = false;
            _renderTimer.Stop();
            PlaybackStateChanged?.Invoke();
        };
    }

    private void ResetPaletteAndCanvas()
    {
        for (int i = 0; i < 16; i++)
        {
            _palette[i] = Colors.Black;
        }
        Array.Clear(_pixels, 0, _pixels.Length);
        _borderColorIndex = 0;
        _currentPacketIndex = 0;
    }

    public async Task LoadTrackAsync(string filePath, int keySemitones, double[] eqBandsDb, double volumePercent)
    {
        Stop();
        CleanupTempDir();

        _sourceFilePath = filePath;
        _cdgBytes = [];
        _packetCount = 0;
        ResetPaletteAndCanvas();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        _tempDir = Path.Combine(Path.GetTempPath(), "LyracistDbEditorPlayer_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        string? cdgPath = null;
        string? audioPath = null;

        await Task.Run(() =>
        {
            if (ext == ".zip")
            {
                using var archive = ZipFile.OpenRead(filePath);
                var cdgEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".cdg", StringComparison.OrdinalIgnoreCase));
                var audioEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

                if (audioEntry != null)
                {
                    audioPath = Path.Combine(_tempDir, "raw_audio" + Path.GetExtension(audioEntry.FullName));
                    audioEntry.ExtractToFile(audioPath, overwrite: true);
                }
                if (cdgEntry != null)
                {
                    cdgPath = Path.Combine(_tempDir, "track.cdg");
                    cdgEntry.ExtractToFile(cdgPath, overwrite: true);
                }
            }
            else if (ext == ".cdg")
            {
                cdgPath = filePath;
                string mp3Candidate = Path.ChangeExtension(filePath, ".mp3");
                string wavCandidate = Path.ChangeExtension(filePath, ".wav");
                if (File.Exists(mp3Candidate)) audioPath = mp3Candidate;
                else if (File.Exists(wavCandidate)) audioPath = wavCandidate;
            }
            else
            {
                audioPath = filePath;
                string companionCdg = Path.ChangeExtension(filePath, ".cdg");
                if (File.Exists(companionCdg))
                {
                    cdgPath = companionCdg;
                }
            }

            if (!string.IsNullOrEmpty(cdgPath) && File.Exists(cdgPath))
            {
                _cdgBytes = File.ReadAllBytes(cdgPath);
                _packetCount = _cdgBytes.Length / PacketSize;
            }
        });

        _rawAudioPath = audioPath;
        _activeAudioPath = audioPath;

        if (!string.IsNullOrEmpty(_rawAudioPath))
        {
            await ApplyDspAndOpenAsync(keySemitones, eqBandsDb, volumePercent, TimeSpan.Zero, autoPlay: true);
        }
    }

    public async Task ApplyDspAndOpenAsync(int keySemitones, double[] eqBandsDb, double volumePercent, TimeSpan resumePosition, bool autoPlay)
    {
        if (string.IsNullOrEmpty(_rawAudioPath) || !File.Exists(_rawAudioPath))
        {
            return;
        }

        _dspCts?.Cancel();
        var cts = new CancellationTokenSource();
        _dspCts = cts;

        bool hasEqOrKey = keySemitones != 0 || (eqBandsDb != null && eqBandsDb.Any(b => Math.Abs(b) >= 0.1));
        string targetPlayPath = _rawAudioPath;

        if (hasEqOrKey && !string.IsNullOrEmpty(_tempDir))
        {
            string? processed = await RenderDspAudioViaFfmpegAsync(_rawAudioPath, _tempDir, keySemitones, eqBandsDb ?? [], cts.Token);
            if (cts.IsCancellationRequested) return;
            if (!string.IsNullOrEmpty(processed) && File.Exists(processed))
            {
                targetPlayPath = processed;
            }
        }

        _activeAudioPath = targetPlayPath;
        _mediaPlayer.Open(new Uri(_activeAudioPath));
        _mediaPlayer.Volume = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);

        if (resumePosition > TimeSpan.Zero)
        {
            _mediaPlayer.Position = resumePosition;
        }

        if (_packetCount > 0)
        {
            UpdateCdgFrame(resumePosition);
        }

        if (autoPlay)
        {
            Play();
        }
    }

    private static async Task<string?> RenderDspAudioViaFfmpegAsync(
        string inputAudioPath,
        string tempDir,
        int keySemitones,
        double[] eqBandsDb,
        CancellationToken token)
    {
        try
        {
            string ffmpegExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegExe))
            {
                ffmpegExe = @"C:\ffmpeg\bin\ffmpeg.exe";
                if (!File.Exists(ffmpegExe)) ffmpegExe = "ffmpeg";
            }

            var filters = new List<string>();
            if (keySemitones != 0)
            {
                double pitchRatio = Math.Pow(2.0, keySemitones / 12.0);
                double tempoCompensation = 1.0 / pitchRatio;
                string pStr = pitchRatio.ToString("F5", CultureInfo.InvariantCulture);
                string tStr = Math.Clamp(tempoCompensation, 0.5, 2.0).ToString("F5", CultureInfo.InvariantCulture);
                filters.Add($"asetrate=44100*{pStr},aresample=44100,atempo={tStr}");
            }

            for (int i = 0; i < Math.Min(EqFrequenciesHz.Length, eqBandsDb.Length); i++)
            {
                double gain = Math.Clamp(eqBandsDb[i], -12.0, 12.0);
                if (Math.Abs(gain) >= 0.1)
                {
                    string gStr = gain.ToString("F1", CultureInfo.InvariantCulture);
                    filters.Add($"equalizer=f={EqFrequenciesHz[i]}:t=o:w=1:g={gStr}");
                }
            }

            if (filters.Count == 0) return inputAudioPath;

            string outFile = Path.Combine(tempDir, $"dsp_{Guid.NewGuid():N}.mp3");
            string filterArg = string.Join(",", filters);

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-y -i \"{inputAudioPath}\" -vn -af \"{filterArg}\" -b:a 192k \"{outFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;
            await proc.WaitForExitAsync(token);
            return proc.ExitCode == 0 && File.Exists(outFile) ? outFile : null;
        }
        catch
        {
            return null;
        }
    }

    public void SetVolume(double volumePercent)
    {
        _mediaPlayer.Volume = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
    }

    public void Play()
    {
        if (string.IsNullOrEmpty(_activeAudioPath)) return;
        _mediaPlayer.Play();
        _isPlaying = true;
        _renderTimer.Start();
        PlaybackStateChanged?.Invoke();
    }

    public void Pause()
    {
        _mediaPlayer.Pause();
        _isPlaying = false;
        _renderTimer.Stop();
        PlaybackStateChanged?.Invoke();
    }

    public void Stop()
    {
        _dspCts?.Cancel();
        _mediaPlayer.Stop();
        _isPlaying = false;
        _renderTimer.Stop();
        ResetPaletteAndCanvas();
        if (_packetCount > 0)
        {
            UpdateCdgFrame(TimeSpan.Zero);
        }
        PlaybackStateChanged?.Invoke();
    }

    public void Seek(TimeSpan position)
    {
        _mediaPlayer.Position = position;
        if (_packetCount > 0)
        {
            UpdateCdgFrame(position);
        }
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        if (_packetCount > 0)
        {
            UpdateCdgFrame(_mediaPlayer.Position);
        }
        PlaybackStateChanged?.Invoke();
    }

    private void UpdateCdgFrame(TimeSpan position)
    {
        int targetPacket = Math.Clamp((int)(position.TotalSeconds * PacketsPerSecond), 0, _packetCount);
        if (targetPacket < _currentPacketIndex)
        {
            ResetPaletteAndCanvas();
        }

        while (_currentPacketIndex < targetPacket)
        {
            ApplyCdgPacket(_currentPacketIndex * PacketSize);
            _currentPacketIndex++;
        }

        if (_bitmap == null)
        {
            _bitmap = new WriteableBitmap(CdgWidth, CdgHeight, 96, 96, PixelFormats.Bgra32, null);
        }

        for (int y = 0; y < CdgHeight; y++)
        {
            int rowOffset = y * CdgWidth * 4;
            for (int x = 0; x < CdgWidth; x++)
            {
                byte idx = (byte)(_pixels[x, y] & 0x0F);
                Color c = _palette[idx];
                int off = rowOffset + (x * 4);
                _bgraBuffer[off] = c.B;
                _bgraBuffer[off + 1] = c.G;
                _bgraBuffer[off + 2] = c.R;
                _bgraBuffer[off + 3] = 255;
            }
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, CdgWidth, CdgHeight), _bgraBuffer, CdgWidth * 4, 0);
        FrameUpdated?.Invoke(_bitmap);
    }

    private void ApplyCdgPacket(int offset)
    {
        if (offset + PacketSize > _cdgBytes.Length) return;
        byte cmd = (byte)(_cdgBytes[offset] & 0x3F);
        if (cmd != 0x09) return;

        byte instr = (byte)(_cdgBytes[offset + 1] & 0x3F);
        int dataStart = offset + 4;

        switch (instr)
        {
            case 1: // Memory Preset
                {
                    byte color = (byte)(_cdgBytes[dataStart] & 0x0F);
                    byte repeat = (byte)(_cdgBytes[dataStart + 1] & 0x0F);
                    if (repeat == 0)
                    {
                        for (int y = 0; y < CdgHeight; y++)
                        {
                            for (int x = 0; x < CdgWidth; x++)
                            {
                                _pixels[x, y] = color;
                            }
                        }
                    }
                    break;
                }
            case 2: // Border Preset
                {
                    byte color = (byte)(_cdgBytes[dataStart] & 0x0F);
                    _borderColorIndex = color;
                    for (int y = 0; y < CdgHeight; y++)
                    {
                        for (int x = 0; x < CdgWidth; x++)
                        {
                            if (x < 6 || x >= CdgWidth - 6 || y < 12 || y >= CdgHeight - 12)
                            {
                                _pixels[x, y] = _borderColorIndex;
                            }
                        }
                    }
                    break;
                }
            case 6:  // Tile Block Normal
            case 38: // Tile Block XOR
                {
                    bool isXor = instr == 38;
                    byte color0 = (byte)(_cdgBytes[dataStart] & 0x0F);
                    byte color1 = (byte)(_cdgBytes[dataStart + 1] & 0x0F);
                    int row = (_cdgBytes[dataStart + 2] & 0x1F) * 12;
                    int col = (_cdgBytes[dataStart + 3] & 0x3F) * 6;

                    if (row + 12 > CdgHeight || col + 6 > CdgWidth) break;

                    for (int r = 0; r < 12; r++)
                    {
                        byte rowBits = (byte)(_cdgBytes[dataStart + 4 + r] & 0x3F);
                        for (int c = 0; c < 6; c++)
                        {
                            byte bit = (byte)((rowBits >> (5 - c)) & 0x01);
                            byte pixColor = bit == 0 ? color0 : color1;
                            if (isXor)
                            {
                                _pixels[col + c, row + r] = (byte)((_pixels[col + c, row + r] ^ pixColor) & 0x0F);
                            }
                            else
                            {
                                _pixels[col + c, row + r] = pixColor;
                            }
                        }
                    }
                    break;
                }
            case 30: // Load Color Table Low (0..7)
            case 31: // Load Color Table High (8..15)
                {
                    int baseIdx = instr == 30 ? 0 : 8;
                    for (int i = 0; i < 8; i++)
                    {
                        byte high = (byte)(_cdgBytes[dataStart + (i * 2)] & 0x3F);
                        byte low = (byte)(_cdgBytes[dataStart + (i * 2) + 1] & 0x3F);

                        int r4 = (high >> 2) & 0x0F;
                        int g4 = ((high & 0x03) << 2) | ((low >> 4) & 0x03);
                        int b4 = low & 0x0F;

                        _palette[baseIdx + i] = Color.FromRgb((byte)(r4 * 17), (byte)(g4 * 17), (byte)(b4 * 17));
                    }
                    break;
                }
        }
    }

    private void CleanupTempDir()
    {
        try
        {
            if (!string.IsNullOrEmpty(_tempDir) && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
        _tempDir = null;
    }

    public void Dispose()
    {
        Stop();
        _mediaPlayer.Close();
        CleanupTempDir();
    }
}
