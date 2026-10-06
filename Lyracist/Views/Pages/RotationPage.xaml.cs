// Edited on Oct 6, 2026 @ 12:14:00 -> Add mouse drag-and-drop reordering with midpoint positioning and empty area drop
using System;
using System.Windows.Controls;
using System.Windows.Media;
using Lyracist.ViewModels;
using Point = System.Windows.Point;
using Vector = System.Windows.Vector;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using DependencyObject = System.Windows.DependencyObject;
using SystemParameters = System.Windows.SystemParameters;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Lyracist.Views.Pages;

public partial class RotationPage : Page
{
    private Point _dragStartPoint;
    private Lyracist.Models.Singer? _draggedItem;

    public RotationViewModel ViewModel { get; }

    public RotationPage(RotationViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // If the click is on an interactable control, don't initiate dragging
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
        {
            if (dep is System.Windows.Controls.Primitives.ButtonBase ||
                dep is CheckBox ||
                dep is TextBox)
            {
                _draggedItem = null;
                return;
            }
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (sender is ListBoxItem item && item.DataContext is Lyracist.Models.Singer singer)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItem = singer;
        }
    }

    private void ListBoxItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggedItem = null;
    }

    private void ListBoxItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
        {
            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is ListBoxItem item)
                {
                    var singer = _draggedItem;
                    DragDrop.DoDragDrop(item, singer, DragDropEffects.Move);
                    Lyracist.Shared.DragReorderVisuals.ClearIndicator();
                    _draggedItem = null;
                }
            }
        }
    }

    private void ListBoxItem_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(Lyracist.Models.Singer)))
        {
            if (sender is ListBoxItem hover)
            {
                Lyracist.Shared.DragReorderVisuals.ShowIndicator(hover, e.GetPosition(hover).Y >= hover.ActualHeight / 2.0);
                if (ItemsControl.ItemsControlFromItemContainer(hover) is ItemsControl owner)
                {
                    Lyracist.Shared.DragReorderVisuals.AutoScroll(owner, e);
                }
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void ListBoxItem_Drop(object sender, DragEventArgs e)
    {
        Lyracist.Shared.DragReorderVisuals.ClearIndicator();
        if (sender is ListBoxItem targetItem && e.Data.GetData(typeof(Lyracist.Models.Singer)) is Lyracist.Models.Singer sourceSinger)
        {
            if (targetItem.DataContext is Lyracist.Models.Singer targetSinger && sourceSinger != targetSinger)
            {
                var rotation = ViewModel.Rotation;
                int sourceIndex = rotation.IndexOf(sourceSinger);
                int targetIndex = rotation.IndexOf(targetSinger);

                if (sourceIndex >= 0 && targetIndex >= 0)
                {
                    Point pos = e.GetPosition(targetItem);
                    bool dropAfter = pos.Y >= (targetItem.ActualHeight / 2.0);

                    int insertIndex = Lyracist.Shared.RotationHelpers.ComputeDropIndex(rotation.Count, sourceIndex, targetIndex, dropAfter);
                    if (ViewModel.FloatCurrentSingerToTop && rotation.Count > 0 && rotation[0].IsCurrent)
                    {
                        if (sourceSinger != rotation[0] && insertIndex == 0)
                        {
                            insertIndex = 1;
                        }
                        else if (sourceSinger == rotation[0] && insertIndex > 0)
                        {
                            insertIndex = 0;
                        }
                    }

                    if (insertIndex != sourceIndex)
                    {
                        rotation.Move(sourceIndex, insertIndex);
                        ViewModel.SelectedSinger = sourceSinger;
                        ViewModel.NotifyRotationReordered();
                    }
                }
            }
        }
        _draggedItem = null;
        e.Handled = true;
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        if (sender is ItemsControl list)
        {
            Lyracist.Shared.DragReorderVisuals.AutoScroll(list, e);
        }

        if (e.Data.GetDataPresent(typeof(Lyracist.Models.Singer)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        Lyracist.Shared.DragReorderVisuals.ClearIndicator();
        if (e.Data.GetData(typeof(Lyracist.Models.Singer)) is Lyracist.Models.Singer sourceSinger)
        {
            var rotation = ViewModel.Rotation;
            int sourceIndex = rotation.IndexOf(sourceSinger);
            if (sourceIndex >= 0 && sourceIndex != rotation.Count - 1)
            {
                rotation.Move(sourceIndex, rotation.Count - 1);
                ViewModel.SelectedSinger = sourceSinger;
                ViewModel.NotifyRotationReordered();
            }
        }
        _draggedItem = null;
        e.Handled = true;
    }
}
