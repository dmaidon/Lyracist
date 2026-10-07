// Edited on Sep 6, 2026 @ 10:33:00 -> Add NotifyLibraryUpdated method
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces;

public interface ILibraryService
{
    void ScanDirectory(string path);
    void RescanAllDirectories();

    /// <summary>Stops the running library scan/metadata probe and discards any queued scans (called on app exit).</summary>
    void CancelScan();

    void RemoveSongsUnderDirectory(string path);
    int GetSongCount();
    IEnumerable<KaraokeSong> Search(string query, bool isMusic = false);
    Task<IEnumerable<KaraokeSong>> SearchAsync(string query, bool isMusic = false);
    IEnumerable<KaraokeSong> GetAllSongs();
    IEnumerable<KaraokeSong> GetBackgroundMusicSongs();
    void NotifyLibraryUpdated();
    event EventHandler? LibraryUpdated;
    event EventHandler<Lyracist.Data.Services.ScanProgress>? ScanProgressChanged;
    event EventHandler<string>? ScanFailed;
    event EventHandler<Lyracist.Data.Services.ScanProgress>? MetadataProbeProgressChanged;
    event EventHandler? MetadataProbeCompleted;

    Lyracist.Data.Models.SongAudioSettings GetAudioSettings(string audioPath);
    void SaveAudioSettings(string audioPath, Lyracist.Data.Models.SongAudioSettings settings);

    Lyracist.Data.Models.SingerAudioSettings GetSingerSettings(string singerName);
    void SaveSingerSettings(string singerName, Lyracist.Data.Models.SingerAudioSettings settings);

    /// <summary>Previously-measured integrated loudness (LUFS) for a track, or null if it hasn't been measured yet.</summary>
    double? GetMeasuredLoudness(string audioPath);

    /// <summary>
    /// Measures a track's integrated loudness via ffmpeg and persists it, so future playback can
    /// read it back via <see cref="GetMeasuredLoudness"/>. Safe to call repeatedly for the same
    /// path - a measurement already in flight for that path is not duplicated.
    /// </summary>
    Task MeasureAndSaveLoudnessAsync(string audioPath);
}
