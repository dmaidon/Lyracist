// Edited on Aug 25, 2026 @ 06:15:00 -> Fix S1192 duplicated string literals with private constants
using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using Lyracist.Data.Models;

namespace Lyracist.Data.Services
{
    public static class CatalogBookGenerator
    {
        private const string KaraokeCatalogTitle = "Karaoke Catalog";
        private const string MusicCatalogTitle = "Music Catalog";
        private const string BorderColor = "D3D3D3";
        private const string InsideBorderColor = "E0E0E0";
        private const string ArialFontFamily = "Arial";

        private static string ReportsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");

        public static string GenerateDocx(bool isKaraoke)
        {
            Directory.CreateDirectory(ReportsDirectory);
            string catalogName = isKaraoke ? "Karaoke_Catalog" : "Music_Catalog";
            string fileName = $"{catalogName}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string filePath = Path.Combine(ReportsDirectory, fileName);

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = new Body();
                mainPart.Document.Append(body);

                // Add a title paragraph
                Paragraph titlePara = new Paragraph(
                    new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
                    new Run(
                        new RunProperties(new Bold(), new FontSize() { Val = "32" }), // 16pt font size
                        new Text(isKaraoke ? KaraokeCatalogTitle : MusicCatalogTitle)
                    )
                );
                body.Append(titlePara);

                // Add a blank space
                body.Append(new Paragraph(new Run(new Text(""))));

                // Create Table
                Table table = new Table();

                // Style borders & widths
                TableProperties tblProp = new TableProperties(
                    new TableBorders(
                        new TopBorder() { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new RightBorder() { Val = BorderValues.Single, Size = 4, Color = BorderColor },
                        new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4, Color = InsideBorderColor },
                        new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4, Color = InsideBorderColor }
                    ),
                    new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }
                );
                table.AppendChild(tblProp);

                // Table Header (repeats on each page)
                TableRow headerRow = new TableRow(new TableRowProperties(new TableHeader()));
                headerRow.Append(
                    CreateCell("Artist", "2800", isHeader: true),
                    CreateCell("Title", "4200", isHeader: true),
                    CreateCell("Length", "960", isHeader: true),
                    CreateCell("Genre", "1400", isHeader: true)
                );
                table.Append(headerRow);

                using (var context = new LyracistDbContext())
                {
                    context.Database.Migrate();
                    var songs = context.Songs
                        .Where(s => s.IsKaraoke == isKaraoke)
                        .OrderBy(s => s.Artist)
                        .ThenBy(s => s.Title)
                        .ToList();

                    foreach (var song in songs)
                    {
                        TableRow row = new TableRow();
                        string durationStr = TimeSpan.FromSeconds(song.Duration).ToString(@"mm\:ss");
                        row.Append(
                            CreateCell(song.Artist, "2800", isHeader: false),
                            CreateCell(song.Title, "4200", isHeader: false),
                            CreateCell(durationStr, "960", isHeader: false),
                            CreateCell(song.Genre, "1400", isHeader: false)
                        );
                        table.Append(row);
                    }
                }

                body.Append(table);

                // Add footer with Page [PageNumber] of [TotalPages]
                FooterPart footerPart = mainPart.AddNewPart<FooterPart>();
                string footerPartId = mainPart.GetIdOfPart(footerPart);

                footerPart.Footer = new Footer(
                    new Paragraph(
                        new ParagraphProperties(new Justification() { Val = JustificationValues.Right }),
                        new Run(new RunProperties(new FontSize() { Val = "18" }), new Text("Page ")),
                        new SimpleField() { Instruction = "PAGE" },
                        new Run(new RunProperties(new FontSize() { Val = "18" }), new Text(" of ")),
                        new SimpleField() { Instruction = "NUMPAGES" }
                    )
                );

                SectionProperties sectionProperties = new SectionProperties(
                    new FooterReference() { Type = HeaderFooterValues.Default, Id = footerPartId }
                );
                body.Append(sectionProperties);
            }

            return filePath;
        }

        public static string GeneratePdf(bool isKaraoke)
        {
            Directory.CreateDirectory(ReportsDirectory);
            string catalogName = isKaraoke ? "Karaoke_Catalog" : "Music_Catalog";
            string fileName = $"{catalogName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(ReportsDirectory, fileName);

            const float pageWidth = 612f;  // US Letter in points
            const float pageHeight = 792f;

            using var context = new LyracistDbContext();
            context.Database.Migrate();
            var songs = context.Songs
                .Where(s => s.IsKaraoke == isKaraoke)
                .OrderBy(s => s.Artist)
                .ThenBy(s => s.Title)
                .ToList();

            using var boldTypeface = SKTypeface.FromFamilyName(ArialFontFamily, SKFontStyle.Bold);
            using var regularTypeface = SKTypeface.FromFamilyName(ArialFontFamily, SKFontStyle.Normal);
            using var titleFont = new SKFont(boldTypeface, 14);
            using var headerFont = new SKFont(boldTypeface, 10);
            using var dataFont = new SKFont(regularTypeface, 9);
            using var footerFont = new SKFont(regularTypeface, 8);

            using var darkSlateGray = new SKPaint { Color = SKColors.DarkSlateGray, IsAntialias = true };
            using var black = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var dimGray = new SKPaint { Color = SKColors.DimGray, IsAntialias = true };
            using var gray = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
            using var ghostWhiteFill = new SKPaint { Color = SKColors.GhostWhite, Style = SKPaintStyle.Fill };
            using var lightGrayStroke = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            using var whiteSmokeStroke = new SKPaint { Color = SKColors.WhiteSmoke, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

            var metadata = new SKDocumentPdfMetadata
            {
                Title = isKaraoke ? KaraokeCatalogTitle : MusicCatalogTitle,
                Author = "Lyracist System"
            };

            using (var stream = File.Create(filePath))
            using (var document = SKDocument.CreatePdf(stream, metadata))
            {
                int pageNumber = 0;
                SKCanvas canvas = null!;
                float yPos = 800; // Force new page at start

                // Draws text vertically centered in a row of the given height starting at top.
                static void DrawCentered(SKCanvas c, string text, SKFont font, SKPaint paint, float x, float top, float height, SKTextAlign align = SKTextAlign.Left)
                {
                    float baseline = top + (height / 2f) + (font.Size * 0.35f);
                    c.DrawText(text, x, baseline, align, font, paint);
                }

                void AddNewPage()
                {
                    if (pageNumber > 0)
                    {
                        document.EndPage();
                    }

                    canvas = document.BeginPage(pageWidth, pageHeight);
                    pageNumber++;

                    // Draw title
                    DrawCentered(canvas, isKaraoke ? KaraokeCatalogTitle : MusicCatalogTitle, titleFont, darkSlateGray, 40, 30, 20);

                    // Draw table header
                    var headerRect = new SKRect(40, 55, 572, 75);
                    canvas.DrawRect(headerRect, ghostWhiteFill);
                    canvas.DrawRect(headerRect, lightGrayStroke);

                    DrawCentered(canvas, "Artist", headerFont, black, 45, 55, 20);
                    DrawCentered(canvas, "Title", headerFont, black, 195, 55, 20);
                    DrawCentered(canvas, "Length", headerFont, black, 415, 55, 20);
                    DrawCentered(canvas, "Genre", headerFont, black, 465, 55, 20);

                    // Draw page number footer
                    canvas.DrawLine(40, 750, 572, 750, lightGrayStroke);
                    canvas.DrawText($"Page {pageNumber}", 572, 755 + footerFont.Size, SKTextAlign.Right, footerFont, dimGray);

                    yPos = 75;
                }

                foreach (var song in songs)
                {
                    if (yPos + 18 > 740)
                    {
                        AddNewPage();
                    }

                    string artist = Truncate(song.Artist, 28);
                    string title = Truncate(song.Title, 48);
                    string durationStr = TimeSpan.FromSeconds(song.Duration).ToString(@"mm\:ss");
                    string genre = Truncate(song.Genre, 20);

                    DrawCentered(canvas, artist, dataFont, black, 45, yPos, 18);
                    DrawCentered(canvas, title, dataFont, black, 195, yPos, 18);
                    DrawCentered(canvas, durationStr, dataFont, black, 415, yPos, 18);
                    DrawCentered(canvas, genre, dataFont, black, 465, yPos, 18);

                    canvas.DrawLine(40, yPos + 18, 572, yPos + 18, whiteSmokeStroke);
                    yPos += 18;
                }

                if (pageNumber == 0)
                {
                    AddNewPage();
                    DrawCentered(canvas, "No tracks found in the database matching this type.", dataFont, gray, 45, yPos, 18);
                }

                document.EndPage();
                document.Close();
            }

            return filePath;
        }

        public static string GenerateTxt(bool isKaraoke)
        {
            Directory.CreateDirectory(ReportsDirectory);
            string catalogName = isKaraoke ? "Karaoke_Catalog" : "Music_Catalog";
            string fileName = $"{catalogName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(ReportsDirectory, fileName);

            using var context = new LyracistDbContext();
            context.Database.Migrate();
            var songs = context.Songs
                .Where(s => s.IsKaraoke == isKaraoke)
                .OrderBy(s => s.Artist)
                .ThenBy(s => s.Title)
                .ToList();

            using (var writer = new StreamWriter(filePath))
            {
                string title = isKaraoke ? KaraokeCatalogTitle : MusicCatalogTitle;
                writer.WriteLine(title);
                writer.WriteLine(new string('=', title.Length));
                writer.WriteLine();

                // Table Header
                writer.WriteLine("{0,-30} {1,-50} {2,-8} {3,-20}", "Artist", "Title", "Length", "Genre");
                writer.WriteLine(new string('-', 112));

                foreach (var song in songs)
                {
                    string artist = Truncate(song.Artist, 30);
                    string songTitle = Truncate(song.Title, 50);
                    string durationStr = TimeSpan.FromSeconds(song.Duration).ToString(@"mm\:ss");
                    string genre = Truncate(song.Genre, 20);

                    writer.WriteLine("{0,-30} {1,-50} {2,-8} {3,-20}", artist, songTitle, durationStr, genre);
                }

                if (songs.Count == 0)
                {
                    writer.WriteLine("No tracks found in the database matching this type.");
                }
            }

            return filePath;
        }

        private static TableCell CreateCell(string text, string width, bool isHeader)
        {
            Paragraph para = new Paragraph();
            Run run = new Run(new Text(text ?? string.Empty));
            if (isHeader)
            {
                run.RunProperties = new RunProperties(new Bold(), new FontSize() { Val = "20" }); // 10pt
            }
            else
            {
                run.RunProperties = new RunProperties(new FontSize() { Val = "18" }); // 9pt
            }
            para.Append(run);

            TableCell cell = new TableCell(
                new TableCellProperties(new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = width }),
                para
            );
            return cell;
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : $"{text.Substring(0, maxLength - 3)}...";
        }
    }
}
