// Created on Oct 7, 2026 @ 19:46:00 -> Code-behind for SamplePadPage handling drag-drop and pad interactions
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Lyracist.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace Lyracist.Views.Pages;

public partial class SamplePadPage : Page, INavigableView<SamplePadViewModel>
{
    public SamplePadViewModel ViewModel { get; }

    public SamplePadPage(SamplePadViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void PadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SamplePadSlotItemViewModel slot)
        {
            slot.Play();
        }
    }

    private void Pad_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private void Pad_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) &&
            sender is FrameworkElement element &&
            element.DataContext is SamplePadSlotItemViewModel slot)
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                string file = files[0];
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string[] validExtensions = [".mp3", ".wav", ".m4a", ".ogg", ".flac", ".wma", ".aac"];
                if (validExtensions.Contains(ext))
                {
                    ViewModel.AssignFile(slot.SlotIndex, file);
                }
                else
                {
                    System.Windows.MessageBox.Show("Please drop a valid audio file (.mp3, .wav, .m4a, .ogg, .flac).", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    private void MenuPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is SamplePadSlotItemViewModel slot)
        {
            slot.Play();
        }
    }

    private void MenuClear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is SamplePadSlotItemViewModel slot)
        {
            ViewModel.ClearSlot(slot.SlotIndex);
        }
    }

    private void MenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is SamplePadSlotItemViewModel slot)
        {
            string? newLabel = Lyracist.Shared.TextPromptDialog.Show(
                "Edit Pad Label",
                "Enter a display label for this pad:",
                slot.Label,
                "Save",
                false,
                420,
                200);

            if (!string.IsNullOrWhiteSpace(newLabel))
            {
                slot.Label = newLabel.Trim();
                ViewModel.UpdateSlot(slot);
            }
        }
    }
}
