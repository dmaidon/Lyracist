// Edited on Aug 2, 2026 @ 10:13:00 -> Add UpdateBanner method to support video and image switching/looping
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace KSRotation.Windows
{
    public partial class DjBannerWindow : Window
    {
        public DjBannerWindow()
        {
            InitializeComponent();
        }

        public void UpdateBanner(string? path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                BannerImage.Source = null;
                BannerVideo.Source = null;
                BannerVideo.Visibility = Visibility.Collapsed;
                BannerImage.Visibility = Visibility.Visible;
                return;
            }

            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mp4")
            {
                BannerImage.Source = null;
                BannerImage.Visibility = Visibility.Collapsed;
                BannerVideo.Visibility = Visibility.Visible;
                BannerVideo.Source = new Uri(path);
                BannerVideo.Play();
            }
            else
            {
                BannerVideo.Stop();
                BannerVideo.Source = null;
                BannerVideo.Visibility = Visibility.Collapsed;
                BannerImage.Visibility = Visibility.Visible;
                try
                {
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                    int decodeWidth = GetDecodeTargetWidth(path);
                    if (decodeWidth > 0)
                    {
                        image.DecodePixelWidth = decodeWidth;
                    }
                    image.UriSource = new Uri(path);
                    image.EndInit();
                    image.Freeze();
                    BannerImage.Source = image;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load image banner: {ex.Message}");
                    BannerImage.Source = null;
                }
            }
        }

        // Caps the decoded bitmap to the display's real needs: design resolution is 4K UHD, but if every
        // connected monitor is smaller than that, decoding at 4K would just waste memory for no visible
        // gain. Also never decodes larger than the source image's own native size (would upscale-decode
        // and waste memory on smaller user-uploaded banners).
        private static int GetDecodeTargetWidth(string path)
        {
            const int DesignMaxWidth = 3840; // 4K UHD ceiling — matches the resolution DJ banners are generated at

            int monitorCap = DesignMaxWidth;
            try
            {
                var monitors = Lyracist.Shared.MonitorEnumerator.GetMonitors();
                int largestMonitorWidth = 0;
                foreach (var m in monitors)
                {
                    if (m.Width > largestMonitorWidth) largestMonitorWidth = m.Width;
                }
                if (largestMonitorWidth > 0)
                {
                    monitorCap = Math.Min(DesignMaxWidth, largestMonitorWidth);
                }
            }
            catch
            {
                // Fall back to the 4K design ceiling if monitor enumeration fails
            }

            try
            {
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    new Uri(path),
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                int nativeWidth = decoder.Frames.Count > 0 ? decoder.Frames[0].PixelWidth : 0;
                return nativeWidth > 0 ? Math.Min(monitorCap, nativeWidth) : monitorCap;
            }
            catch
            {
                return monitorCap;
            }
        }

        private void BannerVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BannerVideo.Position = TimeSpan.Zero;
            BannerVideo.Play();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Hide();
                    e.Handled = true;
                    break;
                case Key.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
            }

            base.OnKeyDown(e);
        }

        public bool IsShuttingDown { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (IsShuttingDown)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ToggleFullscreen();
        }

        private void ToggleFullscreen()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
