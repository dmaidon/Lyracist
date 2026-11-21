// Edited on Nov 21, 2026 @ 08:22:00 -> Use explicit WPF type aliases in SingerHistoryContextMenuHelper to avoid WinForms ambiguity
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lyracist.Services.Database;
using WpfBorder = System.Windows.Controls.Border;
using WpfColor = System.Windows.Media.Color;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfContextMenuEventArgs = System.Windows.Controls.ContextMenuEventArgs;
using WpfControlTemplate = System.Windows.Controls.ControlTemplate;
using WpfItemsPresenter = System.Windows.Controls.ItemsPresenter;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Lyracist.Shared
{
    /// <summary>
    /// Builds and attaches a right-click <see cref="WpfContextMenu"/> for song entry <see cref="WpfTextBox"/> controls.
    /// When the performer exists in the singer database (<c>Singers</c> or <c>SingerHistory</c>), right-clicking
    /// the song entry text box displays a scrollable list of their previously sung songs; selecting any song
    /// populates the song entry fields for that performer.
    /// </summary>
    public static class SingerHistoryContextMenuHelper
    {
        private const string GeneratedMenuTag = "PreviouslySungSongsContextMenu";

        private static readonly SolidColorBrush MenuBackgroundBrush = CreateFrozenBrush(0x0F, 0x17, 0x2A);
        private static readonly SolidColorBrush MenuBorderBrush = CreateFrozenBrush(0x0E, 0xA5, 0xE9);
        private static readonly SolidColorBrush HeaderForegroundBrush = CreateFrozenBrush(0x38, 0xBD, 0xF8);
        private static readonly SolidColorBrush PrimaryTextBrush = CreateFrozenBrush(0xF8, 0xFA, 0xFC);
        private static readonly SolidColorBrush SecondaryTextBrush = CreateFrozenBrush(0xCB, 0xD5, 0xE1);
        private static readonly SolidColorBrush MutedTextBrush = CreateFrozenBrush(0x94, 0xA3, 0xB8);
        private static readonly SolidColorBrush CountBadgeBrush = CreateFrozenBrush(0x10, 0xB9, 0x81);
        private static readonly SolidColorBrush KeyBadgeBrush = CreateFrozenBrush(0xF5, 0x9E, 0x0B);
        private static readonly SolidColorBrush SeparatorBrush = CreateFrozenBrush(0x33, 0x41, 0x55);

        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(WpfColor.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Attaches right-click handlers to <paramref name="textBox"/> so right-clicking checks if the singer returned
        /// by <paramref name="getSingerName"/> exists in the database and, if so, shows their previously sung songs.
        /// </summary>
        public static void Attach(
            WpfTextBox? textBox,
            Func<string?> getSingerName,
            Action<SingerHistoryEntry> onSongSelected,
            Action? beforeLookup = null)
        {
            if (textBox == null) return;
            ArgumentNullException.ThrowIfNull(getSingerName);
            ArgumentNullException.ThrowIfNull(onSongSelected);

            textBox.PreviewMouseRightButtonDown += (_, _) =>
            {
                HandlePreviewMouseRightButtonDown(textBox, getSingerName, onSongSelected, beforeLookup);
            };

            textBox.ContextMenuOpening += (_, e) =>
            {
                HandleContextMenuOpening(textBox, e, getSingerName, onSongSelected, beforeLookup);
            };
        }

        /// <summary>
        /// Handles <see cref="UIElement.PreviewMouseRightButtonDown"/> on a song entry <see cref="WpfTextBox"/>,
        /// focusing the text box first (so any sibling Singer Name box with <c>UpdateSourceTrigger=LostFocus</c>
        /// commits its value) and preparing the previously sung songs <see cref="WpfContextMenu"/> if the singer exists in the database.
        /// </summary>
        public static void HandlePreviewMouseRightButtonDown(
            WpfTextBox? textBox,
            Func<string?> getSingerName,
            Action<SingerHistoryEntry> onSongSelected,
            Action? beforeLookup = null)
        {
            if (textBox == null) return;

            if (!textBox.IsKeyboardFocusWithin)
            {
                textBox.Focus();
            }

            beforeLookup?.Invoke();

            string? singerName = getSingerName();
            var menu = BuildContextMenuIfSingerExists(textBox, singerName, onSongSelected);
            if (menu != null)
            {
                textBox.ContextMenu = menu;
            }
            else if (textBox.ContextMenu != null && Equals(textBox.ContextMenu.Tag, GeneratedMenuTag))
            {
                textBox.ContextMenu = null;
            }
        }

        /// <summary>
        /// Handles <see cref="FrameworkElement.ContextMenuOpening"/> on a song entry <see cref="WpfTextBox"/>.
        /// Ensures the previously sung songs <see cref="WpfContextMenu"/> is shown when the singer exists in the database,
        /// or falls back to the standard text box context menu when the singer is not in the database.
        /// </summary>
        public static void HandleContextMenuOpening(
            WpfTextBox? textBox,
            WpfContextMenuEventArgs? e,
            Func<string?> getSingerName,
            Action<SingerHistoryEntry> onSongSelected,
            Action? beforeLookup = null)
        {
            if (textBox == null) return;

            beforeLookup?.Invoke();

            string? singerName = getSingerName();
            var menu = BuildContextMenuIfSingerExists(textBox, singerName, onSongSelected);
            if (menu != null)
            {
                textBox.ContextMenu = menu;
                menu.PlacementTarget = textBox;
                menu.IsOpen = true;
                if (e != null)
                {
                    e.Handled = true;
                }
            }
            else if (textBox.ContextMenu != null && Equals(textBox.ContextMenu.Tag, GeneratedMenuTag))
            {
                textBox.ContextMenu = null;
            }
        }

        /// <summary>
        /// Formats a human-readable single-line label for a historical song entry in the right-click menu.
        /// </summary>
        public static string FormatSongMenuItemText(SingerHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            string title = (entry.SongTitle ?? string.Empty).Trim();
            string artist = (entry.Artist ?? string.Empty).Trim();
            int count = Math.Max(1, entry.TimesPerformed);
            string key = (entry.Key ?? "0").Trim();

            string label = !string.IsNullOrWhiteSpace(artist)
                ? $"{title} — {artist} ({count}x)"
                : $"{title} ({count}x)";

            if (!string.IsNullOrWhiteSpace(key) && key != "0")
            {
                label += $" [Key: {key}]";
            }

            return label;
        }

        /// <summary>
        /// Queries <see cref="SingerHistoryService.GetPreviouslySungSongsForSinger"/> and returns a populated
        /// <see cref="WpfContextMenu"/> if <paramref name="singerName"/> exists in the singer database; otherwise returns <c>null</c>.
        /// </summary>
        public static WpfContextMenu? BuildContextMenuIfSingerExists(
            WpfTextBox? targetTextBox,
            string? singerName,
            Action<SingerHistoryEntry> onSongSelected)
        {
            ArgumentNullException.ThrowIfNull(onSongSelected);

            List<SingerHistoryEntry> songs = SingerHistoryService.GetPreviouslySungSongsForSinger(
                singerName,
                out string resolvedSingerName,
                out bool singerExistsInDatabase);

            if (!singerExistsInDatabase)
            {
                return null;
            }

            string displaySingerName = !string.IsNullOrWhiteSpace(resolvedSingerName)
                ? resolvedSingerName
                : NameFormatting.CleanSingerName(singerName ?? string.Empty);

            var menu = new WpfContextMenu
            {
                Tag = GeneratedMenuTag,
                Background = MenuBackgroundBrush,
                BorderBrush = MenuBorderBrush,
                BorderThickness = new Thickness(1.5),
                Foreground = PrimaryTextBrush,
                Padding = new Thickness(4),
                Template = CreateScrollableContextMenuTemplate()
            };

            // 1. Header item
            var headerItem = new WpfMenuItem
            {
                IsEnabled = false,
                Header = new WpfTextBlock
                {
                    Text = $"🎤 Previously Sung by {displaySingerName} ({songs.Count})",
                    Foreground = HeaderForegroundBrush,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Margin = new Thickness(2, 2, 6, 2)
                }
            };
            menu.Items.Add(headerItem);
            menu.Items.Add(CreateStyledSeparator());

            // 2. Previously sung songs list
            if (songs.Count == 0)
            {
                menu.Items.Add(new WpfMenuItem
                {
                    IsEnabled = false,
                    Header = new WpfTextBlock
                    {
                        Text = "(No previously sung songs recorded yet)",
                        Foreground = MutedTextBrush,
                        FontStyle = FontStyles.Italic,
                        FontSize = 12.5,
                        Margin = new Thickness(2, 2, 6, 2)
                    }
                });
            }
            else
            {
                foreach (var song in songs)
                {
                    var capturedSong = song;
                    var itemPanel = new WpfStackPanel
                    {
                        Orientation = WpfOrientation.Horizontal,
                        Margin = new Thickness(2, 1, 6, 1)
                    };

                    itemPanel.Children.Add(new WpfTextBlock
                    {
                        Text = capturedSong.SongTitle,
                        Foreground = PrimaryTextBrush,
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13
                    });

                    if (!string.IsNullOrWhiteSpace(capturedSong.Artist))
                    {
                        itemPanel.Children.Add(new WpfTextBlock
                        {
                            Text = $" — {capturedSong.Artist}",
                            Foreground = SecondaryTextBrush,
                            FontSize = 12.5
                        });
                    }

                    itemPanel.Children.Add(new WpfTextBlock
                    {
                        Text = $"  ({Math.Max(1, capturedSong.TimesPerformed)}x)",
                        Foreground = CountBadgeBrush,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12
                    });

                    if (!string.IsNullOrWhiteSpace(capturedSong.Key) && capturedSong.Key != "0")
                    {
                        itemPanel.Children.Add(new WpfTextBlock
                        {
                            Text = $"  [Key: {capturedSong.Key}]",
                            Foreground = KeyBadgeBrush,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 11.5
                        });
                    }

                    string venueInfo = !string.IsNullOrWhiteSpace(capturedSong.Venue)
                        ? $"\nVenue(s): {capturedSong.Venue}"
                        : string.Empty;

                    var songItem = new WpfMenuItem
                    {
                        Header = itemPanel,
                        Tag = FormatSongMenuItemText(capturedSong),
                        Foreground = PrimaryTextBrush,
                        ToolTip = $"{capturedSong.SongTitle}" +
                                  (!string.IsNullOrWhiteSpace(capturedSong.Artist) ? $" by {capturedSong.Artist}" : string.Empty) +
                                  $"\nSung: {Math.Max(1, capturedSong.TimesPerformed)}x | Key: {capturedSong.Key} | Speed: {capturedSong.Tempo:0.0}x" +
                                  venueInfo +
                                  "\nClick to use this song"
                    };

                    songItem.Click += (_, _) =>
                    {
                        onSongSelected(capturedSong);
                    };

                    menu.Items.Add(songItem);
                }
            }

            // 3. Standard clipboard commands so Cut / Copy / Paste remain accessible
            if (targetTextBox != null)
            {
                menu.Items.Add(CreateStyledSeparator());
                menu.Items.Add(CreateClipboardMenuItem("Cut", ApplicationCommands.Cut, targetTextBox));
                menu.Items.Add(CreateClipboardMenuItem("Copy", ApplicationCommands.Copy, targetTextBox));
                menu.Items.Add(CreateClipboardMenuItem("Paste", ApplicationCommands.Paste, targetTextBox));
            }

            return menu;
        }

        private static WpfSeparator CreateStyledSeparator()
        {
            return new WpfSeparator
            {
                Background = SeparatorBrush,
                Margin = new Thickness(4, 3, 4, 3)
            };
        }

        private static WpfMenuItem CreateClipboardMenuItem(string label, RoutedUICommand command, UIElement target)
        {
            return new WpfMenuItem
            {
                Header = new WpfTextBlock
                {
                    Text = label,
                    Foreground = SecondaryTextBrush,
                    FontSize = 12
                },
                Command = command,
                CommandTarget = target
            };
        }

        private static WpfControlTemplate CreateScrollableContextMenuTemplate()
        {
            var template = new WpfControlTemplate(typeof(WpfContextMenu));

            var borderFactory = new FrameworkElementFactory(typeof(WpfBorder));
            borderFactory.SetValue(WpfBorder.BackgroundProperty, MenuBackgroundBrush);
            borderFactory.SetValue(WpfBorder.BorderBrushProperty, MenuBorderBrush);
            borderFactory.SetValue(WpfBorder.BorderThicknessProperty, new Thickness(1.5));
            borderFactory.SetValue(WpfBorder.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(WpfBorder.PaddingProperty, new Thickness(4));
            borderFactory.SetValue(FrameworkElement.MaxHeightProperty, 420.0);
            borderFactory.SetValue(FrameworkElement.MinWidthProperty, 260.0);

            var scrollViewerFactory = new FrameworkElementFactory(typeof(WpfScrollViewer));
            scrollViewerFactory.SetValue(WpfScrollViewer.VerticalScrollBarVisibilityProperty, WpfScrollBarVisibility.Auto);
            scrollViewerFactory.SetValue(WpfScrollViewer.HorizontalScrollBarVisibilityProperty, WpfScrollBarVisibility.Disabled);
            scrollViewerFactory.SetValue(WpfScrollViewer.CanContentScrollProperty, true);

            var itemsPresenterFactory = new FrameworkElementFactory(typeof(WpfItemsPresenter));
            itemsPresenterFactory.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);

            scrollViewerFactory.AppendChild(itemsPresenterFactory);
            borderFactory.AppendChild(scrollViewerFactory);
            template.VisualTree = borderFactory;

            return template;
        }
    }
}
