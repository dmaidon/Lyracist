// Last Edited: 2026-11-18 18:48 – New tests for VectorPhotoExporter (valid SVG, layers, hole handling)
using System.Xml.Linq;
using Lyracist.Shared;

namespace Lyracist.Tests;

public class VectorPhotoExporterTests
{
    private static byte[] Solid(int w, int h, Func<int, int, (byte R, byte G, byte B)> color)
    {
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var (r, g, b) = color(x, y);
                int i = (y * w + x) * 4;
                data[i] = b; data[i + 1] = g; data[i + 2] = r; data[i + 3] = 255;
            }
        return data;
    }

    [Fact]
    public void TraceToSvg_TwoColorImage_ProducesValidSvgWithPath()
    {
        // White canvas with a black 8x8 square that has a 2x2 white hole.
        var px = Solid(20, 20, (x, y) =>
            x is >= 6 and < 14 && y is >= 6 and < 14 && !(x is >= 9 and < 11 && y is >= 9 and < 11)
                ? ((byte)0, (byte)0, (byte)0)
                : ((byte)255, (byte)255, (byte)255));

        string svg = VectorPhotoExporter.TraceToSvg(px, 20, 20, 4);

        var doc = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Equal("svg", doc.Root!.Name.LocalName);
        Assert.Equal("0 0 20 20", doc.Root.Attribute("viewBox")!.Value);
        var path = Assert.Single(doc.Descendants(ns + "path"));
        Assert.Equal("evenodd", path.Attribute("fill-rule")!.Value);
        Assert.Equal(3, path.Attribute("d")!.Value.Count(c => c == 'M')); // canvas edge + square outline + inner island
    }

    [Fact]
    public void TraceToSvg_FlatImage_StillReturnsValidSvg()
    {
        var px = Solid(8, 8, (_, _) => ((byte)10, (byte)20, (byte)30));
        string svg = VectorPhotoExporter.TraceToSvg(px, 8, 8);
        Assert.NotNull(XDocument.Parse(svg).Root);
    }

    [Fact]
    public void TraceToSvg_BadBuffer_Throws()
    {
        Assert.Throws<ArgumentException>(() => VectorPhotoExporter.TraceToSvg(new byte[4], 10, 10));
    }
}
