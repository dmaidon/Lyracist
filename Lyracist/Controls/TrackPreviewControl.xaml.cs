// Created on Sep 6, 2026 @ 12:16:00 -> Code-behind for TrackPreviewControl with visual tab switching and video preview playback
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Lyracist.ViewModels;

namespace Lyracist.Controls;

public partial class TrackPreviewControl : System.Windows.Controls.UserControl
{
    public TrackPreviewControl()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnVisualTabChanged(object sender, RoutedEventArgs e)
    {
        if (ViewWaveform == null || ViewSpectrogram == null || ViewVideo == null) return;

        if (TabWaveform?.IsChecked == true)
        {
            ViewWaveform.Visibility = Visibility.Visible;
            ViewSpectrogram.Visibility = Visibility.Collapsed;
            ViewVideo.Visibility = Visibility.Collapsed;
            StopVideo();
        }
        else if (TabSpectrogram?.IsChecked == true)
        {
            ViewWaveform.Visibility = Visibility.Collapsed;
            ViewSpectrogram.Visibility = Visibility.Visible;
            ViewVideo.Visibility = Visibility.Collapsed;
            StopVideo();
        }
        else if (TabVideo?.IsChecked == true)
        {
            ViewWaveform.Visibility = Visibility.Collapsed;
            ViewSpectrogram.Visibility = Visibility.Collapsed;
            ViewVideo.Visibility = Visibility.Visible;

            if (DataContext is TrackPreviewViewModel vm && !string.IsNullOrWhiteSpace(vm.VideoPreviewPath) && File.Exists(vm.VideoPreviewPath))
            {
                PreviewVideoPlayer.Source = new Uri(vm.VideoPreviewPath);
                PreviewVideoPlayer.Play();
            }
        }
    }

    private void BtnPlayVideo_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrackPreviewViewModel vm && !string.IsNullOrWhiteSpace(vm.VideoPreviewPath) && File.Exists(vm.VideoPreviewPath))
        {
            if (PreviewVideoPlayer.Source == null || PreviewVideoPlayer.Source.LocalPath != vm.VideoPreviewPath)
            {
                PreviewVideoPlayer.Source = new Uri(vm.VideoPreviewPath);
            }
            PreviewVideoPlayer.Play();
        }
    }

    private void BtnPauseVideo_Click(object sender, RoutedEventArgs e)
    {
        PreviewVideoPlayer.Pause();
    }

    private void BtnStopVideo_Click(object sender, RoutedEventArgs e)
    {
        StopVideo();
    }

    private void PreviewVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        StopVideo();
    }

    private void StopVideo()
    {
        try
        {
            PreviewVideoPlayer.Stop();
            PreviewVideoPlayer.Position = TimeSpan.Zero;
        }
        catch
        {
            // Ignore
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopVideo();
        if (DataContext is TrackPreviewViewModel vm)
        {
            vm.StopAudioCommand.Execute(null);
        }
    }
}
