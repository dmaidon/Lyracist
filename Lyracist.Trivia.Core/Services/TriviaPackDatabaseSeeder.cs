// Edited on Aug 17, 2026 @ 13:20:30 -> TriviaPackDatabaseSeeder database synchronization utility
using System;
using System.IO;
using Lyracist.Trivia.Core.Models;

namespace Lyracist.Trivia.Core.Services;

public static class TriviaPackDatabaseSeeder
{
    public static void SyncAllPacksToDatabase(TriviaDatabaseService db, string? customDir = null)
    {
        string dir = customDir ?? TriviaStorageHelper.GetPacksDirectory();
        if (!Directory.Exists(dir)) return;

        var packs = TriviaPackManager.LoadAllPacks(dir);
        foreach (var pack in packs)
        {
            db.SeedPackIntoDatabase(pack);
        }
    }
}
