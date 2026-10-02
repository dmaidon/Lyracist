// Edited on Oct 2, 2026 @ 14:10:00 -> Add RotateRawCopy, DeleteRawCopy, and PurgeStaleRawCopies for the raw_ editing copies
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lyracist.Shared;

/// <summary>
/// Provides utilities for decoding and normalizing performer avatar photos with automatic EXIF orientation.
/// Mobile phone camera sensors are physically mounted in landscape; portrait selfies carry an EXIF orientation
/// tag (typically orientation 6 = 90° CW). WPF BitmapImage does not read EXIF tags and displays them sideways.
/// This helper detects the EXIF orientation, applies the appropriate rotation/flip transform, and returns an upright
/// frozen BitmapSource suitable for display on projection screens and in the user profile editor.
/// </summary>
public static class AvatarImageHelper
{
    /// <summary>
    /// Parses the raw bytes of an image to extract the EXIF orientation tag (tag 0x0112 / 274).
    /// Returns 1 (normal / upright) if not found, invalid, or unsupported.
    /// Values: 1=Normal, 2=FlipH, 3=Rotate180, 4=FlipV, 5=Transpose, 6=Rotate90CW, 7=Transverse, 8=Rotate270CW.
    /// </summary>
    public static int GetExifOrientation(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 14) return 1;

        // Check JPEG SOI (0xFF, 0xD8)
        if (bytes[0] != 0xFF || bytes[1] != 0xD8) return 1;

        int idx = 2;
        while (idx + 4 < bytes.Length)
        {
            if (bytes[idx] != 0xFF) break;
            byte marker = bytes[idx + 1];
            if (marker == 0xDA || marker == 0xD9) break; // Start of Scan (SOS) or End of Image (EOI)

            int len = (bytes[idx + 2] << 8) | bytes[idx + 3];
            if (marker == 0xE1 && len >= 8) // APP1 marker (Exif metadata)
            {
                int exifIdx = idx + 4;
                if (exifIdx + 6 < bytes.Length &&
                    bytes[exifIdx] == 'E' && bytes[exifIdx + 1] == 'x' && bytes[exifIdx + 2] == 'i' &&
                    bytes[exifIdx + 3] == 'f' && bytes[exifIdx + 4] == 0 && bytes[exifIdx + 5] == 0)
                {
                    int tiffIdx = exifIdx + 6;
                    if (tiffIdx + 8 < bytes.Length)
                    {
                        bool isLe = bytes[tiffIdx] == 'I' && bytes[tiffIdx + 1] == 'I';
                        bool isBe = bytes[tiffIdx] == 'M' && bytes[tiffIdx + 1] == 'M';
                        if (!isLe && !isBe) return 1;

                        int ReadUShort(int pos) => isLe
                            ? (bytes[pos] | (bytes[pos + 1] << 8))
                            : ((bytes[pos] << 8) | bytes[pos + 1]);

                        int ReadUInt(int pos) => isLe
                            ? (bytes[pos] | (bytes[pos + 1] << 8) | (bytes[pos + 2] << 16) | (bytes[pos + 3] << 24))
                            : ((bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3]);

                        int fortyTwo = ReadUShort(tiffIdx + 2);
                        if (fortyTwo != 42) return 1;

                        int ifdOffset = ReadUInt(tiffIdx + 4);
                        int ifdIdx = tiffIdx + ifdOffset;
                        if (ifdIdx + 2 <= bytes.Length)
                        {
                            int entries = ReadUShort(ifdIdx);
                            int entryIdx = ifdIdx + 2;
                            for (int i = 0; i < entries && entryIdx + 12 <= bytes.Length; i++, entryIdx += 12)
                            {
                                int tag = ReadUShort(entryIdx);
                                if (tag == 0x0112) // Orientation tag
                                {
                                    int val = ReadUShort(entryIdx + 8);
                                    if (val >= 1 && val <= 8) return val;
                                }
                            }
                        }
                    }
                }
            }
            idx += 2 + len;
        }

        return 1;
    }

    /// <summary>
    /// Returns the WPF Transform needed to bring an image with the given EXIF orientation upright.
    /// Returns null if orientation is 1 (normal / no transform needed).
    /// </summary>
    public static Transform? GetExifTransform(int orientation)
    {
        Transform? transform = orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(270) } },
            6 => new RotateTransform(90),   // Phone portrait photo rotated 90° CW
            7 => new TransformGroup { Children = { new ScaleTransform(-1, 1), new RotateTransform(90) } },
            8 => new RotateTransform(270),  // Rotated 270° CW / 90° CCW
            _ => null
        };

        transform?.Freeze();
        return transform;
    }

    /// <summary>
    /// Decodes an image from bytes into a frozen BitmapSource, automatically applying EXIF orientation rotation
    /// so phone selfies taken in portrait mode display upright rather than sideways.
    /// </summary>
    public static BitmapSource? LoadOrientedBitmap(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;

        try
        {
            int orientation = GetExifOrientation(bytes);

            using var ms = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;

            BitmapSource frame = decoder.Frames[0];

            // If byte parser found orientation 1, check BitmapFrame metadata as fallback
            if (orientation == 1 && frame.Metadata is BitmapMetadata meta)
            {
                try
                {
                    object? queryVal = null;
                    try { queryVal = meta.GetQuery("/app1/ifd/{ushort=274}"); } catch { }
                    if (queryVal == null) try { queryVal = meta.GetQuery("/ifd/{ushort=274}"); } catch { }
                    if (queryVal == null) try { queryVal = meta.GetQuery("/app1/ifd/exif/{ushort=274}"); } catch { }

                    if (queryVal is ushort uVal && uVal >= 1 && uVal <= 8)
                    {
                        orientation = uVal;
                    }
                    else if (queryVal is int iVal && iVal >= 1 && iVal <= 8)
                    {
                        orientation = iVal;
                    }
                }
                catch
                {
                    // Fallback to orientation 1
                }
            }

            var transform = GetExifTransform(orientation);
            if (transform != null)
            {
                var transformed = new TransformedBitmap();
                transformed.BeginInit();
                transformed.Source = frame;
                transformed.Transform = transform;
                transformed.EndInit();
                transformed.Freeze();
                return transformed;
            }

            frame.Freeze();
            return frame;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads an image from disk and decodes it with automatic EXIF orientation correction.
    /// </summary>
    public static BitmapSource? LoadOrientedBitmapFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            return LoadOrientedBitmap(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes raw avatar image bytes for disk persistence:
    /// - If the image has an EXIF orientation != 1, it is rotated upright and re-encoded.
    /// - If the image exceeds maxDimension (default 1024), it is scaled down proportionally to save disk space and RAM.
    /// - If the image is already upright and within bounds, the original byte array is returned directly with zero overhead.
    /// </summary>
    public static byte[] NormalizeImageBytes(byte[] rawBytes, int maxDimension = 1024)
    {
        if (rawBytes == null || rawBytes.Length == 0) return Array.Empty<byte>();

        try
        {
            int orientation = GetExifOrientation(rawBytes);
            var orientedSource = LoadOrientedBitmap(rawBytes);
            if (orientedSource == null) return rawBytes;

            int w = orientedSource.PixelWidth;
            int h = orientedSource.PixelHeight;

            // If already upright and within maxDimension, keep original bytes unchanged
            if (orientation <= 1 && w <= maxDimension && h <= maxDimension)
            {
                return rawBytes;
            }

            BitmapSource finalSource = orientedSource;
            if (w > maxDimension || h > maxDimension)
            {
                double scale = Math.Min((double)maxDimension / w, (double)maxDimension / h);
                var scaled = new TransformedBitmap(orientedSource, new ScaleTransform(scale, scale));
                scaled.Freeze();
                finalSource = scaled;
            }

            var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
            encoder.Frames.Add(BitmapFrame.Create(finalSource));
            using var outMs = new MemoryStream();
            encoder.Save(outMs);
            return outMs.ToArray();
        }
        catch
        {
            return rawBytes;
        }
    }

    /// <summary>
    /// Rotates an existing image 90 degrees clockwise and returns the re-encoded JPEG bytes.
    /// </summary>
    public static byte[] RotateImage90Degrees(byte[] rawBytes)
    {
        if (rawBytes == null || rawBytes.Length == 0) return Array.Empty<byte>();

        try
        {
            var source = LoadOrientedBitmap(rawBytes);
            if (source == null) return rawBytes;

            var rotated = new TransformedBitmap(source, new RotateTransform(90));
            rotated.Freeze();

            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(rotated));
            using var outMs = new MemoryStream();
            encoder.Save(outMs);
            return outMs.ToArray();
        }
        catch
        {
            return rawBytes;
        }
    }

    /// <summary>
    /// Renders a cropped square avatar from a source bitmap based on pan offsets and zoom level
    /// relative to a circular/square viewport framing frame.
    /// Slices directly from native source image pixel coordinates and scales to outputDimension,
    /// guaranteeing zero black borders or empty margins.
    /// </summary>
    public static BitmapSource RenderCroppedAvatarBitmap(
        BitmapSource source,
        double panX,
        double panY,
        double zoom,
        double viewportSize = 340,
        double cropDiameter = 280,
        int outputDimension = 512)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        double safeZoom = Math.Max(0.1, zoom);
        double baseScale = Math.Max(cropDiameter / source.PixelWidth, cropDiameter / source.PixelHeight);
        double effectiveScale = baseScale * safeZoom;

        // Crop frame dimension in native source pixels
        double cropSizeSrc = cropDiameter / effectiveScale;
        int cropInt = Math.Max(1, (int)Math.Round(cropSizeSrc));
        cropInt = Math.Min(cropInt, Math.Min(source.PixelWidth, source.PixelHeight));

        // Center of the crop frame in source pixel space:
        // When the photo is dragged by (panX, panY), the viewfinder's center moves in the opposite direction on the source photo.
        double centerXSrc = (source.PixelWidth / 2.0) - (panX / effectiveScale);
        double centerYSrc = (source.PixelHeight / 2.0) - (panY / effectiveScale);

        double leftSrc = centerXSrc - (cropInt / 2.0);
        double topSrc = centerYSrc - (cropInt / 2.0);

        int leftInt = (int)Math.Round(leftSrc);
        int topInt = (int)Math.Round(topSrc);

        leftInt = Math.Clamp(leftInt, 0, Math.Max(0, source.PixelWidth - cropInt));
        topInt = Math.Clamp(topInt, 0, Math.Max(0, source.PixelHeight - cropInt));

        var cropped = new CroppedBitmap(source, new Int32Rect(leftInt, topInt, cropInt, cropInt));

        var rtb = new RenderTargetBitmap(outputDimension, outputDimension, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var dc = visual.RenderOpen())
        {
            // Opaque backdrop first: JPEG has no alpha, so transparent PNG/GIF pixels would otherwise encode as solid black.
            dc.DrawRectangle(System.Windows.Media.Brushes.White, null, new Rect(0, 0, outputDimension, outputDimension));
            dc.DrawImage(cropped, new Rect(0, 0, outputDimension, outputDimension));
        }

        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// <summary>Path of the untouched "raw_" copy that the Center Face editor re-crops from.</summary>
    public static string GetRawCopyPath(string avatarsDir, string fileName) => Path.Combine(avatarsDir, $"raw_{fileName}");

    /// <summary>Rotates the raw_ copy 90 degrees clockwise too, so Center Face doesn't undo a rotation of the saved photo.</summary>
    public static void RotateRawCopy(string avatarsDir, string fileName)
    {
        try
        {
            string rawPath = GetRawCopyPath(avatarsDir, fileName);
            if (!File.Exists(rawPath)) return;
            File.WriteAllBytes(rawPath, RotateImage90Degrees(File.ReadAllBytes(rawPath)));
        }
        catch
        {
            // Best effort: a stale raw copy only affects the next Center Face session.
        }
    }

    /// <summary>Deletes the raw_ copy of an avatar that is being replaced or cleared.</summary>
    public static void DeleteRawCopy(string avatarsDir, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        try
        {
            string rawPath = GetRawCopyPath(avatarsDir, fileName);
            if (File.Exists(rawPath)) File.Delete(rawPath);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    /// <summary>Deletes raw_ copies not touched for <paramref name="maxAge"/>; they only matter while a photo is being centered.</summary>
    public static void PurgeStaleRawCopies(string avatarsDir, TimeSpan maxAge)
    {
        try
        {
            if (!Directory.Exists(avatarsDir)) return;
            DateTime cutoff = DateTime.UtcNow - maxAge;
            foreach (string file in Directory.EnumerateFiles(avatarsDir, "raw_*"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                }
                catch
                {
                    // Skip files that are locked or already gone.
                }
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    /// <summary>
    /// Encodes a bitmap to JPEG bytes with specified quality level.
    /// </summary>
    public static byte[] EncodeBitmapToJpeg(BitmapSource source, int quality = 92)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
