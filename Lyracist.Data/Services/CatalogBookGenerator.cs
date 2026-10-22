// Edited on Oct 22, 2026 @ 08:42:00 -> Migrate PDF generation from PDFsharp to SkiaSharp
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

            var metadata = new SKDocumentPdfMetadata
            {
                Title = isKaraoke ? KaraokeCatalogTitle : MusicCatalogTitle,
                Author = "Lyracist System"
            };

            const float pageWidth = 612f;
            const float pageHeight = 792f;

            using var arialBold = SKTypeface.FromFamilyName(ArialFontFamily, SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            using var arialRegular = SKTypeface.FromFamilyName(ArialFontFamily, SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright) ?? SKTypeface.Default;

            using var titleFont = new SKFont(arialBold, 14);
            using var headerFont = new SKFont(arialBold, 10);
            using var dataFont = new SKFont(arialRegular, 9);
            using var footerFont = new SKFont(arialRegular, 8);

            using var darkSlateGrayPaint = new SKPaint { Color = new SKColor(47, 79, 79), IsAntialias = true };
            using var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var dimGrayPaint = new SKPaint { Color = new SKColor(105, 105, 105), IsAntialias = true };
            using var grayPaint = new SKPaint { Color = new SKColor(128, 128, 128), IsAntialias = true };
            using var ghostWhiteFillPaint = new SKPaint { Color = new SKColor(248, 248, 255), Style = SKPaintStyle.Fill };
            using var lightGrayStrokePaint = new SKPaint { Color = SKColors.LightGray, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            using var whiteSmokeStrokePaint = new SKPaint { Color = new SKColor(245, 245, 245), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

            using var context = new LyracistDbContext();
            context.Database.Migrate();
            var songs = context.Songs
                .Where(s => s.IsKaraoke == isKaraoke)
                .OrderBy(s => s.Artist)
                .ThenBy(s => s.Title)
                .ToList();

            using var stream = File.Create(filePath);
            using var document = SKDocument.CreatePdf(stream, metadata);

            int pageNumber = 0;
            bool hasOpenPage = false;
            SKCanvas canvas = null!;
            float yPos = 75f;

            void AddNewPage()
            {
                if (hasOpenPage)
                {
                    document.EndPage();
                }
                canvas = document.BeginPage(pageWidth, pageHeight);
                hasOpenPage = true;
                pageNumber++;

                // Draw title
                DrawText(canvas, isKaraoke ? KaraokeCatalogTitle : MusicCatalogTitle, 40, 30, 20, titleFont, darkSlateGrayPaint);

                // Draw table header
                canvas.DrawRect(40, 55, 532, 20, ghostWhiteFillPaint);
                canvas.DrawRect(40, 55, 532, 20, lightGrayStrokePaint);

                DrawText(canvas, "Artist", 45, 55, 20, headerFont, blackPaint);
                DrawText(canvas, "Title", 195, 55, 20, headerFont, blackPaint);
                DrawText(canvas, "Length", 415, 55, 20, headerFont, blackPaint);
                DrawText(canvas, "Genre", 465, 55, 20, headerFont, blackPaint);

                // Draw page number footer
                canvas.DrawLine(40, 750, 572, 750, lightGrayStrokePaint);
                DrawTextRight(canvas, $"Page {pageNumber}", 572, 755, 15, footerFont, dimGrayPaint);

                yPos = 75f;
            }

            foreach (var song in songs)
            {
                if (!hasOpenPage || yPos + 18 > 740)
                {
                    AddNewPage();
                }

                string artist = Truncate(song.Artist, 28);
                string title = Truncate(song.Title, 48);
                string durationStr = TimeSpan.FromSeconds(song.Duration).ToString(@"mm\:ss");
                string genre = Truncate(song.Genre, 20);

                DrawText(canvas, artist, 45, yPos, 18, dataFont, blackPaint);
                DrawText(canvas, title, 195, yPos, 18, dataFont, blackPaint);
                DrawText(canvas, durationStr, 415, yPos, 18, dataFont, blackPaint);
                DrawText(canvas, genre, 465, yPos, 18, dataFont, blackPaint);

                canvas.DrawLine(40, yPos + 18, 572, yPos + 18, whiteSmokeStrokePaint);
                yPos += 18;
            }

            if (pageNumber == 0)
            {
                AddNewPage();
                DrawText(canvas, "No tracks found in the database matching this type.", 45, yPos, 18, dataFont, grayPaint);
            }

            if (hasOpenPage)
            {
                document.EndPage();
            }
            document.Close();

            return filePath;
        }

        private static void DrawText(SKCanvas canvas, string text, float x, float rowY, float rowHeight, SKFont font, SKPaint paint)
        {
            if (string.IsNullOrEmpty(text)) return;
            font.GetFontMetrics(out var metrics);
            float textHeight = Math.Abs(metrics.Ascent) + metrics.Descent;
            float verticalOffset = (rowHeight - textHeight) / 2f;
            float baseline = rowY + verticalOffset + Math.Abs(metrics.Ascent);
            canvas.DrawText(text, x, baseline, SKTextAlign.Left, font, paint);
        }

        private static void DrawTextRight(SKCanvas canvas, string text, float rightX, float rowY, float rowHeight, SKFont font, SKPaint paint)
        {
            if (string.IsNullOrEmpty(text)) return;
            font.GetFontMetrics(out var metrics);
            float textHeight = Math.Abs(metrics.Ascent) + metrics.Descent;
            float verticalOffset = (rowHeight - textHeight) / 2f;
            float baseline = rowY + verticalOffset + Math.Abs(metrics.Ascent);
            canvas.DrawText(text, rightX, baseline, SKTextAlign.Right, font, paint);
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