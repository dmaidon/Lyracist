// Created on Sep 6, 2026 @ 09:28:00 -> Shared webcam capture service using FlashCap for performer photo capture
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashCap;

namespace Lyracist.Shared
{
    public class WebcamDeviceInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public CaptureDeviceDescriptor Descriptor { get; set; } = null!;

        public override string ToString() => Name;
    }

    public class WebcamCaptureService : IAsyncDisposable
    {
        private CaptureDevice? _captureDevice;
        private readonly SemaphoreSlim _deviceLock = new(1, 1);
        private bool _isCapturing;

        public static List<WebcamDeviceInfo> GetAvailableCameras()
        {
            var list = new List<WebcamDeviceInfo>();
            try
            {
                var devices = new CaptureDevices();
                int index = 0;
                foreach (var descriptor in devices.EnumerateDescriptors())
                {
                    list.Add(new WebcamDeviceInfo
                    {
                        Index = index++,
                        Name = string.IsNullOrWhiteSpace(descriptor.Name) ? $"Camera {index}" : descriptor.Name,
                        Descriptor = descriptor
                    });
                }
            }
            catch
            {
                // Fallback empty list if DirectShow enumeration fails
            }

            return list;
        }

        public async Task<bool> StartCaptureAsync(CaptureDeviceDescriptor descriptor, Action<BitmapSource> onFrameArrived, CancellationToken token = default)
        {
            await _deviceLock.WaitAsync(token);
            try
            {
                await StopCaptureInternalAsync();

                var characteristics = descriptor.Characteristics
                    .OrderByDescending(c => c.PixelFormat == FlashCap.PixelFormats.JPEG || c.PixelFormat == FlashCap.PixelFormats.PNG)
                    .ThenBy(c => Math.Abs(c.Width - 640))
                    .ThenBy(c => Math.Abs(c.Height - 480))
                    .FirstOrDefault() ?? descriptor.Characteristics.FirstOrDefault();

                if (characteristics == null) return false;

                _captureDevice = await descriptor.OpenAsync(characteristics, async bufferScope =>
                {
                    if (!_isCapturing) return;

                    try
                    {
                        byte[] imageBytes = bufferScope.Buffer.ExtractImage();
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = new MemoryStream(imageBytes);
                            bitmap.EndInit();
                            bitmap.Freeze();

                            onFrameArrived(bitmap);
                        }
                    }
                    catch
                    {
                        // Ignore sporadic frame decode error during stream transitions
                    }

                    await Task.CompletedTask;
                }, token);

                _isCapturing = true;
                await _captureDevice.StartAsync(token);
                return true;
            }
            catch
            {
                _isCapturing = false;
                if (_captureDevice != null)
                {
                    try { await _captureDevice.DisposeAsync(); } catch { }
                    _captureDevice = null;
                }
                return false;
            }
            finally
            {
                _deviceLock.Release();
            }
        }

        public async Task StopCaptureAsync()
        {
            await _deviceLock.WaitAsync();
            try
            {
                await StopCaptureInternalAsync();
            }
            finally
            {
                _deviceLock.Release();
            }
        }

        private async Task StopCaptureInternalAsync()
        {
            _isCapturing = false;
            if (_captureDevice != null)
            {
                try
                {
                    await _captureDevice.StopAsync();
                }
                catch
                {
                    // Ignore stop error
                }
                try
                {
                    await _captureDevice.DisposeAsync();
                }
                catch
                {
                    // Ignore dispose error
                }
                _captureDevice = null;
            }
        }

        public static (string fileName, BitmapSource squareBitmap)? SaveSquarePhoto(BitmapSource sourceFrame, string avatarsDirectory, int targetDimension = 400)
        {
            if (sourceFrame == null) return null;

            try
            {
                Directory.CreateDirectory(avatarsDirectory);

                int width = sourceFrame.PixelWidth;
                int height = sourceFrame.PixelHeight;

                int squareSize = Math.Min(width, height);
                int x = (width - squareSize) / 2;
                int y = (height - squareSize) / 2;

                var cropped = new CroppedBitmap(sourceFrame, new Int32Rect(x, y, squareSize, squareSize));

                // Scale to target dimension
                var scale = (double)targetDimension / squareSize;
                var transformed = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));
                transformed.Freeze();

                string fileName = $"{Guid.NewGuid():N}.jpg";
                string fullPath = Path.Combine(avatarsDirectory, fileName);

                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(transformed));
                using (var stream = File.Create(fullPath))
                {
                    encoder.Save(stream);
                }

                return (fileName, transformed);
            }
            catch
            {
                return null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopCaptureAsync();
            _deviceLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
