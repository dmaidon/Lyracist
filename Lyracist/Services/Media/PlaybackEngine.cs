// Created on Aug 20, 2026 @ 09:55:00 -> Add PlaybackEngine service wrapper for IMediaEngine and OnSongEnded event
using System;
using System.Threading.Tasks;
using Lyracist.Core.Interfaces;

namespace Lyracist.Services.Media;

/// <summary>
/// Service wrapper around the underlying IMediaEngine audio/video player, exposing
/// explicit song lifecycle events (e.g. OnSongEnded) for the AutoAdvanceManager.
/// </summary>
public class PlaybackEngine
{
    private readonly IMediaEngine _mediaEngine;

    /// <summary>
    /// Fires when a song finishes playing naturally.
    /// </summary>
    public event Action? OnSongEnded;

    public PlaybackEngine(IMediaEngine mediaEngine)
    {
        _mediaEngine = mediaEngine;
        _mediaEngine.SongEnded += () => OnSongEnded?.Invoke();
    }

    public Task LoadSong(string path) => _mediaEngine.LoadSong(path);
    public Task Play() => _mediaEngine.Play();
    public Task Pause() => _mediaEngine.Pause();
    public Task Stop() => _mediaEngine.Stop();
    public void Seek(double position) => _mediaEngine.Seek(position);
}
