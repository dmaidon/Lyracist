// Created on Aug 6, 2026 @ 07:01:27 -> Split Special Occasion menu handling out of KaraokeViewModel.cs (God-object cleanup); pure code move, no behavior change
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Lyracist.Models;

namespace Lyracist.ViewModels;

public partial class KaraokeViewModel
{
    /// <summary>Nested Special Occasion menu (categories > subcategories > playable items).</summary>
    public ObservableCollection<OccasionNode> OccasionMenu { get; } = [];

    private void RebuildOccasionMenu()
    {
        OccasionMenu.Clear();
        foreach (var node in _occasions.GetMenuTree())
        {
            OccasionMenu.Add(node);
        }
    }

    [RelayCommand]
    private void PlayOccasion(object? parameter)
    {
        if (parameter is OccasionNode node)
        {
            // Category headers open their submenu; only playable items fire.
            if (!node.IsItem) return;
            _showFlow.PlayOccasion(node.Name, node.FilePath, node.Bass, node.Treble, node.Gain);
        }
    }

    [RelayCommand]
    private void StopOccasion()
    {
        _showFlow.StopOccasion();
    }
}
