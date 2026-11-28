// Created on Nov 27, 2026 @ 13:21:00 -> Interaction logic for KaraokeStudioWindow: audio playback, F8 timestamp stamping, and CDG/MP4 track export
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Lyracist.Core.Helpers;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Data.Services;
using Lyracist.Services.Media;
using Lyracist.Shared;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Lyracist.Windows;

public partial class KaraokeStudioWindow : Window
{
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _clockTimer;
    private bool _isPlaying;
    private bool _suppressSeekCallback;
    private CancellationTokenSource? _exportCts;

    public ObservableCollection<StudioLyricLine> LyricLines { get; } = [];

    public KaraokeStudioWindow()
    {
        InitializeComponent();

        if (GridLyrics.Columns[4] is DataGridComboBoxColumn singerCol)
        {
            singerCol.ItemsSource = new[] { "Singer 1", "Singer 2", "Both (Duet)" };
        }

        GridLyrics.ItemsSource = LyricLines;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _clockTimer.Tick += OnClockTimerTick;
        _player.MediaEnded += (_, _) =>
        {
            _isPlaying = false;
            _clockTimer.Stop();
            BtnPlayPause.Content = "▶ Play Audio";
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _exportCts?.Cancel(); } catch { }
        _clockTimer.Stop();
        _player.Stop();
        _player.Close();
        base.OnClosed(e);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F8)
        {
            OnStampNextLineClicked(sender, e);
            e.Handled = true;
        }
    }

    private void OnBrowseAudioClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Audio or Video File to Turn into Karaoke",
            Filter = "Audio & Video Files (*.mp3;*.wav;*.flac;*.m4a;*.mp4;*.ogg)|*.mp3;*.wav;*.flac;*.m4a;*.mp4;*.ogg|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        TbInputAudioPath.Text = dlg.FileName;
        _player.Open(new Uri(dlg.FileName));

        string baseName = Path.GetFileNameWithoutExtension(dlg.FileName);
        int dashIdx = baseName.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0)
        {
            TbArtistName.Text = NameFormatting.ProperCase(baseName[..dashIdx].Trim());
            TbSongTitle.Text = NameFormatting.ProperCase(baseName[(dashIdx + 3)..].Trim());
        }
        else
        {
            TbSongTitle.Text = NameFormatting.ProperCase(baseName);
            if (string.IsNullOrWhiteSpace(TbArtistName.Text))
            {
                TbArtistName.Text = "Custom Artist";
            }
        }

        try
        {
            using var tagFile = TagLib.File.Create(dlg.FileName);
            if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title)) TbSongTitle.Text = tagFile.Tag.Title;
            if (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstPerformer)) TbArtistName.Text = tagFile.Tag.FirstPerformer;
        }
        catch { }

        TxtStatus.Text = $"Loaded '{Path.GetFileName(dlg.FileName)}'. Paste or import lyrics, then play & stamp timestamps (F8).";
    }

    private void SliderKeyShift_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblKeyShift != null)
        {
            int st = (int)Math.Round(e.NewValue);
            LblKeyShift.Text = $"Key Shift: {st:+0;-0;0} semitones";
        }
    }

    private void SliderTempo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblTempo != null)
        {
            int pct = (int)Math.Round(e.NewValue);
            LblTempo.Text = $"Tempo: {pct}%";
        }
    }

    private void OnParseLyricsClicked(object sender, RoutedEventArgs e)
    {
        string raw = TbRawLyrics.Text ?? string.Empty;
        var lines = raw.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            MessageBox.Show("Paste one or more lyric lines into the box first.", "No Lyrics", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LyricLines.Clear();
        double cursorSec = 4.0;
        foreach (string line in lines)
        {
            double start = cursorSec;
            string text = line;

            // If line already starts with an LRC timestamp [mm:ss.ff], preserve it
            if (text.StartsWith('[') && text.IndexOf(']') is int closeIdx and > 1)
            {
                string tag = text[1..closeIdx];
                if (StudioLyricLine.TryParseTimestamp(tag, out double parsedSec))
                {
                    start = parsedSec;
                    text = text[(closeIdx + 1)..].Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(text)) continue;

            LyricLines.Add(new StudioLyricLine
            {
                StartTimeSeconds = start,
                EndTimeSeconds = start + 3.5,
                LyricText = text,
                SingerAssignment = "Singer 1"
            });
            cursorSec = start + 4.0;
        }

        if (LyricLines.Count > 0)
        {
            GridLyrics.SelectedIndex = 0;
        }
        TxtStatus.Text = $"Loaded {LyricLines.Count} lyric line(s) into the sync grid.";
    }

    private void OnImportLyricsFileClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import LRC or Text Lyrics File",
            Filter = "Lyrics Files (*.lrc;*.txt)|*.lrc;*.txt|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        TbRawLyrics.Text = File.ReadAllText(dlg.FileName);
        OnParseLyricsClicked(sender, e);
    }

    private void OnPlayPauseAudioClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TbInputAudioPath.Text) || !File.Exists(TbInputAudioPath.Text))
        {
            MessageBox.Show("Select an audio track first.", "Audio Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isPlaying)
        {
            _player.Pause();
            _isPlaying = false;
            _clockTimer.Stop();
            BtnPlayPause.Content = "▶ Play Audio";
        }
        else
        {
            _player.Play();
            _isPlaying = true;
            _clockTimer.Start();
            BtnPlayPause.Content = "⏸ Pause";
        }
    }

    private void OnStopAudioClicked(object sender, RoutedEventArgs e)
    {
        _player.Stop();
        _isPlaying = false;
        _clockTimer.Stop();
        BtnPlayPause.Content = "▶ Play Audio";
        TxtPlaybackClock.Text = "00:00.00";
        _suppressSeekCallback = true;
        SliderAudioSeek.Value = 0;
        _suppressSeekCallback = false;
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        TimeSpan pos = _player.Position;
        TxtPlaybackClock.Text = pos.ToString(@"mm\:ss\.ff");

        if (_player.NaturalDuration.HasTimeSpan)
        {
            double dur = _player.NaturalDuration.TimeSpan.TotalSeconds;
            if (dur > 0.5)
            {
                _suppressSeekCallback = true;
                SliderAudioSeek.Maximum = dur;
                SliderAudioSeek.Value = pos.TotalSeconds;
                _suppressSeekCallback = false;
            }
        }
    }

    private void SliderAudioSeek_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSeekCallback) return;
        _player.Position = TimeSpan.FromSeconds(Math.Max(0, e.NewValue));
        TxtPlaybackClock.Text = _player.Position.ToString(@"mm\:ss\.ff");
    }

    private void OnStampNextLineClicked(object sender, RoutedEventArgs e)
    {
        if (LyricLines.Count == 0) return;

        int idx = GridLyrics.SelectedIndex;
        if (idx < 0 || idx >= LyricLines.Count) idx = 0;

        double nowSec = _player.Position.TotalSeconds;
        var current = LyricLines[idx];
        current.StartTimeSeconds = nowSec;
        if (current.EndTimeSeconds <= nowSec)
        {
            current.EndTimeSeconds = nowSec + 3.5;
        }

        if (idx > 0 && LyricLines[idx - 1].EndTimeSeconds > nowSec)
        {
            LyricLines[idx - 1].EndTimeSeconds = Math.Max(LyricLines[idx - 1].StartTimeSeconds + 0.5, nowSec - 0.1);
        }

        if (idx + 1 < LyricLines.Count)
        {
            GridLyrics.SelectedIndex = idx + 1;
            GridLyrics.ScrollIntoView(LyricLines[idx + 1]);
        }
    }

    private async void OnExportClicked(object sender, RoutedEventArgs e)
    {
        string inputAudio = TbInputAudioPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(inputAudio) || !File.Exists(inputAudio))
        {
            MessageBox.Show("Please select an input audio track first.", "Audio Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (LyricLines.Count == 0 && !string.IsNullOrWhiteSpace(TbRawLyrics.Text))
        {
            OnParseLyricsClicked(sender, e);
        }

        string outDir = AppSettings.LibraryDirectories.FirstOrDefault(Directory.Exists) ?? Globals.OriginalsDir;
        Directory.CreateDirectory(outDir);

        var options = new KaraokeStudioExportOptions
        {
            InputAudioPath = inputAudio,
            OutputDirectory = outDir,
            SongTitle = string.IsNullOrWhiteSpace(TbSongTitle.Text) ? "Custom Karaoke Track" : TbSongTitle.Text.Trim(),
            ArtistName = string.IsNullOrWhiteSpace(TbArtistName.Text) ? "Custom Artist" : TbArtistName.Text.Trim(),
            VocalIsolationMode = Math.Max(0, CbVocalMode.SelectedIndex),
            KeySemitones = (int)Math.Round(SliderKeyShift.Value),
            TempoPercent = (int)Math.Round(SliderTempo.Value),
            ExportFormatIndex = Math.Max(0, CbExportFormat.SelectedIndex),
            IncludeInstrumentalCountdowns = ChkCountdowns.IsChecked == true,
            Lines = LyricLines.ToList()
        };

        BtnExport.IsEnabled = false;
        _exportCts = new CancellationTokenSource();
        var progress = new Progress<string>(msg => TxtStatus.Text = msg);

        try
        {
            string exportedPath = await KaraokeStudioExporter.ExportCustomKaraokeTrackAsync(options, progress, _exportCts.Token);

            if (ChkAutoImportToLibrary.IsChecked == true && File.Exists(exportedPath))
            {
                using var context = new LyracistDbContext();
                var existing = context.Songs.FirstOrDefault(s => s.FilePath == exportedPath);
                if (existing == null)
                {
                    var dbSong = new Song
                    {
                        Title = options.SongTitle,
                        Artist = options.ArtistName,
                        FilePath = exportedPath,
                        IsKaraoke = true,
                        KaraokeType = options.ExportFormatIndex == 0 ? "MP3G" : "MP4",
                        Genre = "Custom Studio",
                        NeedsRepair = false
                    };
                    context.Songs.Add(dbSong);
                    await context.SaveChangesAsync();
                    await new SearchService(context).IndexSong(dbSong);
                }
            }

            MessageBox.Show(
                $"Custom karaoke track created successfully!\n\nSaved to:\n{exportedPath}",
                "Karaoke Studio Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Export failed: {ex.Message}";
            MessageBox.Show($"Failed to export custom karaoke track:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnExport.IsEnabled = true;
            _exportCts?.Dispose();
            _exportCts = null;
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
