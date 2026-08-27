// Created on Aug 27, 2026 @ 14:37:40 -> WheelView code-behind with dynamic pie wedge geometry rendering
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KnockoutTrivia.Models;
using KnockoutTrivia.ViewModels;

namespace KnockoutTrivia.Views;

public partial class WheelView : UserControl
{
    public WheelView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RedrawWheel();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WheelViewModel oldVm)
        {
            oldVm.Segments.CollectionChanged -= OnSegmentsChanged;
        }

        if (e.NewValue is WheelViewModel newVm)
        {
            newVm.Segments.CollectionChanged += OnSegmentsChanged;
            RedrawWheel();
        }
    }

    private void OnSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawWheel();
    }

    private void RedrawWheel()
    {
        WheelCanvas.Children.Clear();

        if (DataContext is not WheelViewModel vm || vm.Segments.Count == 0)
        {
            return;
        }

        double radius = 240.0;
        Point center = new(radius, radius);

        int count = vm.Segments.Count;
        double sweep = 360.0 / count;

        for (int i = 0; i < count; i++)
        {
            var seg = vm.Segments[i];
            double startAngle = i * sweep;
            double endAngle = startAngle + sweep;

            // Draw Pie Wedge
            var path = CreateWedgePath(center, radius, startAngle, sweep, seg.SegmentBrush);
            WheelCanvas.Children.Add(path);

            // Add Text Label at Mid Angle
            double midAngle = startAngle + (sweep / 2.0);
            double rad = (midAngle - 90) * Math.PI / 180.0;
            double textDist = radius * 0.65;
            double textX = center.X + textDist * Math.Cos(rad);
            double textY = center.Y + textDist * Math.Sin(rad);

            var textBlock = new TextBlock
            {
                Text = seg.DisplayText,
                FontSize = count > 8 ? 11 : 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // Rotate text to follow slice direction
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new RotateTransform(midAngle + 90));
            textBlock.RenderTransform = transformGroup;

            Canvas.SetLeft(textBlock, textX - 60);
            Canvas.SetTop(textBlock, textY - 12);
            textBlock.Width = 120;

            WheelCanvas.Children.Add(textBlock);
        }
    }

    private static Path CreateWedgePath(Point center, double radius, double startAngle, double sweepAngle, Brush fill)
    {
        double startRad = (startAngle - 90) * Math.PI / 180.0;
        double endRad = ((startAngle + sweepAngle) - 90) * Math.PI / 180.0;

        Point p1 = new(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
        Point p2 = new(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

        bool isLargeArc = sweepAngle > 180.0;

        var figure = new PathFigure
        {
            StartPoint = center,
            IsClosed = true
        };
        figure.Segments.Add(new LineSegment(p1, true));
        figure.Segments.Add(new ArcSegment(p2, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path
        {
            Fill = fill,
            Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            StrokeThickness = 1.5,
            Data = geometry
        };
    }
}
