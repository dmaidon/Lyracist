// Last Edited: 2026-11-18 18:48 – New VectorPhotoExporter: traces a photo into a layered, smoothed SVG (posterized vector)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lyracist.Shared;

/// <summary>
/// Converts a raster photo into a true vector SVG by posterizing it to a small palette and tracing the
/// outline of each color layer. Layers are nested (each layer covers every pixel at or above its luminance rank),
/// so the result has no seams between colors. Result is a stylized "vector portrait", not a pixel-exact copy.
/// </summary>
public static class VectorPhotoExporter
{
    /// <summary>Longest side, in pixels, the photo is reduced to before tracing. Higher = more detail and bigger files.</summary>
    public const int DefaultMaxDimension = 256;

    /// <summary>Default number of palette colors (layers).</summary>
    public const int DefaultColorCount = 16;

    /// <summary>
    /// Loads <paramref name="source"/>, traces it, and writes an SVG file to <paramref name="svgPath"/>.
    /// </summary>
    public static void ExportToSvgFile(BitmapSource source, string svgPath,
        int colorCount = DefaultColorCount, int maxDimension = DefaultMaxDimension)
    {
        ArgumentNullException.ThrowIfNull(source);
        File.WriteAllText(svgPath, ExportToSvg(source, colorCount, maxDimension), new UTF8Encoding(false));
    }

    /// <summary>Converts a WPF bitmap to SVG markup.</summary>
    public static string ExportToSvg(BitmapSource source, int colorCount = DefaultColorCount,
        int maxDimension = DefaultMaxDimension)
    {
        ArgumentNullException.ThrowIfNull(source);

        BitmapSource working = source;
        int longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longest > maxDimension)
        {
            double scale = (double)maxDimension / longest;
            working = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }

        var converted = new FormatConvertedBitmap(working, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        var bgra = new byte[w * h * 4];
        converted.CopyPixels(bgra, w * 4, 0);

        return TraceToSvg(bgra, w, h, colorCount);
    }

    /// <summary>
    /// Core tracer. <paramref name="bgra"/> is tightly packed 32-bit BGRA (stride = width * 4).
    /// Transparent pixels are blended onto white.
    /// </summary>
    public static string TraceToSvg(byte[] bgra, int width, int height, int colorCount = DefaultColorCount)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            throw new ArgumentException("Pixel buffer does not match the given dimensions.");
        colorCount = Math.Clamp(colorCount, 2, 64);

        int count = width * height;
        var rgb = new int[count];
        for (int i = 0; i < count; i++)
        {
            int a = bgra[i * 4 + 3];
            int b = Blend(bgra[i * 4], a);
            int g = Blend(bgra[i * 4 + 1], a);
            int r = Blend(bgra[i * 4 + 2], a);
            rgb[i] = (r << 16) | (g << 8) | b;
        }

        // Palette, ordered dark -> light so layer k = "all pixels with rank >= k".
        var palette = BuildPalette(rgb, colorCount);
        palette.Sort((x, y) => Luma(x).CompareTo(Luma(y)));

        // Map every pixel to its nearest palette entry (rank).
        var rank = new int[count];
        var cache = new Dictionary<int, int>();
        for (int i = 0; i < count; i++)
        {
            if (!cache.TryGetValue(rgb[i], out int idx))
            {
                idx = Nearest(palette, rgb[i]);
                cache[rgb[i]] = idx;
            }
            rank[i] = idx;
        }

        var sb = new StringBuilder(64 * 1024);
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
          .Append(width).Append(' ').Append(height)
          .Append("\" width=\"").Append(width * 4).Append("\" height=\"").Append(height * 4)
          .Append("\" shape-rendering=\"geometricPrecision\">\n");
        sb.Append("<rect width=\"").Append(width).Append("\" height=\"").Append(height)
          .Append("\" fill=\"").Append(Hex(palette[0])).Append("\"/>\n");

        var mask = new bool[count];
        for (int layer = 1; layer < palette.Count; layer++)
        {
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                mask[i] = rank[i] >= layer;
                any |= mask[i];
            }
            if (!any) continue;

            var loops = TraceLoops(mask, width, height);
            if (loops.Count == 0) continue;

            sb.Append("<path fill=\"").Append(Hex(palette[layer])).Append("\" fill-rule=\"evenodd\" d=\"");
            foreach (var loop in loops)
            {
                var smooth = Smooth(Smooth(loop));
                for (int p = 0; p < smooth.Count; p++)
                {
                    sb.Append(p == 0 ? 'M' : 'L')
                      .Append(smooth[p].X.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                      .Append(smooth[p].Y.ToString("0.##", CultureInfo.InvariantCulture));
                }
                sb.Append('Z');
            }
            sb.Append("\"/>\n");
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    // ───────────────────────── Palette (median cut) ─────────────────────────

    private static List<int> BuildPalette(int[] rgb, int maxColors)
    {
        var boxes = new List<int[]> { (int[])rgb.Clone() };

        while (boxes.Count < maxColors)
        {
            // Pick the biggest box that still has color variation.
            int best = -1;
            for (int i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].Length < 2) continue;
                if (best < 0 || boxes[i].Length > boxes[best].Length)
                {
                    if (HasRange(boxes[i])) best = i;
                }
            }
            if (best < 0) break;

            var box = boxes[best];
            int shift = WidestChannelShift(box);
            Array.Sort(box, (x, y) => ((x >> shift) & 255).CompareTo((y >> shift) & 255));
            int mid = box.Length / 2;
            boxes[best] = box[..mid];
            boxes.Add(box[mid..]);
        }

        var palette = new List<int>(boxes.Count);
        foreach (var box in boxes)
        {
            long r = 0, g = 0, b = 0;
            foreach (int c in box)
            {
                r += (c >> 16) & 255;
                g += (c >> 8) & 255;
                b += c & 255;
            }
            int n = Math.Max(1, box.Length);
            int avg = ((int)(r / n) << 16) | ((int)(g / n) << 8) | (int)(b / n);
            if (!palette.Contains(avg)) palette.Add(avg);
        }
        if (palette.Count == 1) palette.Add(palette[0] ^ 0x010101); // keep at least two layers
        return palette;
    }

    private static bool HasRange(int[] box)
    {
        int first = box[0];
        foreach (int c in box) if (c != first) return true;
        return false;
    }

    private static int WidestChannelShift(int[] box)
    {
        int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
        foreach (int c in box)
        {
            int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            if (r < minR) minR = r; if (r > maxR) maxR = r;
            if (g < minG) minG = g; if (g > maxG) maxG = g;
            if (b < minB) minB = b; if (b > maxB) maxB = b;
        }
        int rr = maxR - minR, gr = maxG - minG, br = maxB - minB;
        if (rr >= gr && rr >= br) return 16;
        return gr >= br ? 8 : 0;
    }

    private static int Nearest(List<int> palette, int color)
    {
        int best = 0;
        long bestDist = long.MaxValue;
        int r = (color >> 16) & 255, g = (color >> 8) & 255, b = color & 255;
        for (int i = 0; i < palette.Count; i++)
        {
            int p = palette[i];
            long dr = ((p >> 16) & 255) - r, dg = ((p >> 8) & 255) - g, db = (p & 255) - b;
            long d = dr * dr + dg * dg + db * db;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private static double Luma(int c) =>
        0.299 * ((c >> 16) & 255) + 0.587 * ((c >> 8) & 255) + 0.114 * (c & 255);

    private static int Blend(byte channel, int alpha) => (channel * alpha + 255 * (255 - alpha)) / 255;

    private static string Hex(int c) => "#" + c.ToString("x6", CultureInfo.InvariantCulture);

    // ───────────────────────── Contour tracing ─────────────────────────

    /// <summary>
    /// Traces the pixel-edge outlines of a binary mask. Outer boundaries run clockwise and holes counter-clockwise
    /// (screen coordinates), so an even-odd fill reproduces the mask. Collinear points are merged.
    /// </summary>
    private static List<List<(double X, double Y)>> TraceLoops(bool[] mask, int w, int h)
    {
        int stride = w + 1;
        int vertexCount = stride * (h + 1);
        var next0 = new int[vertexCount];
        var next1 = new int[vertexCount];
        Array.Fill(next0, -1);
        Array.Fill(next1, -1);

        void AddEdge(int x0, int y0, int x1, int y1)
        {
            int from = y0 * stride + x0;
            int to = y1 * stride + x1;
            if (next0[from] < 0) next0[from] = to; else next1[from] = to;
        }

        bool Filled(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && mask[y * w + x];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[y * w + x]) continue;
                if (!Filled(x, y - 1)) AddEdge(x, y, x + 1, y);               // top
                if (!Filled(x + 1, y)) AddEdge(x + 1, y, x + 1, y + 1);       // right
                if (!Filled(x, y + 1)) AddEdge(x + 1, y + 1, x, y + 1);       // bottom
                if (!Filled(x - 1, y)) AddEdge(x, y + 1, x, y);               // left
            }
        }

        var loops = new List<List<(double X, double Y)>>();
        for (int start = 0; start < vertexCount; start++)
        {
            while (next0[start] >= 0)
            {
                var raw = new List<int>();
                int cur = start;
                do
                {
                    raw.Add(cur);
                    int nxt;
                    if (next0[cur] >= 0) { nxt = next0[cur]; next0[cur] = next1[cur]; next1[cur] = -1; }
                    else { nxt = next1[cur]; next1[cur] = -1; }
                    cur = nxt;
                } while (cur != start && cur >= 0);

                if (cur < 0 || raw.Count < 4) continue;
                var corners = KeepCorners(raw, stride);
                if (corners.Count >= 3 && Math.Abs(Area(corners)) >= 1.5)
                    loops.Add(corners);
            }
        }
        return loops;
    }

    private static List<(double X, double Y)> KeepCorners(List<int> vertices, int stride)
    {
        int n = vertices.Count;
        var result = new List<(double X, double Y)>();
        for (int i = 0; i < n; i++)
        {
            int prev = vertices[(i + n - 1) % n], cur = vertices[i], nxt = vertices[(i + 1) % n];
            int dx1 = cur % stride - prev % stride, dy1 = cur / stride - prev / stride;
            int dx2 = nxt % stride - cur % stride, dy2 = nxt / stride - cur / stride;
            if (dx1 != dx2 || dy1 != dy2) result.Add((cur % stride, cur / stride));
        }
        return result;
    }

    private static double Area(List<(double X, double Y)> pts)
    {
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    /// <summary>One round of Chaikin corner cutting on a closed polygon.</summary>
    private static List<(double X, double Y)> Smooth(List<(double X, double Y)> pts)
    {
        var result = new List<(double X, double Y)>(pts.Count * 2);
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            result.Add((0.75 * p.X + 0.25 * q.X, 0.75 * p.Y + 0.25 * q.Y));
            result.Add((0.25 * p.X + 0.75 * q.X, 0.25 * p.Y + 0.75 * q.Y));
        }
        return result;
    }
}
