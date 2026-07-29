// Edited on Jul 28, 2026 @ 19:04:00 -> Add isMusic parameter to Search and SearchAsync methods
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces;

public interface ILibraryService
{
    void ScanDirectory(string path);
    void RescanAllDirectories();
    void RemoveSongsUnderDirectory(string path);
    int GetSongCount();
    IEnumerable<KaraokeSong> Search(string query, bool isMusic = false);
    Task<IEnumerable<KaraokeSong>> SearchAsync(string query, bool isMusic = false);
    IEnumerable<KaraokeSong> GetAllSongs();
    IEnumerable<KaraokeSong> GetBackgroundMusicSongs();
    event EventHandler? LibraryUpdated;
    event EventHandler<Lyracist.Data.Services.ScanProgress>? ScanProgressChanged;
    event EventHandler<string>? ScanFailed;
    event EventHandler<Lyracist.Data.Services.ScanProgress>? MetadataProbeProgressChanged;
    event EventHandler? MetadataProbeCompleted;

    Lyracist.Data.Models.SongAudioSettings GetAudioSettings(string audioPath);
    void SaveAudioSettings(string audioPath, Lyracist.Data.Models.SongAudioSettings settings);

    Lyracist.Data.Models.SingerAudioSettings GetSingerSettings(string singerName);
    void SaveSingerSettings(string singerName, Lyracist.Data.Models.SingerAudioSettings settings);
}
