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
    IEnumerable<KaraokeSong> Search(string query);
    Task<IEnumerable<KaraokeSong>> SearchAsync(string query);
    IEnumerable<KaraokeSong> GetAllSongs();
    IEnumerable<KaraokeSong> GetBackgroundMusicSongs();
    event EventHandler? LibraryUpdated;

    Lyracist.Data.Models.SongAudioSettings GetAudioSettings(string audioPath);
    void SaveAudioSettings(string audioPath, Lyracist.Data.Models.SongAudioSettings settings);

    Lyracist.Data.Models.SingerAudioSettings GetSingerSettings(string singerName);
    void SaveSingerSettings(string singerName, Lyracist.Data.Models.SingerAudioSettings settings);
}
