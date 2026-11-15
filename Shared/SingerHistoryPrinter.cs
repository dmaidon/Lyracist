// Edited on Nov 14, 2026 @ 13:57:30 -> Switch SingerHistoryPrinter to deterministic FixedDocument/FixedPage WPF Grid layout with NoWrap single-line rows
#if !MAUI
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Lyracist.Data.Services;
using Lyracist.Services.Database;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using PrintDialog = System.Windows.Controls.PrintDialog;
using Size = System.Windows.Size;

namespace Lyracist.Shared;

public static class SingerHistoryPrinter
{
    private static readonly FontFamily PrintFontFamily = new("Segoe UI, Arial");
    private static readonly SolidColorBrush HeaderBgBrush = CreateFrozenBrush(30, 41, 59);       // #1E293B
    private static readonly SolidColorBrush BannerBgBrush = CreateFrozenBrush(241, 245, 249);    // #F1F5F9
    private static readonly SolidColorBrush BannerBorderBrush = CreateFrozenBrush(148, 163, 184);// #94A3B8
    private static readonly SolidColorBrush ZebraRowBrush = CreateFrozenBrush(248, 250, 252);    // #F8FAFC
    private static readonly SolidColorBrush CellBorderBrush = CreateFrozenBrush(203, 213, 225);  // #CBD5E1
    private static readonly SolidColorBrush DarkTextBrush = CreateFrozenBrush(15, 23, 42);       // #0F172A
    private static readonly SolidColorBrush MutedTextBrush = CreateFrozenBrush(71, 85, 105);     // #475569

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public static (int UniqueSongs, int TotalPerformances, string ConsolidatedVenues) CalculateSummaryStats(
        string? profileVenues,
        IReadOnlyList<SingerHistoryEntry>? entries)
    {
        if (entries == null || entries.Count == 0)
        {
            string initialVenues = DatabaseService.MergeVenueLists(profileVenues, null);
            return (0, 0, initialVenues);
        }

        int uniqueSongs = entries.Count;
        int totalPerformances = entries.Sum(e => Math.Max(1, e.TimesPerformed));
        string mergedVenues = profileVenues ?? string.Empty;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Venue))
            {
                mergedVenues = DatabaseService.MergeVenueLists(mergedVenues, entry.Venue);
            }
        }
        mergedVenues = DatabaseService.MergeVenueLists(mergedVenues, null);
        return (uniqueSongs, totalPerformances, mergedVenues);
    }

    /// <summary>
    /// Opens the Windows Print Dialog and prints a formatted multi-page report of the specified singer's song history,
    /// including the number of times each song has been performed and the venue(s).
    /// </summary>
    public static bool PrintSingerHistory(
        string singerName,
        string? profileVenues,
        string? vocalRange,
        string? customTitle,
        IReadOnlyList<SingerHistoryEntry> historyEntries,
        out string statusMessage)
    {
        if (string.IsNullOrWhiteSpace(singerName))
        {
            statusMessage = "No singer selected to print.";
            return false;
        }

        try
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                statusMessage = "Printing cancelled.";
                return false;
            }

            double pageWidth = printDialog.PrintableAreaWidth > 100 ? printDialog.PrintableAreaWidth : 816;
            double pageHeight = printDialog.PrintableAreaHeight > 100 ? printDialog.PrintableAreaHeight : 1056;

            var fixedDoc = BuildFixedDocument(
                singerName.Trim(),
                profileVenues,
                vocalRange,
                customTitle,
                historyEntries ?? [],
                pageWidth,
                pageHeight);

            printDialog.PrintDocument(fixedDoc.DocumentPaginator, $"Singer History - {singerName.Trim()}");

            int uniqueCount = historyEntries?.Count ?? 0;
            int totalPerfs = historyEntries?.Sum(h => Math.Max(1, h.TimesPerformed)) ?? 0;
            statusMessage = $"Printed history for '{singerName.Trim()}' ({uniqueCount} unique song{(uniqueCount == 1 ? "" : "s")}, {totalPerfs} total performance{(totalPerfs == 1 ? "" : "s")}).";
            return true;
        }
        catch (Exception ex)
        {
            Globals.LogError("SingerHistoryPrinter", $"Error printing history for '{singerName}'", ex);
            statusMessage = $"Failed to print singer history: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Builds a deterministic, pixel-exact WPF FixedDocument using native Grid rows with TextWrapping.NoWrap
    /// so columns and headers never wrap vertically across any printer driver.
    /// </summary>
    public static FixedDocument BuildFixedDocument(
        string singerName,
        string? profileVenues,
        string? vocalRange,
        string? customTitle,
        IReadOnlyList<SingerHistoryEntry> historyEntries,
        double pageWidth = 816,
        double pageHeight = 1056)
    {
        double effectivePageWidth = pageWidth > 100 ? pageWidth : 816;
        double effectivePageHeight = pageHeight > 100 ? pageHeight : 1056;
        const double marginX = 32;
        const double marginY = 28;
        double usableWidth = Math.Max(420, effectivePageWidth - (marginX * 2));
        double usableHeight = Math.Max(400, effectivePageHeight - (marginY * 2));

        var (uniqueSongs, totalPerformances, consolidatedVenues) = CalculateSummaryStats(profileVenues, historyEntries);
        string venuesDisplay = string.IsNullOrWhiteSpace(consolidatedVenues) ? "—" : consolidatedVenues;

        var orderedEntries = (historyEntries ?? [])
            .OrderBy(e => e.SongTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Artist, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Column widths (sum = usableWidth)
        const double colNumWidth = 32;
        const double colSungWidth = 52;
        const double colKeyWidth = 72;
        const double colDateWidth = 82;
        double fixedColsWidth = colNumWidth + colSungWidth + colKeyWidth + colDateWidth;
        double flexibleWidth = Math.Max(180, usableWidth - fixedColsWidth);
        double colTitleWidth = Math.Floor(flexibleWidth * 0.44);
        double colArtistWidth = Math.Floor(flexibleWidth * 0.30);
        double colVenueWidth = Math.Max(60, flexibleWidth - colTitleWidth - colArtistWidth);
        double[] colWidths = [colNumWidth, colTitleWidth, colArtistWidth, colSungWidth, colVenueWidth, colKeyWidth, colDateWidth];

        // Row & section heights
        const double firstPageBannerHeight = 76;
        const double continuationBannerHeight = 28;
        const double tableHeaderHeight = 24;
        const double rowHeight = 20;
        const double footerHeight = 20;

        int firstPageMaxRows = Math.Max(1, (int)Math.Floor((usableHeight - firstPageBannerHeight - tableHeaderHeight - footerHeight - 8) / rowHeight));
        int nextPageMaxRows = Math.Max(1, (int)Math.Floor((usableHeight - continuationBannerHeight - tableHeaderHeight - footerHeight - 8) / rowHeight));

        var pageSlices = new List<(int StartIndex, List<SingerHistoryEntry> Rows)>();
        if (orderedEntries.Count == 0)
        {
            pageSlices.Add((0, []));
        }
        else
        {
            int idx = 0;
            int count = Math.Min(firstPageMaxRows, orderedEntries.Count);
            pageSlices.Add((0, orderedEntries.GetRange(0, count)));
            idx += count;
            while (idx < orderedEntries.Count)
            {
                int take = Math.Min(nextPageMaxRows, orderedEntries.Count - idx);
                pageSlices.Add((idx, orderedEntries.GetRange(idx, take)));
                idx += take;
            }
        }

        var fixedDoc = new FixedDocument();
        fixedDoc.DocumentPaginator.PageSize = new Size(effectivePageWidth, effectivePageHeight);

        string printedTimestamp = DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);
        int totalPages = pageSlices.Count;

        for (int pageIdx = 0; pageIdx < totalPages; pageIdx++)
        {
            var (startIndex, pageRows) = pageSlices[pageIdx];
            bool isFirstPage = pageIdx == 0;

            var fixedPage = new FixedPage
            {
                Width = effectivePageWidth,
                Height = effectivePageHeight,
                Background = Brushes.White
            };

            var rootGrid = new Grid
            {
                Width = usableWidth,
                Height = usableHeight
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Banner
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Table
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(footerHeight) }); // Footer

            // 1. Top Banner
            var bannerBorder = new Border
            {
                Background = BannerBgBrush,
                BorderBrush = BannerBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = isFirstPage ? new Thickness(12, 8, 12, 8) : new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 0, 6)
            };

            string titleSuffix = !string.IsNullOrWhiteSpace(customTitle) ? $"  ({customTitle.Trim()})" : string.Empty;
            if (isFirstPage)
            {
                var bannerStack = new StackPanel();
                bannerStack.Children.Add(new TextBlock
                {
                    Text = $"SINGER PERFORMANCE HISTORY — {singerName}{titleSuffix}",
                    FontFamily = PrintFontFamily,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = DarkTextBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                string rangePart = string.IsNullOrWhiteSpace(vocalRange) || string.Equals(vocalRange, "Any / Unspecified", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : $"   |   Vocal Range: {vocalRange}";

                bannerStack.Children.Add(new TextBlock
                {
                    Text = $"Unique Songs: {uniqueSongs}   |   Total Performances: {totalPerformances}{rangePart}   |   Printed: {printedTimestamp}",
                    FontFamily = PrintFontFamily,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = DarkTextBrush,
                    Margin = new Thickness(0, 2, 0, 1),
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                bannerStack.Children.Add(new TextBlock
                {
                    Text = $"Venues Performed: {venuesDisplay}",
                    FontFamily = PrintFontFamily,
                    FontSize = 10,
                    Foreground = MutedTextBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                bannerBorder.Child = bannerStack;
            }
            else
            {
                bannerBorder.Child = new TextBlock
                {
                    Text = $"SINGER PERFORMANCE HISTORY — {singerName}{titleSuffix}  (Continued)",
                    FontFamily = PrintFontFamily,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = DarkTextBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
            }

            Grid.SetRow(bannerBorder, 0);
            rootGrid.Children.Add(bannerBorder);

            // 2. Table Stack
            var tableStack = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical
            };

            tableStack.Children.Add(CreateFixedTableRow(
                colWidths,
                tableHeaderHeight,
                HeaderBgBrush,
                Brushes.White,
                FontWeights.Bold,
                10,
                ["#", "Song Title", "Artist", "# Sung", "Venue(s)", "Key / Speed", "Last Sung"],
                isHeader: true));

            if (pageRows.Count == 0)
            {
                var emptyBorder = new Border
                {
                    BorderBrush = CellBorderBrush,
                    BorderThickness = new Thickness(1, 0, 1, 1),
                    Padding = new Thickness(10, 8, 10, 8)
                };
                emptyBorder.Child = new TextBlock
                {
                    Text = "No performed songs recorded for this singer.",
                    FontFamily = PrintFontFamily,
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Foreground = MutedTextBrush
                };
                tableStack.Children.Add(emptyBorder);
            }
            else
            {
                for (int r = 0; r < pageRows.Count; r++)
                {
                    var entry = pageRows[r];
                    int rowNum = startIndex + r + 1;
                    int times = Math.Max(1, entry.TimesPerformed);
                    string venueText = string.IsNullOrWhiteSpace(entry.Venue) ? "—" : entry.Venue;
                    string keyText = string.IsNullOrWhiteSpace(entry.Key) ? "0" : entry.Key;
                    double tempoVal = entry.Tempo <= 0 ? 1.0 : entry.Tempo;
                    string keySpeedText = $"{keyText} / {tempoVal:0.0}x";
                    string dateText = entry.Timestamp == DateTime.MinValue
                        ? "—"
                        : entry.Timestamp.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

                    var rowBg = (r % 2 == 1) ? ZebraRowBrush : Brushes.White;
                    tableStack.Children.Add(CreateFixedTableRow(
                        colWidths,
                        rowHeight,
                        rowBg,
                        DarkTextBrush,
                        FontWeights.Normal,
                        9.5,
                        [
                            rowNum.ToString(CultureInfo.InvariantCulture),
                            entry.SongTitle ?? string.Empty,
                            string.IsNullOrWhiteSpace(entry.Artist) ? "—" : entry.Artist,
                            times.ToString(CultureInfo.InvariantCulture),
                            venueText,
                            keySpeedText,
                            dateText
                        ],
                        isHeader: false));
                }
            }

            Grid.SetRow(tableStack, 1);
            rootGrid.Children.Add(tableStack);

            // 3. Footer
            var footerText = new TextBlock
            {
                Text = $"Page {pageIdx + 1} of {totalPages}   •   {singerName} ({uniqueSongs} unique song{(uniqueSongs == 1 ? "" : "s")}, {totalPerformances} total performance{(totalPerformances == 1 ? "" : "s")})",
                FontFamily = PrintFontFamily,
                FontSize = 9,
                Foreground = MutedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(footerText, 2);
            rootGrid.Children.Add(footerText);

            FixedPage.SetLeft(rootGrid, marginX);
            FixedPage.SetTop(rootGrid, marginY);
            fixedPage.Children.Add(rootGrid);

            fixedPage.Measure(new Size(effectivePageWidth, effectivePageHeight));
            fixedPage.Arrange(new Rect(new System.Windows.Point(0, 0), new Size(effectivePageWidth, effectivePageHeight)));
            fixedPage.UpdateLayout();

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            fixedDoc.Pages.Add(pageContent);
        }

        return fixedDoc;
    }

    private static Border CreateFixedTableRow(
        double[] colWidths,
        double height,
        SolidColorBrush background,
        SolidColorBrush foreground,
        FontWeight defaultWeight,
        double fontSize,
        string[] values,
        bool isHeader)
    {
        var rowGrid = new Grid
        {
            Height = height,
            Background = background
        };

        for (int c = 0; c < colWidths.Length; c++)
        {
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(colWidths[c], GridUnitType.Pixel)
            });
        }

        HorizontalAlignment[] alignments =
        [
            HorizontalAlignment.Center, // #
            HorizontalAlignment.Left,   // Song Title
            HorizontalAlignment.Left,   // Artist
            HorizontalAlignment.Center, // # Sung
            HorizontalAlignment.Left,   // Venue(s)
            HorizontalAlignment.Center, // Key / Speed
            HorizontalAlignment.Center  // Last Sung
        ];

        for (int c = 0; c < colWidths.Length; c++)
        {
            bool isBoldCol = isHeader || c == 1 || c == 3;
            bool isMutedCol = !isHeader && (c == 0 || c == 5 || c == 6);

            var cellBorder = new Border
            {
                BorderBrush = CellBorderBrush,
                BorderThickness = new Thickness(c == 0 ? 1 : 0, isHeader ? 1 : 0, 1, 1),
                Padding = new Thickness(5, 0, 5, 0)
            };

            var tb = new TextBlock
            {
                Text = c < values.Length ? values[c] : string.Empty,
                FontFamily = PrintFontFamily,
                FontSize = fontSize,
                FontWeight = isBoldCol ? (isHeader || c == 3 ? FontWeights.Bold : FontWeights.SemiBold) : defaultWeight,
                Foreground = isHeader ? Brushes.White : (isMutedCol ? MutedTextBrush : foreground),
                HorizontalAlignment = alignments[c],
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            cellBorder.Child = tb;
            Grid.SetColumn(cellBorder, c);
            rowGrid.Children.Add(cellBorder);
        }

        return new Border
        {
            Child = rowGrid
        };
    }

    public static FlowDocument BuildFlowDocument(
        string singerName,
        string? profileVenues,
        string? vocalRange,
        string? customTitle,
        IReadOnlyList<SingerHistoryEntry> historyEntries,
        double pageWidth = 816,
        double pageHeight = 1056)
    {
        var (uniqueSongs, totalPerformances, consolidatedVenues) = CalculateSummaryStats(profileVenues, historyEntries);
        string venuesDisplay = string.IsNullOrWhiteSpace(consolidatedVenues) ? "—" : consolidatedVenues;

        double effectivePageWidth = pageWidth > 100 ? pageWidth : 816;
        double effectivePageHeight = pageHeight > 100 ? pageHeight : 1056;
        var pagePadding = new Thickness(32, 28, 32, 28);
        double usableWidth = Math.Max(420, effectivePageWidth - pagePadding.Left - pagePadding.Right);

        var doc = new FlowDocument
        {
            FontFamily = PrintFontFamily,
            FontSize = 10,
            Foreground = DarkTextBrush,
            Background = Brushes.White,
            PageWidth = effectivePageWidth,
            PageHeight = effectivePageHeight,
            PagePadding = pagePadding,
            ColumnWidth = usableWidth,
            ColumnGap = 0
        };

        // Top Summary Banner Section
        var headerSection = new Section
        {
            Background = BannerBgBrush,
            BorderBrush = BannerBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };

        string titleSuffix = !string.IsNullOrWhiteSpace(customTitle) ? $"  ({customTitle.Trim()})" : string.Empty;
        var titlePara = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 2)
        };
        titlePara.Inlines.Add(new Run($"SINGER PERFORMANCE HISTORY — {singerName}{titleSuffix}")
        {
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DarkTextBrush
        });
        headerSection.Blocks.Add(titlePara);

        string rangeDisplay = string.IsNullOrWhiteSpace(vocalRange) || string.Equals(vocalRange, "Any / Unspecified", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"   |   Vocal Range: {vocalRange}";

        var statsPara = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 2),
            FontSize = 10,
            Foreground = DarkTextBrush
        };
        statsPara.Inlines.Add(new Run("Unique Songs: ") { FontWeight = FontWeights.SemiBold });
        statsPara.Inlines.Add(new Run(uniqueSongs.ToString(CultureInfo.InvariantCulture)));
        statsPara.Inlines.Add(new Run("   |   Total Performances: ") { FontWeight = FontWeights.SemiBold });
        statsPara.Inlines.Add(new Run(totalPerformances.ToString(CultureInfo.InvariantCulture)));
        if (!string.IsNullOrEmpty(rangeDisplay))
        {
            statsPara.Inlines.Add(new Run(rangeDisplay));
        }
        statsPara.Inlines.Add(new Run($"   |   Printed: {DateTime.Now:MMM d, yyyy h:mm tt}") { Foreground = MutedTextBrush });
        headerSection.Blocks.Add(statsPara);

        var venuesPara = new Paragraph
        {
            Margin = new Thickness(0),
            FontSize = 10,
            Foreground = DarkTextBrush
        };
        venuesPara.Inlines.Add(new Run("Venues Performed: ") { FontWeight = FontWeights.SemiBold });
        venuesPara.Inlines.Add(new Run(venuesDisplay));
        headerSection.Blocks.Add(venuesPara);

        doc.Blocks.Add(headerSection);

        if (historyEntries.Count == 0)
        {
            var emptyPara = new Paragraph(new Run("No performed songs recorded for this singer."))
            {
                FontStyle = FontStyles.Italic,
                Foreground = MutedTextBrush,
                Margin = new Thickness(4, 12, 0, 0)
            };
            doc.Blocks.Add(emptyPara);
            return doc;
        }

        // Songs Table - Use explicit pixel column widths so WPF FlowDocument Table never collapses Star columns
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = CellBorderBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0)
        };

        double tableWidth = Math.Max(400, usableWidth - 2);
        const double colNumWidth = 30;
        const double colSungWidth = 48;
        const double colKeyWidth = 68;
        const double colDateWidth = 80;
        double fixedColsWidth = colNumWidth + colSungWidth + colKeyWidth + colDateWidth;
        double flexibleWidth = Math.Max(180, tableWidth - fixedColsWidth);
        double colTitleWidth = Math.Floor(flexibleWidth * 0.44);
        double colArtistWidth = Math.Floor(flexibleWidth * 0.30);
        double colVenueWidth = Math.Max(60, flexibleWidth - colTitleWidth - colArtistWidth);

        // 7 Columns: #, Song Title, Artist, # Sung, Venue(s), Key / Speed, Last Sung
        table.Columns.Add(new TableColumn { Width = new GridLength(colNumWidth, GridUnitType.Pixel) });
        table.Columns.Add(new TableColumn { Width = new GridLength(colTitleWidth, GridUnitType.Pixel) });
        table.Columns.Add(new TableColumn { Width = new GridLength(colArtistWidth, GridUnitType.Pixel) });
        table.Columns.Add(new TableColumn { Width = new GridLength(colSungWidth, GridUnitType.Pixel) });
        table.Columns.Add(new TableColumn { Width = new GridLength(colVenueWidth, GridUnitType.Pixel) });
        table.Columns.Add(new TableColumn { Width = new GridLength(colKeyWidth, GridUnitType.Pixel) });
        table.Columns.Add(new TableColumn { Width = new GridLength(colDateWidth, GridUnitType.Pixel) });

        var rowGroup = new TableRowGroup();

        // Header Row
        var headerRow = new TableRow { Background = HeaderBgBrush };
        headerRow.Cells.Add(CreateHeaderCell("#", TextAlignment.Center));
        headerRow.Cells.Add(CreateHeaderCell("Song Title", TextAlignment.Left));
        headerRow.Cells.Add(CreateHeaderCell("Artist", TextAlignment.Left));
        headerRow.Cells.Add(CreateHeaderCell("# Sung", TextAlignment.Center));
        headerRow.Cells.Add(CreateHeaderCell("Venue(s)", TextAlignment.Left));
        headerRow.Cells.Add(CreateHeaderCell("Key / Speed", TextAlignment.Center));
        headerRow.Cells.Add(CreateHeaderCell("Last Sung", TextAlignment.Center));
        rowGroup.Rows.Add(headerRow);

        // Sort alphabetically by Song Title (or keep most recent/most performed easy to scan)
        var orderedEntries = historyEntries
            .OrderBy(e => e.SongTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Artist, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < orderedEntries.Count; i++)
        {
            var entry = orderedEntries[i];
            var row = new TableRow
            {
                Background = (i % 2 == 1) ? ZebraRowBrush : Brushes.White
            };

            int times = Math.Max(1, entry.TimesPerformed);
            string venueText = string.IsNullOrWhiteSpace(entry.Venue) ? "—" : entry.Venue;
            string keyText = string.IsNullOrWhiteSpace(entry.Key) ? "0" : entry.Key;
            double tempoVal = entry.Tempo <= 0 ? 1.0 : entry.Tempo;
            string keySpeedText = $"{keyText} / {tempoVal:0.0}x";
            string dateText = entry.Timestamp == DateTime.MinValue
                ? "—"
                : entry.Timestamp.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

            row.Cells.Add(CreateBodyCell((i + 1).ToString(CultureInfo.InvariantCulture), TextAlignment.Center, MutedTextBrush));
            row.Cells.Add(CreateBodyCell(entry.SongTitle, TextAlignment.Left, DarkTextBrush, FontWeights.SemiBold));
            row.Cells.Add(CreateBodyCell(string.IsNullOrWhiteSpace(entry.Artist) ? "—" : entry.Artist, TextAlignment.Left, DarkTextBrush));
            row.Cells.Add(CreateBodyCell(times.ToString(CultureInfo.InvariantCulture), TextAlignment.Center, DarkTextBrush, FontWeights.Bold));
            row.Cells.Add(CreateBodyCell(venueText, TextAlignment.Left, DarkTextBrush));
            row.Cells.Add(CreateBodyCell(keySpeedText, TextAlignment.Center, MutedTextBrush));
            row.Cells.Add(CreateBodyCell(dateText, TextAlignment.Center, MutedTextBrush));

            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        return doc;
    }

    private static TableCell CreateHeaderCell(string text, TextAlignment alignment)
    {
        var para = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            TextAlignment = alignment,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        };
        return new TableCell(para)
        {
            Padding = new Thickness(5, 4, 5, 4),
            BorderBrush = CellBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
    }

    private static TableCell CreateBodyCell(string text, TextAlignment alignment, SolidColorBrush foreground, FontWeight? weight = null)
    {
        var para = new Paragraph(new Run(text ?? string.Empty))
        {
            Margin = new Thickness(0),
            TextAlignment = alignment,
            FontSize = 9.5,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = foreground
        };
        return new TableCell(para)
        {
            Padding = new Thickness(5, 3, 5, 3),
            BorderBrush = CellBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
    }
}
#endif

