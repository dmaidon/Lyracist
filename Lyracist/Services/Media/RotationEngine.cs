// Created on Aug 20, 2026 @ 09:55:15 -> Add RotationEngine service wrapper around RotationViewModel for singer rotation queries and skipping
using System;
using Lyracist.Models;
using Lyracist.ViewModels;

namespace Lyracist.Services.Media;

/// <summary>
/// Service wrapper around singer rotation operations, providing GetNextSinger()
/// and SkipCurrentSinger() for the AutoAdvanceManager.
/// </summary>
public class RotationEngine
{
    private readonly RotationViewModel _rotationViewModel;

    public RotationEngine(RotationViewModel rotationViewModel)
    {
        _rotationViewModel = rotationViewModel;
    }

    /// <summary>
    /// Gets the current active, non-paused singer.
    /// </summary>
    public Singer? GetCurrentSinger() => _rotationViewModel.GetCurrentSinger();

    /// <summary>
    /// Gets the next singer in rotation order.
    /// </summary>
    public Singer? GetNextSinger() => _rotationViewModel.GetNextSinger();

    /// <summary>
    /// Skips the current singer and advances rotation order without completing the song.
    /// </summary>
    public void SkipCurrentSinger() => _rotationViewModel.SkipCurrentSinger();

    /// <summary>
    /// Marks the current singer as done and advances rotation.
    /// </summary>
    public void DoneSinger(Singer singer) => _rotationViewModel.DoneSingerCommand.Execute(singer);
}
