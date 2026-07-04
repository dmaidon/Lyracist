using System.Collections.Generic;
using Lyracist.Models;

namespace Lyracist.Core.Interfaces;

public interface IPlaylistService
{
    List<PlaylistTrack> GetOpeningPlaylist();
    List<PlaylistTrack> GetFillInPlaylist();
    List<PlaylistTrack> GetEndRotationPlaylist();

    void AddSongToOpening(int songId);
    void AddSongToFillIn(int songId);
    void AddSongToEndRotation(int songId);

    void RemoveFromOpening(int itemId);
    void RemoveFromFillIn(int itemId);
    void RemoveFromEndRotation(int itemId);

    /// <param name="direction">-1 to move up, +1 to move down.</param>
    void MoveOpeningItem(int itemId, int direction);
    void MoveFillInItem(int itemId, int direction);
    void MoveEndRotationItem(int itemId, int direction);
}
