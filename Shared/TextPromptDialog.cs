// Created on Oct 5, 2026 @ 09:00:00 -> One dark/amber single-field prompt shared by the announcement and birthday banner dialogs in both apps
#if !MAUI
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace Lyracist.Shared;

public static class TextPromptDialog
{
    private static readonly SolidColorBrush Amber = new(Color.FromRgb(245, 158, 11));

    /// <summary>Modal text prompt. Returns the entered text on OK, or null when cancelled.</summary>
    public static string? Show(string title, string label, string defaultText, string okText, bool multiline, double width, double height)
    {
        var window = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(24, 24, 32)),
            Foreground = Brushes.White,
            Topmost = true
        };

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Amber,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(labelBlock, 0);

        var textBox = new TextBox
        {
            Text = defaultText,
            FontSize = multiline ? 15 : 16,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(36, 36, 48)),
            Foreground = Brushes.White,
            BorderBrush = Amber,
            Margin = new Thickness(0, 0, 0, 16)
        };
        textBox.SelectAll();
        Grid.SetRow(textBox, 1);

        string? result = null;
        var okButton = new Button
        {
            Content = okText,
            IsDefault = true,
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = Amber,
            Foreground = Brushes.Black,
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand
        };
        okButton.Click += (_, _) => { result = textBox.Text; window.DialogResult = true; };

        var cancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            Padding = new Thickness(16, 6, 16, 6),
            Cursor = Cursors.Hand
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);
        Grid.SetRow(buttons, 2);

        grid.Children.Add(labelBlock);
        grid.Children.Add(textBox);
        grid.Children.Add(buttons);

        window.Content = grid;
        window.Loaded += (_, _) => textBox.Focus();

        return window.ShowDialog() == true ? result : null;
    }

    public static string? ShowAnnouncement(string defaultText) => Show(
        "Special Event Announcement Banner", "Enter Announcement Text to Display:",
        string.IsNullOrWhiteSpace(defaultText) ? "Welcome to Jill & Robert, 1st timers tonight" : defaultText,
        "📢 Launch Banner", multiline: true, 520, 270);

    public static string? ShowBirthday(string defaultName) => Show(
        "Birthday Special Event Banner", "Enter Birthday Performer Name:", defaultName,
        "🎉 Launch Banner", multiline: false, 440, 220);
}
#endif
