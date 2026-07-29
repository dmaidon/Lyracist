// Created on Jul 28, 2026 @ 19:34:00 -> Add static constructor for PDFsharp font settings and EF Core migration check
using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Lyracist.Data.Models;

namespace Lyracist.Data.Services
{
    public static class CatalogBookGenerator
    {
        static CatalogBookGenerator()
        {
            try
            {
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new SimpleFontResolver();
            }
            catch
            {
                // Guard against multiple initialization attempts
            }
        }

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
                        new Text(isKaraoke ? "Karaoke Catalog" : "Music Catalog")
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
                        new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                        new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                        new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                        new RightBorder() { Val = BorderValues.Single, Size = 4, Color = "D3D3D3" },
                        new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4, Color = "E0E0E0" },
                        new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4, Color = "E0E0E0" }
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

            PdfDocument document = new();
            document.Info.Title = isKaraoke ? "Karaoke Catalog" : "Music Catalog";
            document.Info.Author = "Lyracist System";

            XFont titleFont = new("Arial", 14, XFontStyleEx.Bold);
            XFont headerFont = new("Arial", 10, XFontStyleEx.Bold);
            XFont dataFont = new("Arial", 9, XFontStyleEx.Regular);
            XFont footerFont = new("Arial", 8, XFontStyleEx.Regular);

            XStringFormat leftAlign = new() { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };

            using var context = new LyracistDbContext();
            context.Database.Migrate();
            var songs = context.Songs
                .Where(s => s.IsKaraoke == isKaraoke)
                .OrderBy(s => s.Artist)
                .ThenBy(s => s.Title)
                .ToList();

            int pageNumber = 0;
            PdfPage page = null!;
            XGraphics gfx = null!;
            double yPos = 800; // Force new page at start

            void AddNewPage()
            {
                page = document.AddPage();
                page.Size = PdfSharp.PageSize.Letter;
                gfx = XGraphics.FromPdfPage(page);
                pageNumber++;

                // Draw title
                gfx.DrawString(isKaraoke ? "Karaoke Catalog" : "Music Catalog", titleFont, XBrushes.DarkSlateGray, new XRect(40, 30, 532, 20), leftAlign);

                // Draw table header
                gfx.DrawRectangle(XBrushes.GhostWhite, new XRect(40, 55, 532, 20));
                gfx.DrawRectangle(XPens.LightGray, new XRect(40, 55, 532, 20));

                gfx.DrawString("Artist", headerFont, XBrushes.Black, new XRect(45, 55, 145, 20), leftAlign);
                gfx.DrawString("Title", headerFont, XBrushes.Black, new XRect(195, 55, 215, 20), leftAlign);
                gfx.DrawString("Length", headerFont, XBrushes.Black, new XRect(415, 55, 45, 20), leftAlign);
                gfx.DrawString("Genre", headerFont, XBrushes.Black, new XRect(465, 55, 107, 20), leftAlign);

                // Draw page number footer
                gfx.DrawLine(XPens.LightGray, 40, 750, 572, 750);
                gfx.DrawString($"Page {pageNumber}", footerFont, XBrushes.DimGray, new XRect(40, 755, 532, 15), new XStringFormat { Alignment = XStringAlignment.Far });

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

                gfx.DrawString(artist, dataFont, XBrushes.Black, new XRect(45, yPos, 145, 18), leftAlign);
                gfx.DrawString(title, dataFont, XBrushes.Black, new XRect(195, yPos, 215, 18), leftAlign);
                gfx.DrawString(durationStr, dataFont, XBrushes.Black, new XRect(415, yPos, 45, 18), leftAlign);
                gfx.DrawString(genre, dataFont, XBrushes.Black, new XRect(465, yPos, 107, 18), leftAlign);

                gfx.DrawLine(XPens.WhiteSmoke, 40, yPos + 18, 572, yPos + 18);
                yPos += 18;
            }

            if (pageNumber == 0)
            {
                AddNewPage();
                gfx.DrawString("No tracks found in the database matching this type.", dataFont, XBrushes.Gray, new XRect(45, yPos, 500, 18), leftAlign);
            }

            document.Save(filePath);
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

        public class SimpleFontResolver : PdfSharp.Fonts.IFontResolver
        {
            public byte[] GetFont(string faceName)
            {
                string fontFile = faceName switch
                {
                    "Arial-Bold" => "arialbd.ttf",
                    "Arial-Italic" => "ariali.ttf",
                    "Arial-BoldItalic" => "arialbi.ttf",
                    _ => "arial.ttf"
                };

                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", fontFile);
                if (File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }
                
                // Fallback to regular arial
                fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arial.ttf");
                if (File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }

                return null;
            }

            public PdfSharp.Fonts.FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
                {
                    if (isBold && isItalic)
                        return new PdfSharp.Fonts.FontResolverInfo("Arial-BoldItalic");
                    else if (isBold)
                        return new PdfSharp.Fonts.FontResolverInfo("Arial-Bold");
                    else if (isItalic)
                        return new PdfSharp.Fonts.FontResolverInfo("Arial-Italic");
                    else
                        return new PdfSharp.Fonts.FontResolverInfo("Arial");
                }
                return null;
            }
        }
    }
}
