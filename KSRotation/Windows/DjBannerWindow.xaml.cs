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
