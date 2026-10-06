// Created on Oct 6, 2026 @ 13:00:00 -> Shared WPF drag-reorder helpers: edge auto-scroll and insertion-line indicator
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Lyracist.Shared;

/// <summary>
/// Visual helpers for list drag-and-drop reordering: auto-scrolls the list when the cursor nears
/// its top/bottom edge, and draws a thin insertion line on the hovered row.
/// </summary>
public static class DragReorderVisuals
{
    private const double EdgeZone = 32.0;
    private static InsertionAdorner? _adorner;
    private static AdornerLayer? _layer;

    /// <summary>Scrolls the list one line when the drag cursor is within the edge zone.</summary>
    public static void AutoScroll(ItemsControl list, System.Windows.DragEventArgs e)
    {
        var scroller = FindScrollViewer(list);
        if (scroller == null) return;

        double y = e.GetPosition(list).Y;
        if (y < EdgeZone) scroller.LineUp();
        else if (y > list.ActualHeight - EdgeZone) scroller.LineDown();
    }

    /// <summary>Shows the insertion line above or below <paramref name="row"/>, replacing any previous one.</summary>
    public static void ShowIndicator(FrameworkElement row, bool after)
    {
        if (_adorner != null && _adorner.AdornedElement == row && _adorner.After == after) return;
        ClearIndicator();

        var layer = AdornerLayer.GetAdornerLayer(row);
        if (layer == null) return;

        _layer = layer;
        _adorner = new InsertionAdorner(row, after);
        layer.Add(_adorner);
    }

    /// <summary>Removes the insertion line if one is showing. Safe to call repeatedly.</summary>
    public static void ClearIndicator()
    {
        if (_adorner != null)
        {
            _layer?.Remove(_adorner);
        }
        _adorner = null;
        _layer = null;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv) return sv;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    private sealed class InsertionAdorner : Adorner
    {
        private readonly System.Windows.Media.Pen _pen;
        public bool After { get; }

        public InsertionAdorner(UIElement element, bool after) : base(element)
        {
            After = after;
            IsHitTestVisible = false;
            var brush = System.Windows.SystemColors.HighlightBrush;
            _pen = new System.Windows.Media.Pen(brush, 3);
            _pen.Freeze();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double y = After ? AdornedElement.RenderSize.Height - 1 : 1;
            dc.DrawLine(_pen, new System.Windows.Point(0, y), new System.Windows.Point(AdornedElement.RenderSize.Width, y));
        }
    }
}
