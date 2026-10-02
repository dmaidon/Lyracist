// Edited on Oct 2, 2026 @ 13:40:00 -> Add no-black-pixel tests for extreme pan/zoom and transparent sources
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lyracist.Shared;
using Xunit;

namespace KSRotation.Tests;

public class AvatarImageHelperTests
{
    [Fact]
    public void GetExifOrientation_NullOrEmpty_ReturnsOne()
    {
        Assert.Equal(1, AvatarImageHelper.GetExifOrientation(null!));
        Assert.Equal(1, AvatarImageHelper.GetExifOrientation([]));
        Assert.Equal(1, AvatarImageHelper.GetExifOrientation([0x01, 0x02, 0x03]));
    }

    [Fact]
    public void GetExifOrientation_PngHeader_ReturnsOne()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(1, AvatarImageHelper.GetExifOrientation(pngHeader));
    }

    [Fact]
    public void GetExifTransform_Orientation1_ReturnsNull()
    {
        Assert.Null(AvatarImageHelper.GetExifTransform(1));
    }

    [Theory]
    [InlineData(3, 180)]
    [InlineData(6, 90)]
    [InlineData(8, 270)]
    public void GetExifTransform_RotatedOrientations_MatchExpected(int orientation, double expectedAngle)
    {
        var transform = AvatarImageHelper.GetExifTransform(orientation);
        var rotate = Assert.IsType<RotateTransform>(transform);
        Assert.Equal(expectedAngle, rotate.Angle);
    }

    [Fact]
    public void GetExifOrientation_SyntheticJpegWithExifOrientation6_LittleEndian_ReturnsSix()
    {
        byte[] jpeg = CreateSyntheticJpegWithExif(orientation: 6, littleEndian: true);
        int parsed = AvatarImageHelper.GetExifOrientation(jpeg);
        Assert.Equal(6, parsed);
    }

    [Fact]
    public void GetExifOrientation_SyntheticJpegWithExifOrientation8_BigEndian_ReturnsEight()
    {
        byte[] jpeg = CreateSyntheticJpegWithExif(orientation: 8, littleEndian: false);
        int parsed = AvatarImageHelper.GetExifOrientation(jpeg);
        Assert.Equal(8, parsed);
    }

    [Fact]
    public void RotateImage90Degrees_RotatesBitmapDimensions()
    {
        byte[] original = CreateTestJpeg(width: 40, height: 20);
        byte[] rotated = AvatarImageHelper.RotateImage90Degrees(original);

        Assert.NotEmpty(rotated);

        using var ms = new MemoryStream(rotated);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        // 40x20 rotated 90 degrees should become 20x40
        Assert.Equal(20, frame.PixelWidth);
        Assert.Equal(40, frame.PixelHeight);
    }

    [Fact]
    public void NormalizeImageBytes_AppliesExifOrientationAndProducesValidJpeg()
    {
        byte[] original = CreateSyntheticJpegWithExif(orientation: 6, littleEndian: true);
        byte[] normalized = AvatarImageHelper.NormalizeImageBytes(original);

        Assert.NotEmpty(normalized);

        using var ms = new MemoryStream(normalized);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        Assert.True(frame.PixelWidth > 0);
        Assert.True(frame.PixelHeight > 0);
    }
    [Fact]
    public void RenderCroppedAvatarBitmap_ProducesSquareBitmapWithSpecifiedDimensions()
    {
        var testBitmap = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.Crimson, null, new System.Windows.Rect(0, 0, 400, 300));
        }
        testBitmap.Render(visual);

        var cropped = AvatarImageHelper.RenderCroppedAvatarBitmap(
            testBitmap,
            panX: 10,
            panY: -5,
            zoom: 1.5,
            viewportSize: 340,
            cropDiameter: 280,
            outputDimension: 512);

        Assert.NotNull(cropped);
        Assert.Equal(512, cropped.PixelWidth);
        Assert.Equal(512, cropped.PixelHeight);

        // Verify pixel colors: all 4 corners and center should be Crimson (non-black, red dominant)
        var pixels = new uint[512 * 512];
        cropped.CopyPixels(pixels, 512 * 4, 0);

        // Top-left, top-right, bottom-left, bottom-right, center
        int[] sampleIndices = [0, 511, 256 * 512 + 256, 511 * 512, 511 * 512 + 511];
        foreach (int idx in sampleIndices)
        {
            uint argb = pixels[idx];
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            Assert.True(a > 200, $"Expected alpha > 200 at pixel {idx}, got {a}");
            Assert.True(r > 150, $"Expected red > 150 (not black) at pixel {idx}, got r={r}");
        }

        byte[] jpeg = AvatarImageHelper.EncodeBitmapToJpeg(cropped);
        Assert.NotEmpty(jpeg);
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);
    }
    private static RenderTargetBitmap Solid(int w, int h, Brush? brush)
    {
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            if (brush != null) ctx.DrawRectangle(brush, null, new System.Windows.Rect(0, 0, w, h));
        }
        bmp.Render(visual);
        return bmp;
    }

    [Theory]
    [InlineData(400, 300, 0, 0, 1.0)]
    [InlineData(300, 600, 5000, -5000, 1.0)]
    [InlineData(1000, 700, -9999, 9999, 3.5)]
    [InlineData(280, 280, 40, 40, 2.0)]
    public void RenderCroppedAvatarBitmap_NeverContainsBlackPixels(int w, int h, double panX, double panY, double zoom)
    {
        var cropped = AvatarImageHelper.RenderCroppedAvatarBitmap(Solid(w, h, Brushes.Crimson), panX, panY, zoom);
        var pixels = new uint[512 * 512];
        cropped.CopyPixels(pixels, 512 * 4, 0);
        Assert.DoesNotContain(pixels, p => (p & 0x00FFFFFF) < 0x000A0A0A);
    }

    [Fact]
    public void RenderCroppedAvatarBitmap_TransparentSource_IsNotBlack()
    {
        var cropped = AvatarImageHelper.RenderCroppedAvatarBitmap(Solid(400, 300, null), 0, 0, 1.0);
        var pixels = new uint[512 * 512];
        cropped.CopyPixels(pixels, 512 * 4, 0);
        Assert.All(pixels, p => Assert.True((p & 0x00FFFFFF) > 0x00F0F0F0, $"pixel {p:X8} is not white"));
    }
    [Fact]
    public void RawCopyHelpers_RotateDeleteAndPurge()
    {
        string dir = Path.Combine(Path.GetTempPath(), "avatar_raw_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            byte[] jpeg = AvatarImageHelper.EncodeBitmapToJpeg(Solid(400, 200, Brushes.Crimson));
            string rawPath = AvatarImageHelper.GetRawCopyPath(dir, "a.jpg");
            File.WriteAllBytes(rawPath, jpeg);

            AvatarImageHelper.RotateRawCopy(dir, "a.jpg");
            var rotated = AvatarImageHelper.LoadOrientedBitmapFromFile(rawPath)!;
            Assert.Equal(200, rotated.PixelWidth);
            Assert.Equal(400, rotated.PixelHeight);

            AvatarImageHelper.DeleteRawCopy(dir, "a.jpg");
            Assert.False(File.Exists(rawPath));

            string oldRaw = AvatarImageHelper.GetRawCopyPath(dir, "old.jpg");
            string newRaw = AvatarImageHelper.GetRawCopyPath(dir, "new.jpg");
            File.WriteAllBytes(oldRaw, jpeg);
            File.WriteAllBytes(newRaw, jpeg);
            File.SetLastWriteTimeUtc(oldRaw, DateTime.UtcNow.AddDays(-30));
            AvatarImageHelper.PurgeStaleRawCopies(dir, TimeSpan.FromDays(7));
            Assert.False(File.Exists(oldRaw));
            Assert.True(File.Exists(newRaw));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
    private static byte[] CreateTestJpeg(int width, int height)
    {
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.Crimson, null, new System.Windows.Rect(0, 0, width, height));
        }
        rtb.Render(visual);

        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static byte[] CreateSyntheticJpegWithExif(ushort orientation, bool littleEndian)
    {
        byte[] baseJpeg = CreateTestJpeg(30, 20);

        // Build APP1 Exif segment
        using var app1Ms = new MemoryStream();
        using (var bw = new BinaryWriter(app1Ms))
        {
            // Exif header: "Exif\0\0"
            bw.Write(new byte[] { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 });

            long tiffStart = app1Ms.Position;
            if (littleEndian)
            {
                bw.Write(new byte[] { 0x49, 0x49, 0x2A, 0x00 }); // II + 42
                bw.Write((uint)8); // offset to IFD0
                bw.Write((ushort)1); // 1 IFD entry
                // Entry: Tag 0x0112 (Orientation), Type 3 (SHORT), Count 1, Value orientation
                bw.Write((ushort)0x0112);
                bw.Write((ushort)3);
                bw.Write((uint)1);
                bw.Write(orientation);
                bw.Write((ushort)0); // padding
                bw.Write((uint)0); // Next IFD offset
            }
            else
            {
                bw.Write(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }); // MM + 42
                bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x08 }); // offset 8
                bw.Write(new byte[] { 0x00, 0x01 }); // 1 entry
                // Entry in big-endian
                bw.Write(new byte[] { 0x01, 0x12 });
                bw.Write(new byte[] { 0x00, 0x03 });
                bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x01 });
                bw.Write((byte)(orientation >> 8));
                bw.Write((byte)(orientation & 0xFF));
                bw.Write(new byte[] { 0x00, 0x00 });
                bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
            }
        }

        byte[] app1Data = app1Ms.ToArray();
        ushort app1Len = (ushort)(app1Data.Length + 2);

        using var finalMs = new MemoryStream();
        finalMs.WriteByte(0xFF);
        finalMs.WriteByte(0xD8); // SOI

        finalMs.WriteByte(0xFF);
        finalMs.WriteByte(0xE1); // APP1
        finalMs.WriteByte((byte)(app1Len >> 8));
        finalMs.WriteByte((byte)(app1Len & 0xFF));
        finalMs.Write(app1Data, 0, app1Data.Length);

        // Append rest of baseJpeg starting after SOI (offset 2)
        finalMs.Write(baseJpeg, 2, baseJpeg.Length - 2);

        return finalMs.ToArray();
    
    }
}
