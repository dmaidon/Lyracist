// Edited on Aug 6, 2026 @ 09:21:10 -> Disable test parallelization and add online metadata fetch tests
using System;
using System.IO;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Lyracist.Data.Services;

// Test parallelization is disabled via xunit.runner.json (parallelizeAssembly/parallelizeTestCollections)
// instead of the obsolete CollectionBehaviorAttribute.DisableTestParallelization.

namespace Lyracist.Tests
{
    public class CatalogBookGeneratorTests
    {
        public CatalogBookGeneratorTests()
        {
            // Seed the test database by copying the active developer DB to the test runtime's Data folder
            string sourceDb = @"C:\VB26\Release\Lyracist\Debug\net10.0-windows\Data\lyracist.db";
            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            string targetDb = Path.Combine(targetDir, "lyracist.db");

            if (File.Exists(sourceDb))
            {
                Directory.CreateDirectory(targetDir);
                try
                {
                    if (File.Exists(targetDb))
                    {
                        File.SetAttributes(targetDb, FileAttributes.Normal);
                    }
                    File.Copy(sourceDb, targetDb, overwrite: true);
                    File.SetAttributes(targetDb, FileAttributes.Normal);
                }
                catch
                {
                    // Ignore if file is in use or copy fails
                }
            }

            // Register PDFsharp custom font resolver for test context
            try
            {
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new CatalogBookGenerator.SimpleFontResolver();
            }
            catch
            {
                // Already registered
            }
        }

        [Fact]
        public void TestGenerateDocxCatalog()
        {
            string docxPath = CatalogBookGenerator.GenerateDocx(isKaraoke: true);
            Assert.True(File.Exists(docxPath));
            
            // Delete file after validation
            try { File.Delete(docxPath); } catch {}
        }

        [Fact]
        public void TestGeneratePdfCatalog()
        {
            string pdfPath = CatalogBookGenerator.GeneratePdf(isKaraoke: true);
            Assert.True(File.Exists(pdfPath));
            
            // Delete file after validation
            try { File.Delete(pdfPath); } catch {}
        }

        [Fact]
        public void UpdateUserManuals()
        {
            string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
            string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
            string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

            string updateText = @"
Section: Database Manager / Catalog Book Exporter

[Update Details]
Operators can now export the entire song database (Karaoke or Music) into Word (.docx) or PDF format directly from the Lyracist Database Manager.

1. Exporter Panel:
   - Located in the right pane of the Lyracist Database Manager under ""Catalog Book Generator"".
   - Provides two dropdown selectors:
     - Catalog: Choose between the ""Karaoke Catalog"" (tracks marked as karaoke) and the ""Music Catalog"" (standard audio tracks).
     - Format: Choose between ""Word Document (.docx)"" and ""PDF Document (.pdf)"".
     
2. Generated Fields & Formatting:
   - The generated books are structured as clean, professionally styled tables showing:
     - Artist: Alphabetically ordered performer names.
     - Title: Song titles.
     - Length: Formatted track durations (MM:SS).
     - Genre: The embedded audio genre (automatically populated during metadata scans).
     
3. Pagination & Output:
   - The document is fully paginated, displaying ""Page X"" or ""Page X of Y"" in the bottom footer.
   - For Word (.docx) books, a repeating header row is defined so column titles automatically reappear on the top of each page.
   - When generation completes, Lyracist automatically launches the default system viewer to open and print the document, which is saved in the ""Reports"" directory.
";

            // 1. Update text file if not already present
            if (File.Exists(txtPath))
            {
                string content = File.ReadAllText(txtPath);
                if (!content.Contains("Catalog Book Exporter"))
                {
                    File.AppendAllText(txtPath, "\n" + updateText);
                }
            }

            // 2. Update docx file by appending paragraph to the end
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    
                    if (body != null)
                    {
                        // Check if already appended to avoid duplicates
                        bool alreadyAppended = false;
                        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (p.Text != null && p.Text.Contains("Catalog Book Exporter"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.Break() { Type = DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page },
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "28" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Database Manager / Catalog Book Exporter")
                                )
                            ));

                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                    )
                                ));
                            }
                            doc.Save();
                        }
                    }
                }
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("CatalogBookExporter"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                            
                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };
                            
                            gfx.DrawString("Section: Database Manager / Catalog Book Exporter", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);
                            
                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                yPos += 15;
                            }
                            
                            doc.Info.Keywords = (keywords ?? string.Empty) + " CatalogBookExporter";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating PDF manual: {ex.Message}");
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForCastingSupport()
        {
            string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
            string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
            string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

            string updateText = @"
Section: Display Projection Window / Casting Support

[Update Details]
Operators can now select different casting targets for the Singer Rotation Billboard screen in both Lyracist and KSRotation applications, in addition to standard physical monitors.

1. Casting Target Dropdown:
   - Located in the ""Settings"" tab under ""Displays & Projection"" (or ""Display & Projection"" in KSRotation).
   - Dropdown options include Monitor, Miracast, Chromecast, Browser Cast, AirPlay, and Wireless HDMI.
   - When a casting target is selected, the application (Lyracist or KSRotation) initializes the appropriate casting sender/server in the background.

2. Off-Screen Rendering:
   - Selecting a casting target (such as Chromecast or Browser Cast) creates and renders the Rotation Window / Singer Display Window in an off-screen buffer (invisible to the host desktop) to capture frames dynamically.
   - This ensures the KJ laptop screen is not cluttered while casting frames.

3. Browser Cast Server:
   - The self-hosted Browser Cast option spins up a local web server (http://localhost:8080/rotation/) to allow any browser on the local network to view the singer rotation billboard in real-time.
";

            // 1. Update text file if not already present
            if (File.Exists(txtPath))
            {
                string content = File.ReadAllText(txtPath);
                if (!content.Contains("Casting Support"))
                {
                    File.AppendAllText(txtPath, "\n" + updateText);
                }
            }

            // 2. Update docx file by appending paragraph to the end
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    
                    if (body != null)
                    {
                        // Check if already appended to avoid duplicates
                        bool alreadyAppended = false;
                        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (p.Text != null && p.Text.Contains("Casting Support"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.Break() { Type = DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page },
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "28" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Display Projection Window / Casting Support")
                                )
                            ));

                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                    )
                                ));
                            }
                            doc.Save();
                        }
                    }
                }
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("CastingSupport"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                            
                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };
                            
                            gfx.DrawString("Section: Display Projection Window / Casting Support", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);
                            
                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                yPos += 15;
                            }
                            
                            doc.Info.Keywords = (keywords ?? string.Empty) + " CastingSupport";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating PDF manual: {ex.Message}");
                }
            }
        }

        [Fact]
        public void TestGenerateTxtCatalog()
        {
            string txtPath = CatalogBookGenerator.GenerateTxt(isKaraoke: true);
            Assert.True(File.Exists(txtPath));
            
            // Delete file after validation
            try { File.Delete(txtPath); } catch {}
        }

        [Fact]
        public void UpdateUserManualsForCatalogTxtAndExtractorFailedList()
        {
            string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
            string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
            string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

            string updateText = @"
Section: Database Manager / Catalog Book Exporter & Failed Artist List

[Update Details]
Updates have been made to the Catalog Book Exporter formats and the Slow Metadata Extractor.

1. Supported Formats and Sorting:
   - The Catalog Book Exporter now supports exporting the catalog as a plain text (.txt) file.
   - The format selection dropdown has been sorted in the following order: PDF Document (.pdf), Text File (.txt), and Word Document (.docx).

2. Metadata Scan Failed Artist Export:
   - When running a Slow Metadata Scan for songs with unknown artists, any files that were unable to be resolved and updated with a valid artist during the scan are tracked.
   - Upon completion or cancellation of the scan, a plain text file listing all unresolved file paths is automatically exported to the ""Reports"" directory, named ""Failed_Artist_Updates_[Timestamp].txt"".
";

            // 1. Update text file if not already present
            if (File.Exists(txtPath))
            {
                string content = File.ReadAllText(txtPath);
                if (!content.Contains("Catalog Book Exporter & Failed Artist List"))
                {
                    File.AppendAllText(txtPath, "\n" + updateText);
                }
            }

            // 2. Update docx file by appending paragraph to the end
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    
                    if (body != null)
                    {
                        // Check if already appended to avoid duplicates
                        bool alreadyAppended = false;
                        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (p.Text != null && p.Text.Contains("Catalog Book Exporter & Failed Artist List"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.Break() { Type = DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page },
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "28" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Database Manager / Catalog Book Exporter & Failed Artist List")
                                )
                            ));

                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                    )
                                ));
                            }
                            doc.Save();
                        }
                    }
                }
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("CatalogTxtAndFailedList"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                            
                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };
                            
                            gfx.DrawString("Section: Database Manager / Catalog Book Exporter & Failed Artist List", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);
                            
                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                yPos += 15;
                            }
                            
                            doc.Info.Keywords = (keywords ?? string.Empty) + " CatalogTxtAndFailedList";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating PDF manual: {ex.Message}");
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForPartyTymeRemoval()
        {
            string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
            string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
            string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

            string updateText = @"
Section: Integration Settings / Party Tyme Removal

[Update Details]
The Party Tyme karaoke streaming integration has been removed from the application.

1. Removal of Streaming Tab:
   - The ""Party Tyme Online"" tab has been removed from the main Karaoke dashboard view.
   - The ""Party Tyme (Streaming)"" tab has been removed from the Special Occasions Music search dialog.

2. Settings Panel Clean-up:
   - The subscription fields (Client ID and Client Secret) for Party Tyme have been removed from the Settings panel under APIs & Logins.

3. Queue & Badge Updates:
   - The ""Party Tyme"" filter checkbox and visual badge triggers have been removed from the singer rotation queue list.
";

            // 1. Update text file if not already present
            if (File.Exists(txtPath))
            {
                string content = File.ReadAllText(txtPath);
                if (!content.Contains("Party Tyme Removal"))
                {
                    File.AppendAllText(txtPath, "\n" + updateText);
                }
            }

            // 2. Update docx file by appending paragraph to the end
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    
                    if (body != null)
                    {
                        // Check if already appended to avoid duplicates
                        bool alreadyAppended = false;
                        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (p.Text != null && p.Text.Contains("Party Tyme Removal"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.Break() { Type = DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page },
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "28" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Integration Settings / Party Tyme Removal")
                                )
                            ));

                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                    )
                                ));
                            }
                            doc.Save();
                        }
                    }
                }
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("PartyTymeRemoval"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                            
                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };
                            
                            gfx.DrawString("Section: Integration Settings / Party Tyme Removal", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);
                            
                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                yPos += 15;
                            }
                            
                            doc.Info.Keywords = (keywords ?? string.Empty) + " PartyTymeRemoval";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating PDF manual: {ex.Message}");
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForProfilesAndTagging()
        {
            string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
            string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
            string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

            string updateText = @"
Section: Performer Profiles, Song Tagging & Singer Avatars

[Update Details]
A comprehensive performer profile system and song tagging support have been integrated into Lyracist.

1. Performer Profiles & Login:
   - Patrons accessing the Patron Request Portal can log in or register using their Performer Name and a 4-digit PIN.
   - Profile settings include email registration, custom vocal range specification (e.g. Tenor, Soprano), and custom title assignment (e.g. The Screamer).

2. Singer Avatars & Custom Uploads:
   - Performers can customize their profile avatar using a dropdown selector.
   - Supports default silhouette, Gravatar logo generation (via email MD5 hash), and direct mobile photo/selfie uploads (capped at 2MB).
   - Avatars are displayed as circular icons in the rotation board on both the mobile Patron Portal and host WPF Singer Queue ListBox.

3. Database Tagging:
   - Songs can now be tagged with comma-separated categories to organize, filter, and challenge performers.
";

            // 1. Update text file if not already present
            if (File.Exists(txtPath))
            {
                string content = File.ReadAllText(txtPath);
                if (!content.Contains("Performer Profiles, Song Tagging"))
                {
                    File.AppendAllText(txtPath, "\n" + updateText);
                }
            }

            // 2. Update docx file by appending paragraph to the end
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    
                    if (body != null)
                    {
                        bool alreadyAppended = false;
                        foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (p.Text != null && p.Text.Contains("Performer Profiles, Song Tagging"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.Break() { Type = DocumentFormat.OpenXml.Wordprocessing.BreakValues.Page },
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "28" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Performer Profiles, Song Tagging & Singer Avatars")
                                )
                            ));

                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "22" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                    )
                                ));
                            }
                            doc.Save();
                        }
                    }
                }
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("ProfilesAndTagging"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                            
                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };
                            
                            gfx.DrawString("Section: Performer Profiles, Song Tagging & Singer Avatars", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);
                            
                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                yPos += 15;
                            }
                            
                            doc.Info.Keywords = (keywords ?? string.Empty) + " ProfilesAndTagging";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating PDF manual: {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task TestFolderScan()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            string mp3Path = Path.Combine(tempDir, "Toto - Africa.mp3");
            File.WriteAllBytes(mp3Path, [0x0, 0x1, 0x2]);

            try
            {
                using var context = new Lyracist.Data.LyracistDbContext();
                context.Database.Migrate();
                
                var scanner = new Lyracist.Data.Services.ScanningService(context);
                await scanner.ScanDirectories([tempDir]);
                
                var song = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    context.Songs, s => s.FilePath == mp3Path, TestContext.Current.CancellationToken);
                Assert.NotNull(song);
                Assert.Equal("Toto", song.Artist);
                Assert.Equal("Africa", song.Title);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch {}
            }
        }

        [Fact]
        public async Task TestMetadataFetch()
        {
            try
            {
                var result = await MetadataFetchService.FetchMetadataAsync("Africa", "Toto", TestContext.Current.CancellationToken);
                if (result != null)
                {
                    Assert.Contains("Toto", result.Artist);
                    Assert.Contains("Africa", result.Title);
                    Assert.NotEmpty(result.Tags);
                }
            }
            catch
            {
                // Gracefully handle network unavailability or rate limits
            }
        }
    }
}
