using System;
using System.Collections.Generic;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces;

public interface ILibraryService
{
    void ScanDirectory(string path);
    IEnumerable<KaraokeSong> Search(string query);
    IEnumerable<KaraokeSong> GetAllSongs();
    event EventHandler? LibraryUpdated;
}
