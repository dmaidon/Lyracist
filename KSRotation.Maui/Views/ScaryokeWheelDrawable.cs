// Created on Jul 27, 2026 @ 11:00:00 -> Draws the weighted category wheel via Microsoft.Maui.Graphics
// (GraphicsView). Each slice is built by sampling points along the arc directly (mirroring WPF's
// Polar() helper) rather than via FillArc/DrawArc, since FillArc's exact angle-direction/wraparound
// convention couldn't be verified without rendering it — sampling points ourselves removes that risk.
using KSRotation.Maui.ViewModels;
using Microsoft.Maui.Graphics;

namespace KSRotation.Maui.Views
{
    public class ScaryokeWheelDrawable : IDrawable
    {
        public IReadOnlyList<WheelSegment> Segments { get; set; } = [];

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Segments.Count == 0)
            {
                return;
            }

            float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            float radius = (size / 2f) - 4f;
            float centerX = dirtyRect.Width / 2f;
            float centerY = dirtyRect.Height / 2f;

            canvas.StrokeColor = Color.FromArgb("#1A1822");
            canvas.StrokeSize = 2;

            // Segment angles are tracked clockwise from the top (12 o'clock).
            double start = 0;
            foreach (var segment in Segments)
            {
                var path = new PathF();
                path.MoveTo(centerX, centerY);

                // Sample the arc edge every ~2 degrees so it reads as a smooth curve without relying
                // on any arc-drawing primitive's angle convention.
                int steps = Math.Max(2, (int)Math.Ceiling(segment.Sweep / 2.0));
                for (int i = 0; i <= steps; i++)
                {
                    double angle = start + (segment.Sweep * i / steps);
                    var point = PolarPoint(angle, radius, centerX, centerY);
                    path.LineTo(point.X, point.Y);
                }
                path.Close();

                canvas.FillColor = Color.FromArgb(segment.Color);
                canvas.FillPath(path);
                canvas.DrawPath(path);

                DrawLabel(canvas, segment, start, segment.Sweep, centerX, centerY, radius);

                start += segment.Sweep;
            }

            // Hub
            canvas.FillColor = Color.FromArgb("#1A1822");
            canvas.FillCircle(centerX, centerY, radius * 0.06f);
        }

        private static PointF PolarPoint(double angleDegrees, float radius, float centerX, float centerY)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            float x = centerX + (float)(radius * Math.Sin(rad));
            float y = centerY - (float)(radius * Math.Cos(rad));
            return new PointF(x, y);
        }

        private static void DrawLabel(ICanvas canvas, WheelSegment segment, double sliceStart, double sweep, float centerX, float centerY, float radius)
        {
            double mid = sliceStart + (sweep / 2);
            var labelPoint = PolarPoint(mid, radius * 0.62f, centerX, centerY);

            // Rotate the label to read outward along the slice, flipping upright on the bottom half so
            // it isn't upside-down relative to the room (the wheel spins, the room doesn't).
            double textAngle = mid - 90;
            if (mid > 180)
            {
                textAngle += 180;
            }

            bool isDjsChoice = string.Equals(segment.Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);

            canvas.SaveState();
            canvas.Translate(labelPoint.X, labelPoint.Y);
            canvas.Rotate((float)textAngle);
            canvas.FontColor = Color.FromArgb(segment.TextColor);
            canvas.FontSize = isDjsChoice ? 18 : 13;
            canvas.DrawString(isDjsChoice ? "💀" : segment.Name, -60, -10, 120, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.RestoreState();
        }
    }
}
