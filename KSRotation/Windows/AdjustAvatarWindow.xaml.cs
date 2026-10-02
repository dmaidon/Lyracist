// Edited on Oct 2, 2026 @ 13:10:00 -> Clamp pan offsets to crop circle bounds to prevent void spaces
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lyracist.Shared;

namespace KSRotation.Windows;

public partial class AdjustAvatarWindow : Window
{
    private const double ViewportSize = 340.0;
    private const double CropDiameter = 280.0;

    private readonly string _destinationPath;
    private BitmapSource? _sourceBitmap;
    private double _baseScale = 1.0;
    private double _zoom = 1.0;
    private double _panX = 0.0;
    private double _panY = 0.0;
    private bool _isDragging;
    private System.Windows.Point _lastMousePos;

    public AdjustAvatarWindow(string sourcePath, string destinationPath, string singerName = "")
    {
        InitializeComponent();

        _destinationPath = destinationPath;

        if (!string.IsNullOrWhiteSpace(singerName))
        {
            TitleText.Text = $"CENTER PHOTO: {singerName.ToUpperInvariant()}";
            Title = $"Center Photo - {singerName}";
        }

        if (File.Exists(sourcePath))
        {
            _sourceBitmap = AvatarImageHelper.LoadOrientedBitmapFromFile(sourcePath);
        }

        Loaded += (_, _) =>
        {
            if (_sourceBitmap != null)
            {
                PreviewImage.Source = _sourceBitmap;
                RecalculateBaseScale();
                UpdateImageTransform();
            }
        };
    }

    private void RecalculateBaseScale()
    {
        if (_sourceBitmap == null) return;
        _baseScale = Math.Max(CropDiameter / _sourceBitmap.PixelWidth, CropDiameter / _sourceBitmap.PixelHeight);
    }

    private void UpdateImageTransform()
    {
        if (_sourceBitmap == null) return;

        double effectiveScale = _baseScale * _zoom;
        double renderWidth = _sourceBitmap.PixelWidth * effectiveScale;
        double renderHeight = _sourceBitmap.PixelHeight * effectiveScale;

        // Clamp pan offsets so the image always completely covers the circular viewfinder
        double maxPanX = Math.Max(0, (renderWidth - CropDiameter) / 2.0);
        double maxPanY = Math.Max(0, (renderHeight - CropDiameter) / 2.0);

        _panX = Math.Clamp(_panX, -maxPanX, maxPanX);
        _panY = Math.Clamp(_panY, -maxPanY, maxPanY);

        double center = ViewportSize / 2.0;
        double imgCenterX = center + _panX;
        double imgCenterY = center + _panY;

        double left = imgCenterX - (renderWidth / 2.0);
        double top = imgCenterY - (renderHeight / 2.0);

        PreviewImage.Width = renderWidth;
        PreviewImage.Height = renderHeight;
        Canvas.SetLeft(PreviewImage, left);
        Canvas.SetTop(PreviewImage, top);

        ZoomText.Text = $"{_zoom:0.0}x";
    }

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastMousePos = e.GetPosition(ViewportBorder);
        ViewportBorder.CaptureMouse();
    }

    private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || _sourceBitmap == null) return;

        System.Windows.Point currentPos = e.GetPosition(ViewportBorder);
        Vector delta = currentPos - _lastMousePos;
        _lastMousePos = currentPos;

        _panX += delta.X;
        _panY += delta.Y;

        UpdateImageTransform();
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ViewportBorder.ReleaseMouseCapture();
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double step = e.Delta > 0 ? 0.1 : -0.1;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + step, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _zoom = e.NewValue;
        UpdateImageTransform();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _panX = 0;
        _panY = 0;
        _zoom = 1.0;
        ZoomSlider.Value = 1.0;
        UpdateImageTransform();
    }

    private void OnRotateClicked(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null) return;

        try
        {
            var rotated = new TransformedBitmap(_sourceBitmap, new RotateTransform(90));
            rotated.Freeze();
            _sourceBitmap = rotated;
            PreviewImage.Source = _sourceBitmap;

            _panX = 0;
            _panY = 0;
            RecalculateBaseScale();
            UpdateImageTransform();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to rotate photo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (_sourceBitmap == null)
        {
            DialogResult = false;
            Close();
            return;
        }

        try
        {
            var cropped = AvatarImageHelper.RenderCroppedAvatarBitmap(
                _sourceBitmap,
                _panX,
                _panY,
                _zoom,
                viewportSize: ViewportSize,
                cropDiameter: CropDiameter,
                outputDimension: 512);

            byte[] jpegBytes = AvatarImageHelper.EncodeBitmapToJpeg(cropped, quality: 92);
            File.WriteAllBytes(_destinationPath, jpegBytes);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save centered photo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
