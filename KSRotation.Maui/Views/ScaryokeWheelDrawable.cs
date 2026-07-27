// Created on Jul 27, 2026 @ 11:00:00 -> Draws the weighted category wheel via Microsoft.Maui.Graphics
// (GraphicsView), replacing WPF's manual PathGeometry/ArcSegment construction with FillArc/DrawArc.
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

            // Segment angles are tracked clockwise from the top (12 o'clock), matching how the wheel
            // is conceptually laid out. Microsoft.Maui.Graphics' arc angles are measured counter-
            // clockwise from the 3 o'clock position (standard math convention), so each slice is
            // converted below.
            double start = 0;
            foreach (var segment in Segments)
            {
                double end = start + segment.Sweep;

                float mathStart = (float)(90 - end);
                float mathEnd = (float)(90 - start);

                canvas.FillColor = Color.FromArgb(segment.Color);
                canvas.FillArc(centerX - radius, centerY - radius, radius * 2, radius * 2, mathStart, mathEnd, true);
                canvas.DrawArc(centerX - radius, centerY - radius, radius * 2, radius * 2, mathStart, mathEnd, true, false);

                DrawLabel(canvas, segment, start, segment.Sweep, centerX, centerY, radius);

                start = end;
            }

            // Hub
            canvas.FillColor = Color.FromArgb("#1A1822");
            canvas.FillCircle(centerX, centerY, radius * 0.06f);
        }

        private static void DrawLabel(ICanvas canvas, WheelSegment segment, double sliceStart, double sweep, float centerX, float centerY, float radius)
        {
            double mid = sliceStart + (sweep / 2);
            double labelRad = mid * Math.PI / 180.0;
            float labelRadius = radius * 0.62f;
            float labelX = centerX + (float)(labelRadius * Math.Sin(labelRad));
            float labelY = centerY - (float)(labelRadius * Math.Cos(labelRad));

            // Rotate the label to read outward along the slice, flipping upright on the bottom half so
            // it isn't upside-down relative to the room (the wheel spins, the room doesn't).
            double textAngle = mid - 90;
            if (mid > 180)
            {
                textAngle += 180;
            }

            bool isDjsChoice = string.Equals(segment.Name, "DJ's Choice", StringComparison.OrdinalIgnoreCase);

            canvas.SaveState();
            canvas.Translate(labelX, labelY);
            canvas.Rotate((float)textAngle);
            canvas.FontColor = Color.FromArgb(segment.TextColor);
            canvas.FontSize = isDjsChoice ? 18 : 13;
            canvas.DrawString(isDjsChoice ? "💀" : segment.Name, -60, -10, 120, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.RestoreState();
        }
    }
}
