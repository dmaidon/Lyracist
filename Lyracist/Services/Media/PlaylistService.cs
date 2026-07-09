using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Lyracist.Core.Interfaces;
using Lyracist.Data;
using Lyracist.Data.Models;
using Lyracist.Models;

namespace Lyracist.Services.Media;

public class PlaylistService : IPlaylistService
{
    public List<PlaylistTrack> GetOpeningPlaylist()
    {
        using var context = new LyracistDbContext();
        var items = context.OpeningPlaylistItems
            .Include(i => i.Song)
            .OrderBy(i => i.Order)
            .ToList();
        return [.. items.Select(i => Map(i.OpeningPlaylistItemId, i.SongId, i.Order, i.Song))];
    }

    public List<PlaylistTrack> GetFillInPlaylist()
    {
        using var context = new LyracistDbContext();
        var items = context.FillInPlaylistItems
            .Include(i => i.Song)
            .OrderBy(i => i.Order)
            .ToList();
        return [.. items.Select(i => Map(i.FillInPlaylistItemId, i.SongId, i.Order, i.Song))];
    }

    public List<PlaylistTrack> GetEndRotationPlaylist()
    {
        using var context = new LyracistDbContext();
        var items = context.EndRotationPlaylistItems
            .Include(i => i.Song)
            .OrderBy(i => i.Order)
            .ToList();
        return [.. items.Select(i => Map(i.EndRotationPlaylistItemId, i.SongId, i.Order, i.Song))];
    }

    public void AddSongToOpening(int songId)
    {
        using var context = new LyracistDbContext();
        int nextOrder = context.OpeningPlaylistItems.Any() ? context.OpeningPlaylistItems.Max(i => i.Order) + 1 : 0;
        context.OpeningPlaylistItems.Add(new OpeningPlaylistItem { SongId = songId, Order = nextOrder });
        context.SaveChanges();
    }

    public void AddSongToFillIn(int songId)
    {
        using var context = new LyracistDbContext();
        int nextOrder = context.FillInPlaylistItems.Any() ? context.FillInPlaylistItems.Max(i => i.Order) + 1 : 0;
        context.FillInPlaylistItems.Add(new FillInPlaylistItem { SongId = songId, Order = nextOrder });
        context.SaveChanges();
    }

    public void AddSongToEndRotation(int songId)
    {
        using var context = new LyracistDbContext();
        int nextOrder = context.EndRotationPlaylistItems.Any() ? context.EndRotationPlaylistItems.Max(i => i.Order) + 1 : 0;
        context.EndRotationPlaylistItems.Add(new EndRotationPlaylistItem { SongId = songId, Order = nextOrder });
        context.SaveChanges();
    }

    public void RemoveFromOpening(int itemId)
    {
        using var context = new LyracistDbContext();
        var item = context.OpeningPlaylistItems.Find(itemId);
        if (item == null) return;

        context.OpeningPlaylistItems.Remove(item);
        context.SaveChanges();
        Renumber(context.OpeningPlaylistItems.OrderBy(i => i.Order).ToList(), context);
    }

    public void RemoveFromFillIn(int itemId)
    {
        using var context = new LyracistDbContext();
        var item = context.FillInPlaylistItems.Find(itemId);
        if (item == null) return;

        context.FillInPlaylistItems.Remove(item);
        context.SaveChanges();
        Renumber(context.FillInPlaylistItems.OrderBy(i => i.Order).ToList(), context);
    }

    public void RemoveFromEndRotation(int itemId)
    {
        using var context = new LyracistDbContext();
        var item = context.EndRotationPlaylistItems.Find(itemId);
        if (item == null) return;

        context.EndRotationPlaylistItems.Remove(item);
        context.SaveChanges();
        Renumber(context.EndRotationPlaylistItems.OrderBy(i => i.Order).ToList(), context);
    }

    public void MoveOpeningItem(int itemId, int direction)
    {
        using var context = new LyracistDbContext();
        var items = context.OpeningPlaylistItems.OrderBy(i => i.Order).ToList();
        Swap(items, itemId, direction, i => i.OpeningPlaylistItemId, (i, o) => i.Order = o);
        context.SaveChanges();
    }

    public void MoveFillInItem(int itemId, int direction)
    {
        using var context = new LyracistDbContext();
        var items = context.FillInPlaylistItems.OrderBy(i => i.Order).ToList();
        Swap(items, itemId, direction, i => i.FillInPlaylistItemId, (i, o) => i.Order = o);
        context.SaveChanges();
    }

    public void MoveEndRotationItem(int itemId, int direction)
    {
        using var context = new LyracistDbContext();
        var items = context.EndRotationPlaylistItems.OrderBy(i => i.Order).ToList();
        Swap(items, itemId, direction, i => i.EndRotationPlaylistItemId, (i, o) => i.Order = o);
        context.SaveChanges();
    }

    private static void Swap<T>(List<T> items, int itemId, int direction, System.Func<T, int> getId, System.Action<T, int> setOrder)
    {
        int index = items.FindIndex(i => getId(i).Equals(itemId));
        int targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= items.Count) return;

        (items[index], items[targetIndex]) = (items[targetIndex], items[index]);
        for (int i = 0; i < items.Count; i++)
        {
            setOrder(items[i], i);
        }
    }

    private static void Renumber(List<OpeningPlaylistItem> items, LyracistDbContext context)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
        context.SaveChanges();
    }

    private static void Renumber(List<FillInPlaylistItem> items, LyracistDbContext context)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
        context.SaveChanges();
    }

    private static void Renumber(List<EndRotationPlaylistItem> items, LyracistDbContext context)
    {
        for (int i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
        }
        context.SaveChanges();
    }

    private static PlaylistTrack Map(int itemId, int songId, int order, Song? song)
    {
        return new PlaylistTrack
        {
            ItemId = itemId,
            SongId = songId,
            Order = order,
            Title = song?.Title ?? "(missing song)",
            Artist = song?.Artist ?? string.Empty,
            AudioPath = song?.FilePath ?? string.Empty
        };
    }
}
