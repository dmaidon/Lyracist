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
            if (dep is System.Windows.Controls.Primitives.ButtonBase || dep is CheckBox)
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
                    DragDrop.DoDragDrop(item, _draggedItem, DragDropEffects.Move);
                }
            }
        }
    }

    private void ListBoxItem_DragOver(object sender, DragEventArgs e)
    {
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

    private void ListBoxItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem targetItem && e.Data.GetData(typeof(Lyracist.Models.Singer)) is Lyracist.Models.Singer sourceSinger)
        {
            if (targetItem.DataContext is Lyracist.Models.Singer targetSinger && sourceSinger != targetSinger)
            {
                var rotation = ViewModel.Rotation;
                int sourceIndex = rotation.IndexOf(sourceSinger);
                int targetIndex = rotation.IndexOf(targetSinger);

                if (sourceIndex >= 0 && targetIndex >= 0)
                {
                    rotation.RemoveAt(sourceIndex);
                    rotation.Insert(targetIndex, sourceSinger);
                    ViewModel.SelectedSinger = sourceSinger;
                    ViewModel.NotifyRotationReordered();
                }
            }
        }
        _draggedItem = null;
    }
}
