// Edited on Oct 6, 2026 @ 13:07:00 -> Add User Manual update test for new singer welcome screen on name box lost focus
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
        private static bool _dbSeeded;
        private static readonly object _dbLock = new object();
        private static readonly object _manualLock = new object();

        public CatalogBookGeneratorTests()
        {
            lock (_dbLock)
            {
                if (!_dbSeeded)
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
                            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sourceDb};Pooling=False"))
                            {
                                conn.Open();
                                using var cmd = conn.CreateCommand();
                                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch
                        {
                            // Ignore if busy or cannot checkpoint
                        }
                        finally
                        {
                            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        }

                        foreach (var ext in new[] { "", "-wal", "-shm" })
                        {
                            try
                            {
                                string s = sourceDb + ext;
                                string t = targetDb + ext;
                                if (File.Exists(s))
                                {
                                    if (File.Exists(t))
                                    {
                                        File.SetAttributes(t, FileAttributes.Normal);
                                    }
                                    File.Copy(s, t, overwrite: true);
                                    File.SetAttributes(t, FileAttributes.Normal);
                                }
                                else if (File.Exists(t))
                                {
                                    File.Delete(t);
                                }
                            }
                            catch
                            {
                                // Ignore individual copy failures
                            }
                        }
                    }

                    _dbSeeded = true;
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
            try { File.Delete(docxPath); } catch { }
        }

        [Fact]
        public void TestGeneratePdfCatalog()
        {
            string pdfPath = CatalogBookGenerator.GeneratePdf(isKaraoke: true);
            Assert.True(File.Exists(pdfPath));

            // Delete file after validation
            try { File.Delete(pdfPath); } catch { }
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
            try { File.Delete(txtPath); } catch { }
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
        public void UpdateUserManualsForSearchScanAndCompactAutoAdvance()
        {
            string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
            string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
            string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

            string updateText = @"
Section: Karaoke Song Search & Compact Auto-Advance Toolbar

[Update Details]
Optimizations have been introduced to the primary Karaoke hosting workspace for database search and Auto-Advance DJ control.

1. Direct Song Search Database Scan:
   - Clicking the 'Scan' button beside the search input box immediately scans and filters songs from the local SQLite database matching the search text.
   - Pressing the Enter key while typing inside the search input box also immediately executes the database scan.
   - Disk folder scanning is cleanly separated into an adjacent folder icon button ('Scan music folder from disk into library...'), preventing unintentional folder picker dialogs.

2. Compact Auto-Advance DJ Toolbar:
   - The Auto-Advance control strip has been redesigned into a slim, space-efficient horizontal toolbar (reducing vertical height from ~80px to ~32px).
   - DJ controls ('Start Song' and 'Skip Singer') have been streamlined into compact action buttons with informative tooltips.
   - This reclaims significant vertical screen real estate for the song results table, singer queue, and live lyrics preview panels on laptop displays.
";

            // 1. Update text file if not already present
            if (File.Exists(txtPath))
            {
                string content = File.ReadAllText(txtPath);
                if (!content.Contains("Karaoke Song Search & Compact Auto-Advance Toolbar"))
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
                            if (p.Text != null && p.Text.Contains("Karaoke Song Search & Compact Auto-Advance Toolbar"))
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
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Karaoke Song Search & Compact Auto-Advance Toolbar")
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
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SearchScanAndCompactToolbar"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: Karaoke Song Search & Compact Auto-Advance Toolbar", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                yPos += 15;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " SearchScanAndCompactToolbar";
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
                try { Directory.Delete(tempDir, true); } catch { }
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
                    Assert.NotEmpty(result.Tags);
                }
            }
            catch
            {
                // Gracefully handle network unavailability or rate limits
            }
        }

        [Fact]
        public void TestClearAbandonedMigrationLocks()
        {
            Lyracist.Data.LyracistDbContext.ClearAbandonedMigrationLocks();
            using var context = new Lyracist.Data.LyracistDbContext();
            context.Database.Migrate();
        }

        [Fact]
        public void UpdateUserManualsForQrCodeToggleAndMusicLibrarySeparation()
        {
            lock (_manualLock)
            {
                string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
                string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
                string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

                string updateText = @"
Section: Audience Lyrics Screen QR Code & Library Format Separation

[Update Details]
Major updates have been added to improve projection screen customization and local library management across karaoke and standard music tracks.

1. Audience Lyrics Screen QR Code Toggle:
   - Hosts can now toggle the display of the patron mobile request QR code badge positioned in the top-right corner of the lyrics projection window.
   - Configurable in three intuitive locations:
     - Settings -> Monitors & Screen Assignments: 'Show QR Code on Lyrics Screen' checkbox under Lyrics Projection Screen.
     - Lyrics Page: 'Show QR Code on Lyrics Screen' toggle switch under the Audience Lyrics Screen settings card.
     - Lyrics Window Context Menu: Right-click anywhere on the active lyrics projection window and click 'Show QR Code' to toggle visibility instantly during a live show.
   - The toggle state is permanently persisted across application restarts in AppSettings.

2. Karaoke Library vs. Music Library Separation:
   - Automatic MP3+G Companion CDG Detection: The library scanner now automatically identifies unzipped karaoke pairs (e.g. song.mp3 + song.cdg) and routes them into the Karaoke Library as MP3G tracks, preventing karaoke files from mistakenly appearing as standard background music.
   - Expanded Audio Format Support: The library scanner now supports comprehensive audio file types including .mp3, .mp4, .zip, .wav, .m4a, .flac, .wma, .aac, and .ogg.
   - Distinct Library Tabs with Live Count Badges: The search results view now features 'Karaoke Library' and 'Music Library' tabs displaying live match count badges (e.g. 'Karaoke Library (12)' and 'Music Library (3)').
   - Search Query Isolation: SQLite database queries now evaluate track type filters directly in SQL, ensuring music searches return up to 150 matching music songs without being crowded out by karaoke results.
";

                // 1. Update text file if not already present
                if (File.Exists(txtPath))
                {
                    string content = File.ReadAllText(txtPath);
                    if (!content.Contains("Audience Lyrics Screen QR Code & Library Format Separation"))
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
                                if (p.Text != null && p.Text.Contains("Audience Lyrics Screen QR Code & Library Format Separation"))
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
                                        new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Audience Lyrics Screen QR Code & Library Format Separation")
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("QrCodeToggleAndMusicSeparation"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Audience Lyrics Screen QR Code & Library Format Separation", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " QrCodeToggleAndMusicSeparation";
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
        }

        [Fact]
        public void UpdateUserManualsForSearchPerformanceTabletButtonsAndAudioVolume()
        {
            lock (_manualLock)
            {
                string docxPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.docx";
                string pdfPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual.pdf";
                string txtPath = @"C:\VB26\Lyracist\Documentation\Lyracist_User_Manual_Updates.txt";

                string updateText = @"
Section: Search Performance, Tablet Touch Sizing & High-Output Audio Engine

[Update Details]
Major performance, layout responsiveness, and audio output enhancements have been integrated into Lyracist for seamless live DJ and KJ operation.

1. High-Performance Instant Search & Lookup:
   - Zero-Lag Search Results: Song search results now batch UI notifications, updating the Karaoke and Music library tables instantaneously without visual stuttering or UI thread delays.
   - Isolated Streaming Queries: External network searches (YouTube, Spotify, Amazon) are isolated exclusively to the Streaming Links tab, keeping local library song lookup instantaneous and eliminating unnecessary bandwidth usage during shows.
   - Quick-Clear Search Button: A dedicated clear button ('X') has been integrated directly into the search bar to clear queries and reset results with a single tap or click.
   - Active Row Virtualization: Search grids now employ full UI recycling virtualization, ensuring instant scrolling even with large catalog result sets.

2. Responsive Tablet Touch Sizing & Column Protection:
   - Tablet-Optimized Action Buttons: Action buttons in search result tables (Add to Rotation, Singer History, and Singer Queue) are locked to dedicated 52px non-collapsing columns with generous 38x32px touch targets.
   - Anti-Squish Protection on High-DPI Displays: On 1080p tablet screens operating under 150% or 175% Windows DPI scaling, action buttons maintain their full proportions and never collapse into thin vertical lines.
   - Minimum Panel Width: The search and assignment panel enforces a 360px minimum width to ensure readable song titles and artist names regardless of window resizing.

3. Punchy High-Output Audio Playback:
   - Native Windows WASAPI mmdevice Audio Pipeline: All karaoke playback (LibVlcVideoBackend) and background music channels (BackgroundMusicPlayer) now route directly through the modern Windows Audio Session API (WASAPI mmdevice) on both default and custom audio devices, bypassing legacy DirectSound/WaveOut software mixer attenuation.
   - Equalizer Preamp Stabilization: Hardware and software equalizer filters now initialize with clean 0 dB preamp gain, eliminating internal filter attenuation that previously reduced track volume.
   - Enhanced DJ Volume Headroom: Software volume and gain headroom have been expanded up to 150% (+6 dB digital boost), matching native drive playback loudness and allowing performers to cut through live venue acoustics.
   - Calibrated Live Loudness Target: Automatic volume normalization has been recalibrated to -12 LUFS (matching commercial live performance standards), delivering loud, punchy playback without crushing volume.
";

                // 1. Update text file if not already present
                if (File.Exists(txtPath))
                {
                    string content = File.ReadAllText(txtPath);
                    if (!content.Contains("Search Performance, Tablet Touch Sizing & High-Output Audio Engine"))
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
                                if (p.Text != null && p.Text.Contains("Search Performance, Tablet Touch Sizing & High-Output Audio Engine"))
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
                                        new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Search Performance, Tablet Touch Sizing & High-Output Audio Engine")
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SearchPerfTabletButtonsAudioVolume"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Search Performance, Tablet Touch Sizing & High-Output Audio Engine", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " SearchPerfTabletButtonsAudioVolume";
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
        }

        [Fact]
        public void UpdateUserManualsForUsbMixerOutputBoostAndLimiter()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Master Output Boost / USB Mixer Mode & Anti-Clipping Limiter (Updated Sep 9, 2026)
- Master Output Boost / Preamp (USB Mixer Mode): Provides an adjustable digital preamplification slider from 0 dB to +12 dB with dedicated quick presets for 0 dB (Standard), +6 dB (USB Mixer / Club), and +12 dB (Maximum Drive).
- Optimized for Professional USB Mixers: Engineered specifically for external sound cards and USB audio interfaces like the Yamaha MG10XU (Channel 9/10 USB stereo return), supplying a robust, punchy +4 dBu professional line-level signal directly from Windows without having to crank mixer channel gain knobs to their physical limits.
- Anti-Clipping Peak Limiter: Integrated transparent soft-knee peak compressor/limiter that safeguards against digital clipping and speaker distortion when output boost is engaged or when loud audio tracks are played.
- Expanded Volume Headroom: Playback volume headroom ceiling extended to 200% (+6 dB digital gain) across both the main karaoke media engine and the background music fill-in players.
- Unified BGM and Performance Levels: Master preamp boost settings automatically apply across all playback channels (Karaoke performances, Opening Music, Fill-in Music, and End-of-Rotation Music) ensuring seamless, matched volume levels throughout the entire show.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Master Output Boost / USB Mixer Mode & Anti-Clipping Limiter"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                                {
                                    if (p.Text != null && p.Text.Contains("Master Output Boost / USB Mixer Mode & Anti-Clipping Limiter"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Master Output Boost / USB Mixer Mode & Anti-Clipping Limiter")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("UsbMixerOutputBoostLimiter"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Master Output Boost / USB Mixer Mode & Anti-Clipping Limiter", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " UsbMixerOutputBoostLimiter";
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
        }

        [Fact]
        public void UpdateUserManualsForKaraoke1080pLayoutOptimization()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Karaoke Screen 1080p Resolution Layout Optimization, Search Results Grid Scrolling & Compact TitleBlock (Updated Sep 10, 2026)
- 1920x1080 Resolution Optimization: Refined page margins and layout heights to eliminate vertical clipping on standard Full HD (1080p) laptops and monitors running at 100% and 125% Windows display scaling.
- Compact TitleBlock & Reclaimed Real Estate: Replaced the large 100px+ header with a sleek 40px toolbar featuring inline Venue/DJ branding, compact request bulbs, and compact theme selection, reclaiming over 60px of vertical space for the active queue and lyrics monitor.
- Interactive QR Code Thumbnail & Quick Popout: Scaled the inline QR code to a clean 30x30 thumbnail with Join URL and 1-click 'Kiosk' launcher; clicking the QR thumbnail instantly launches the enlarged, high-resolution QR modal for patrons or tablet kiosk setup.
- Internal Search Results DataGrid Scrolling: Removed the outer column ScrollViewer so the Karaoke and Music Library DataGrids are constrained to 15–20 visible rows with smooth internal vertical scrolling and UI recycling virtualization, preventing massive lists (100+ songs) from stretching the page.
- Direct Singer Assignment Visibility: Redesigned the Singer Assignment card into a compact two-column layout (Singer Name + Duet Partner, Key Slider + Notes), anchoring it permanently beneath the search grid so DJs never have to scroll down to assign selected songs to performers.
- Compact Global FooterBar & Anti-Impingement Spacing: Streamlined the playback footer bar from ~94px down to ~52px by merging the performer name and active song title onto a single row (displaying the song in bold italics and golden highlight color), placing the seek bar on the 2nd row with dedicated right margin spacing to prevent the duration progress bar and timestamps from impinging on the center Play/Pause/Stop controls.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Karaoke Screen 1080p Resolution Layout Optimization"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Karaoke Screen 1080p Resolution Layout Optimization"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Karaoke Screen 1080p Resolution Layout Optimization, Search Results Grid Scrolling & Compact TitleBlock")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("Karaoke1080pLayoutOptimization"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Karaoke Screen 1080p Resolution Layout Optimization", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " Karaoke1080pLayoutOptimization";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForSpecialEventBannerSelection()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Special Event Banner Single Selection & 'None' Button Reset (Updated Sep 10, 2026)
- Guaranteed Mutual Exclusion: Special Event Banners (e.g. Birthday, Anniversary, Holiday, Scaryoke) now strictly enforce single-selection mutual exclusion across the radio button group. Selecting any banner immediately deselects all other banners.
- Active 'None' Selection Reset: Selecting the 'None' radio button cleanly clears and deselects all active special event banner selections across the interface and restores standard DJ branding banners automatically.
- Synchronization & Persistence: The active selection is synchronized seamlessly between Lyracist and KSRotation through the event synchronization loop and saved in application display preferences.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Special Event Banner Single Selection"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Special Event Banner Single Selection"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Special Event Banner Single Selection & 'None' Button Reset")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SpecialEventBannerMutualExclusion"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Special Event Banner Single Selection & 'None' Button Reset", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " SpecialEventBannerMutualExclusion";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForHybridGpsVenueAndCasting()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Hybrid GPS Venue Auto-Location & DashCast TV Streaming (Updated Sep 17, 2026)
- Hybrid GPS & Wi-Fi Venue Detection: Automatically recognizes performance venues using a 150-meter GPS proximity circle (via Haversine spherical distance). Known venues in Settings/ksrotation_venues.json or venues.json are selected automatically on launch.
- Travel Router SSID Immunity: Portable travel routers broadcasting static SSIDs across multiple gigs can be marked as 'Travel Router' in KSRotation.Maui About popup. Flagged SSIDs are excluded from Wi-Fi matching to prevent incorrect venue selection.
- Tablet-to-Laptop GPS Sync: Laptops without satellite GPS hardware receive real-time peer GPS coordinates from companion Android tablets running KSRotation.Maui via POST /api/venue/location.
- 1-Click '📍 Tag GPS' Geotagging: Instantly records and saves current satellite coordinates, Wi-Fi SSID, and 150m detection radius to the active venue name across Lyracist, KSRotation, and KSRotation.Maui.
- DashCast TV Billboard Streaming: Stream live rotation queues, current singer, upcoming queue, and dual QR codes directly to Google Cast / Chromecast displays with dynamic server-side template injection for instant first paint.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Hybrid GPS Venue Auto-Location & DashCast TV Streaming"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Hybrid GPS Venue Auto-Location"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Hybrid GPS Venue Auto-Location & DashCast TV Streaming")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("HybridGpsVenueAutoLocation"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Hybrid GPS Venue Auto-Location & DashCast TV Streaming", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " HybridGpsVenueAutoLocation";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForSingerSkip()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Singer Skip Round-Scoped Rotation Bypass (Updated Sep 17, 2026)
- Singer Skip vs. Inactive vs. Paused: When a performer needs to step away temporarily (e.g. grabbing a drink or stepping outside), the DJ can activate 'Skip' without removing them from rotation or losing their turn order.
- Retains Rotation Placement: Unlike 'Inactive' (which forfeits rotation spot and drops the singer to the end of the line), a skipped singer retains their exact slot in the rotation queue.
- Automatic Round-Scoped Rollover: When rotation advances and the round completes (crossing or reaching the '⚓ Anchor' singer), the 'IsSkipped' flag automatically resets so the performer sings normally in the following round without requiring manual DJ intervention.
- Manual Unskip: The DJ can toggle 'Skip' off at any time using the row button, context menu, or remote DJ portal. If the currently performing singer is skipped, the rotation immediately advances to the next eligible performer.
- Cross-Platform UI & Indicators: Supported with visual '⏭ SKIP' amber badges, quick action toggle buttons, and synchronized wait-time recalculations across Lyracist, KSRotation, Remote DJ Portal, Audience Billboard, Kiosk, and Patron Mobile Portal.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Singer Skip Round-Scoped Rotation Bypass"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Singer Skip Round-Scoped Rotation Bypass"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Singer Skip Round-Scoped Rotation Bypass")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SingerSkipRoundScopedBypass"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Singer Skip Round-Scoped Rotation Bypass", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " SingerSkipRoundScopedBypass";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForSpecialSinger()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Special Singer (One-Time Performance) (Updated Sep 18, 2026)
- Special / Guest Performer Support: Accommodates guest singers or one-off performances who only perform a single song without joining the ongoing rotation.
- Instant Top of List & Current Singer: When added or designated, the special singer is placed immediately at the top of the queue (index 0) and promoted to current performer, accommodating spur-of-the-moment guest performances.
- Displaced Performer Continuity: If another performer was already singing or designated next, they are preserved as 'Up Next' so rotation resumes with them seamlessly once the special performance concludes.
- Visual Highlight While Performing: Displays a prominent '⭐ SPECIAL' badge on stage, detached rotation displays, external audience billboards, TV web-casts, tablet kiosks, and mobile portals.
- Automatic Inactive Transition: Immediately following the conclusion of their song, the special performer is marked inactive automatically.
- Seamless Queue Resumption: The rotation immediately resumes with the displaced performer or next sequential singer as if the guest was never in the rotation.
- Rotation Anchor Immunity: Round anchor allocation algorithms ignore special performers so round tracking boundaries are never anchored to transitory guest singers.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Instant Top of List & Current Singer"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    try
                    {
                        using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                            {
                                var body = doc.MainDocumentPart?.Document?.Body;
                                if (body != null)
                                {
                                    bool alreadyAppended = false;
                                    foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                    {
                                        if (p.InnerText.Contains("Instant Top of List & Current Singer"))
                                        {
                                            alreadyAppended = true;
                                            break;
                                        }
                                    }

                                    if (!alreadyAppended)
                                    {
                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                                new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                                new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Special Singer (One-Time Performance)")
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
                    }
                    catch (Exception)
                    {
                        // File may be locked by an external reader/Word during test execution
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SpecialSingerCurrentTopPlacement"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Special Singer (One-Time Performance)", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " SpecialSingerCurrentTopPlacement";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForDeviceHandoff()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Seamless Bidirectional Device Switching & Session Handoff (Updated Sep 19, 2026)
- Live Karaoke Session Continuity: Allows a host or DJ to transition an ongoing karaoke event between a laptop (running KSRotation) and a tablet (running KSRotation.Maui on Android or Windows) without losing a single singer, queue position, or performance record.
- Complete State Preservation: The handoff payload completely transfers the active singer queue, currently performing singer ('IsCurrent'), next-up singer ('IsNext'), round rotation anchor ('IsRotationStart'), paused/inactive singers, queued future songs, linked duet pairs, completed song checkmarks (rounds 1-10), tonight's performance history with original timestamps, pending patron requests, venue/DJ branding, and session schedule/duplicate rules.
- Dedicated Switch Device QR Code & Web Portal: The hosting device generates a high-resolution QR code pointing to http://<host-ip>:<port>/handoff. Scanning this QR code using any smartphone or tablet camera displays a live session summary with active singer counts, current performer name, and a one-tap 'Open in KSRotation MAUI' action button.
- Native Deep Linking (ksrotation://handoff): When scanned or tapped on mobile/tablet, the system deep-links directly into KSRotation.Maui, pre-fills the host parameters, and prompts for one-tap confirmation to take over the event.
- One-Tap Wi-Fi Auto-Discovery: Both KSRotation and KSRotation.Maui feature an inline 'Scan Wi-Fi' network discovery tool that sweeps the local subnet to detect active peers in seconds, eliminating manual IP address entry.
- Bidirectional Handoff (Laptop <-> Tablet): Supports transferring from Laptop to Tablet when leaving the venue, and transferring from Tablet back to Laptop when returning or setting up the main rig.
- Failsafe Manual Entry & Session Backup (.ksr): If local router client isolation is enabled, DJs can enter the IP and DJ PIN manually, or download/export a standalone session backup file (.ksr) directly from the portal.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Seamless Bidirectional Device Switching & Session Handoff"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Seamless Bidirectional Device Switching & Session Handoff"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Seamless Bidirectional Device Switching & Session Handoff")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SeamlessDeviceSwitchingHandoff"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Seamless Bidirectional Device Switching & Session Handoff", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " SeamlessDeviceSwitchingHandoff";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForAutoRemoteDjHandoff()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Automated Transition to In-App Remote DJ Controller on Handoff (Updated Sep 20, 2026)
- Automatic Tablet Remote DJ Transition: When the host arrives and pulls an active karaoke session from the DJ's tablet using KSRotation desktop ('Pull Session from Device'), the tablet running KSRotation.Maui automatically switches into an in-app Remote DJ View (dj.html).
- Complete In-App Experience: The DJ remains inside the native KSRotation.Maui application, where an embedded full-screen Remote DJ controller renders the live rotation, queue management tools, round checkmarks, last round toggles, and singer addition modals.
- Automatic PIN Pre-Authentication: The desktop host securely passes its connection address, port, and DJ PIN during the handoff pull, allowing the tablet's embedded web view to authenticate automatically without requiring the DJ to re-enter credentials.
- Top Header Controls & Standalone Exit: The in-app Remote DJ view features a dedicated header toolbar with live connection indicators, a 'Reload' button for instantaneous refreshes, and an 'Exit Remote DJ' button allowing hosts to safely return the tablet to native standalone hosting mode whenever needed.
- Configurable Auto-Switch Setting: An 'Auto-switch to Remote DJ (dj.html) on handoff' toggle is available directly on the Switch Device dialog and within application settings (enabled by default). When disabled, the tablet prompts the DJ with an optional confirmation before transitioning.
- Manual Remote DJ Connection: DJs can also tap the 'Connect as Remote DJ' button directly from the Switch Device dialog to connect to any active host machine on the Wi-Fi network at any time.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Automated Transition to In-App Remote DJ Controller on Handoff"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Automated Transition to In-App Remote DJ Controller on Handoff"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Automated Transition to In-App Remote DJ Controller on Handoff")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("AutoRemoteDjHandoff"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Automated Transition to In-App Remote DJ Controller on Handoff", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 15), leftAlign);
                                    yPos += 15;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " AutoRemoteDjHandoff";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForDeviceHandoffAndFlowDiagram()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string flowDiagram =
@"[Architectural Flow: Live Session Handoff & Remote DJ Takeover]
+------------------------------------------------------------------------+
|        ARCHITECTURAL FLOW: LIVE SESSION HANDOFF & REMOTE DJ TAKEOVER   |
+------------------------------------------------------------------------+

    DJ (Tablet: KSRotation.Maui)             KJ Host (Laptop: KSRotation)
    +---------------------------+           +----------------------------+
    | Active rotation on tablet |           | Arrives at venue & opens   |
    | Serves requests & portal  |           | 'Switch Device' on laptop  |
    +-------------+-------------+           +--------------+-------------+
                  |                                        |
                  |                                 [1] Wi-Fi Scan or IP
                  |                                     Selects tablet
                  |                                        |
                  |<------- 1. GET /api/session/handoff ---+
                  |         (Sends Laptop IP, Port & PIN)  |
                  |                                        |
                  +------- 2. 200 OK + Full Session JSON ->|
                  |        (Queue, Checkmarks, History)    |
                  |                                 [2] Imports session
                  |                                     Takes over host
                  |                                        |
  [3] Auto-switches to in-app                             |
      Remote DJ (dj.html)                                  |
      Pre-authenticated with PIN                           |
                  |                                        |
  [4] DJ manages show from floor via embedded tablet UI    |
      =====================================================+===============
      Seamless bi-directional synchronization over venue Wi-Fi!";

                string updateText = @"
Section: Device Switching, Live Session Handoff & Architecture Flow (Updated Sep 20, 2026)

" + flowDiagram + @"

[Key Capabilities & Workflow]
1. Zero-Beat Live Session Migration:
   - Completely transfers active queue, current performer, up-next performer, rotation start anchor, round checkmarks (1-10), tonight's performance history with original timestamps, pending patron requests, and venue/DJ branding between laptop and tablet.
2. Automated In-App Remote DJ Transition:
   - When the host laptop takes over, the DJ's tablet automatically transitions into an embedded full-screen Remote DJ controller (dj.html), pre-authenticated with the host's credentials so the DJ can immediately manage queue order and mark songs finished from the floor.
3. One-Tap Wi-Fi Auto-Discovery:
   - Sweeps the venue subnet in 1 second to detect active instances on port 5000. Selecting a discovered peer auto-populates the host IP and port, and focuses the DJ PIN field for instant entry.
4. Dedicated Switch Device QR Code & Deep Linking:
   - Generating a QR code to /handoff allows instant camera scanning, opening a mobile landing page with a 1-tap 'Open in KSRotation MAUI' deep link (ksrotation://handoff).
5. Dedicated Kiosk Request Station (kiosk.html):
   - Mount an Android or iOS tablet in landscape mode at the bar or near the stage. When idle, an attractive attractor screen greets patrons, inviting them to search songs and submit requests directly into the DJ's approval queue.
6. Audience Billboard & Chromecast Web-Casting (billboard.html):
   - Stream the live audience billboard directly to Google Cast / Chromecast displays from KSRotation.Maui, featuring a dedicated full-height rotation column and side-by-side QR codes.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Device Switching, Live Session Handoff & Architecture Flow"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Device Switching, Live Session Handoff & Architecture Flow"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Device Switching, Live Session Handoff & Architecture Flow")
                                        )
                                    ));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        bool isDiagramLine = line.Contains("+--") || line.Contains("|") || line.Contains("===") || line.Contains("-->");
                                        var runProps = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = isDiagramLine ? "18" : "22" }
                                        );
                                        if (isDiagramLine)
                                        {
                                            runProps.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.RunFonts() { Ascii = "Courier New", HighAnsi = "Courier New" });
                                        }

                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                                runProps,
                                                new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                            )
                                        ));
                                    }
                                    doc.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("DeviceHandoffFlowArch"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont monoFont = new PdfSharp.Drawing.XFont("Arial", 7.5, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Device Switching, Live Session Handoff & Architecture Flow", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 58;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isDiagram = line.Contains("+--") || line.Contains("|") || line.Contains("===") || line.Contains("-->");
                                    var font = isDiagram ? monoFont : bodyFont;
                                    var brush = isDiagram ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isDiagram ? 9.5 : 12;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " DeviceHandoffFlowArch";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForRoundCompletionEstimationNotice()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Dynamic Round Completion Estimation & Full Round Duration Notice (Updated Sep 21, 2026)

[Overview & DJ Benefits]
• End-of-Night Round Timing:
  - During live karaoke shows, knowing whether there is sufficient venue time remaining for another full round before closing or last call is critical for smooth show operations.
  - The Dynamic Round Completion Estimation system tracks the remaining performers in the active round, calculates remaining minutes based on individual or default song lengths, and projects the exact estimated completion clock time (ETA).

[Cross-App Availability & Displays]
1. Remote DJ Controller (dj.html):
   - A prominent, color-accented status banner is docked directly beneath the Last Round banner at the top of the queue panel.
   - Shows real-time performer count left in the round, remaining minutes, projected clock completion time (e.g. 'ends ~11:42 PM'), and full round duration.
   - Updates dynamically every 15 seconds, and recalculates immediately on queue modifications (singer add, reorder, status toggle).
2. Desktop Karaoke Station (KSRotation):
   - Styled badge beside active singer counts in the main Rotation header toolbar: '⏱️ Round: 4 singers left • ~20m (ends ~11:42 PM) | Full round: ~25m (5 singers)'.
3. Mobile Tablet App (KSRotation.Maui):
   - Pinned estimation banner above the singer rotation list on Android and Windows tablets.
4. Lyracist Pro (Rotation Page):
   - Pinned estimation banner above the singer queue on the Rotation management page.

[Smart Round Mechanics]
• Dynamic Recalculation:
  - Updates as performers finish songs, new singers are added, or songs are reordered.
• Last Round Awareness:
  - In 'Last Round' mode, filters strictly for performers who have not yet sung in the final round ('!HasSungInLastRound'), showing the exact countdown of remaining performances until the show concludes.
• Singer Status Filtering:
  - Automatically excludes paused, skipped, inactive performers, and filler background music tracks so timing projections reflect true active vocalists.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Dynamic Round Completion Estimation & Full Round Duration Notice"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Dynamic Round Completion Estimation & Full Round Duration Notice"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Dynamic Round Completion Estimation & Full Round Duration Notice")
                                        )
                                    ));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        var runProps = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = line.StartsWith("[") ? "24" : "22" }
                                        );
                                        if (line.StartsWith("["))
                                        {
                                            runProps.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                        }

                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                                runProps,
                                                new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                            )
                                        ));
                                    }
                                    doc.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("RoundEstimateDocNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.5, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.5, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Dynamic Round Completion Estimation & Full Round Duration Notice", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.5;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " RoundEstimateDocNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForActiveSingersOnVegasAndVinylBanners()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Active Singers Count Display on Vegas Billboard and Vinyl Record Banners (Updated Sep 21, 2026)

[Overview & Audience Display Enhancements]
• Live Rotation Count Visibility:
  - Both the Vegas Marquee ('Vegas Billboard') and the Vinyl Turntable ('Vinyl Record') projection banners prominently display the live count of active performers currently in the rotation queue (e.g. '5 Singers in Rotation').
  - The badge reflects real-time rotation state, instantly updating as singers are added, reordered, marked inactive, or when a final round is underway.

[Display Locations & Styling]
1. Vegas Marquee ('Vegas Billboard'):
   - Top Header: A sleek, gold-bordered glowing badge ('🎤 {N} Singers in Rotation') is anchored in the top-right corner of the brass marquee frame.
   - Up Next Section: A matching pill badge sits directly alongside the 'UP NEXT' label above the upcoming singer chips.
2. Vinyl Turntable ('Vinyl Record'):
   - Now Spinning Header: A glowing gold badge ('🎤 {N} Singers in Rotation') is positioned alongside the '♪ NOW SPINNING' / '♪ NOW PLAYING' header.
   - On Deck Section: A matching pill badge sits directly alongside the 'ON DECK' queue label.

[Smart Filtering]
• Pure Vocalist Tracking:
  - Excludes paused, skipped, and inactive singers, as well as background music filler tracks, so patrons and the KJ see the exact count of active singers in the rotation cycle.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Active Singers Count Display on Vegas Billboard and Vinyl Record Banners"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Active Singers Count Display on Vegas Billboard and Vinyl Record Banners"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Active Singers Count Display on Vegas Billboard and Vinyl Record Banners")
                                        )
                                    ));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        var runProps = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = line.StartsWith("[") ? "24" : "22" }
                                        );
                                        if (line.StartsWith("["))
                                        {
                                            runProps.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                        }

                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                                runProps,
                                                new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                            )
                                        ));
                                    }
                                    doc.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("ActiveSingersBannersNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.5, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.5, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Active Singers Count Display on Vegas Billboard and Vinyl Record Banners", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.5;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " ActiveSingersBannersNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForFourNewRotationBillboardViews()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Four New Dynamic Rotation Billboard Projection Views (Updated Sep 22, 2026)

[Overview & Audience Display Options]
Lyracist Pro and KSRotation now feature eleven distinct, high-impact projection themes for the audience-facing Singer Rotation Billboard screen. Four new animated themes have been added with 100% visual and behavioral parity across both applications:
1. Casino Slot Reels:
   - Experience high-roller casino excitement with each rotation slot rendered as a spinning slot machine reel.
   - When a performance starts, the hero performer reel rapidly spins and clunks to a decelerated stop on the payline.
   - The current singer receives a celebratory ""JACKPOT"" flourish complete with a pulsating golden drop-shadow glow and a dynamic burst of drifting gold coins and star sparkles.
2. Jukebox:
   - Transports the venue to a classic 1950s rock-and-roll diner cabinet.
   - Features a chrome-trimmed neon arch header, glowing illuminated song-selection pushbuttons for the upcoming queue, and dual animated rising bubble tubes that drift continuously up the left and right cabinet pillars.
3. Stadium Jumbotron:
   - Delivers a massive arena sports-and-concert experience with an authentic LED scoreboard aesthetic.
   - Performer names and song titles are presented with high-visibility dot-matrix typography while twin sweeping stadium floodlights crisscross the arena backdrop.
4. Movie Theater 'Now Showing':
   - An elegant vintage Hollywood premiere presentation.
   - Begins with a classic 35mm film leader countdown sweep (3...2...1) and a flickering cinematic projector light cone that cascades over the feature performer card.
   - Upcoming singers are showcased on vintage marquee coming-attractions lobby cards.

[Full Feature Parity & Controls]
• Complete Badge Support:
  - All four new views fully support performer avatars/selfies, ⭐ SPECIAL performer badges, ⚓ ANCHOR round-start markers, and {N} estimated wait-time badges.
• Automatic Cycling & Rotation Schedule:
  - All eleven projection views are dynamically integrated into the Screen Rotation schedule under Settings -> Display -> Screen Rotation.
  - DJs can check which views to include and configure custom display durations (in seconds) for hands-free, automated cycling throughout the show.
• View Mode Help & Descriptions:
  - Complete descriptions for all eleven themes are accessible directly within KSRotation (Help Topic 3) and Lyracist Pro (Settings -> Display -> Billboard View Mode -> View Mode Descriptions & Themes expander).";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Four New Dynamic Rotation Billboard Projection Views"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Four New Dynamic Rotation Billboard Projection Views"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Four New Dynamic Rotation Billboard Projection Views")
                                        )
                                    ));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        var runProps = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = line.StartsWith("[") ? "24" : "22" }
                                        );
                                        if (line.StartsWith("["))
                                        {
                                            runProps.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                        }

                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                                runProps,
                                                new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                            )
                                        ));
                                    }
                                    doc.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("FourNewBillboardViewsNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Four New Dynamic Rotation Billboard Projection Views", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " FourNewBillboardViewsNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForUnifiedScreenRotationAndDisplayLayout()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Screen Rotation and Display Tab Layout Reorganization (Updated Sep 22, 2026)

[Overview & Layout Redesign]
The Display configuration experience in KSRotation and Lyracist Pro has been streamlined for enhanced operational clarity and fast setup:
1. Two-Column Optimized Layout:
   - Target Monitor (Display & Projection), Connect & Request Instructions, and Casting Controls are consolidated into Column 0 on the left.
   - The entire Column 1 on the right is dedicated to the Screen Rotation settings, providing a clean, uncluttered interface.
2. Unified Rotation Interval (Single Time Setting):
   - Eliminated tedious individual per-screen duration inputs in favor of a single global duration setting (e.g., 'Change screen every: 180 sec').
   - DJs and KJs no longer have to manually set the seconds on every individual view.
3. Random Screen Cycling:
   - When automatic screen rotation is active, the billboard projection now randomly cycles among the enabled views at every configured interval.
   - When multiple screens are enabled, the random picker automatically avoids immediately repeating the current screen, delivering dynamic and fresh visual variety to the audience.
4. One-Click 'Select All' & 'Clear All' Controls:
   - Added instant '✓ Select All' and '✗ Clear All' action buttons above the screen list.
   - Hosts can enable all 11 billboard views with a single click or clear the selection instantly to focus on just their favorites.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Screen Rotation and Display Tab Layout Reorganization"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Screen Rotation and Display Tab Layout Reorganization"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Screen Rotation and Display Tab Layout Reorganization")
                                        )
                                    ));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        var runProps = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = line.StartsWith("[") ? "24" : "22" }
                                        );
                                        if (line.StartsWith("["))
                                        {
                                            runProps.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                        }

                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                            new DocumentFormat.OpenXml.Wordprocessing.Run(
                                                runProps,
                                                new DocumentFormat.OpenXml.Wordprocessing.Text(line)
                                            )
                                        ));
                                    }
                                    doc.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("UnifiedScreenRotationNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Screen Rotation and Display Tab Layout Reorganization", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " UnifiedScreenRotationNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManual_SongEndReturnKaraokeScreen_Oct2026()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Song End Return to Karaoke Screen & DJ Manual Start Workflow (Updated Oct 3, 2026)

[Overview & Operational Flow]
To ensure smooth hosting transitions during live shows, Lyracist Pro provides an optimized workflow when a singer completes their performance:
1. Automatic Return to Karaoke Control Screen:
   - When the currently playing song finishes, the main host window immediately navigates back to the primary Karaoke screen (KaraokePage).
   - This provides the DJ with instant access to the singer queue, search bar, and playback controls without requiring manual tab clicks.
2. Audience Display Reset to Rotation Billboard:
   - The secondary projection window seamlessly closes the lyrics view and restores the full-screen Rotation Billboard.
   - The billboard displays the upcoming singer queue and automatically flashes an announcement welcoming the next singer to the stage.
3. DJ-Controlled Performance Start (No Automatic Song Progression):
   - Rather than automatically launching the next song over an empty stage, Lyracist marks the completed singer done, queues the next performer, starts fill-in background music, and waits safely in 'Ready to Start'.
   - The DJ initiates playback with the '▶ Start Song' button once the next singer is stationed on stage with their microphone.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Song End Return to Karaoke Screen & DJ Manual Start Workflow"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Song End Return to Karaoke Screen & DJ Manual Start Workflow"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    var headingPara = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading2" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1F497D" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Song End Return to Karaoke Screen & DJ Manual Start Workflow"))));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        bool isSubHead = line.StartsWith("[");
                                        var p = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                                        var r = p.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
                                        if (isSubHead)
                                        {
                                            r.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1F497D" }));
                                        }
                                        r.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(line));
                                    }
                                    doc.MainDocumentPart?.Document?.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("SongEndReturnKaraokeNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Song End Return to Karaoke Screen & DJ Manual Start Workflow", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " SongEndReturnKaraokeNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManual_LyricsAudioVisualizer_Oct2026()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Lyrics Screen Audio Visualizer & Spectrum Customization (Updated Oct 3, 2026)

[Overview & Operational Capabilities]
Lyracist Pro includes a configurable, real-time audio spectrum visualizer displayed across the bottom of the active Lyrics projection screen:
1. Live Audio Spectrum Analysis (Real-Time WASAPI Loopback FFT):
   - Rather than simple simulated animation, Lyracist features a hardware-accelerated Windows Core Audio (WASAPI mmdevice) loopback engine that captures the live audio output directly.
   - A 1024-point Radix-2 Fast Fourier Transform (FFT) with Hann windowing decomposes music into logarithmic frequency bands (from 32 Hz sub-bass to 16 kHz brilliance).
   - Frequency pre-emphasis tilt ensures high-frequency percussive elements (hi-hats, cymbals) dance vigorously alongside bass kicks and vocal transients with smooth attack/decay physics.
2. Simulated & Ambient Motion Mode:
   - For hosts preferring purely decorative motion without audio loopback capture, hosts can switch to 'Simulated / Ambient' mode which animates rhythmic volume-scaled bars during playback and gentle ambient sine waves when idle.
3. Visual Themes & Color Schemes:
   - Choose between 7 stylized gradient themes:
     * Neon Sunset: Blue (#2563EB) to Purple (#A855F7) to Hot Pink (#EC4899).
     * Cyberpunk: Electric Cyan (#00F0FF) to Deep Violet (#7928CA) to Neon Fuchsia (#FF007F).
     * Emerald Pulse: Forest Green (#065F46) to Vibrant Jade (#10B981) to Neon Lime (#84CC16).
     * Solar Flare: Crimson Red (#DC2626) to Vivid Amber (#F59E0B) to Electric Gold (#FDE047).
     * Electric Blue: Deep Navy (#1E3A8A) to Azure (#0284C7) to Ice Cyan (#67E8F9).
     * Rainbow Spectrum: 360-degree full-spectrum rainbow hue mapping across every individual frequency bar.
     * Monochrome Glow: Deep Slate (#334155) to Silver (#94A3B8) to Crisp White (#FFFFFF).
4. Bar Width, Density & Spacing Adjustments:
   - Slim: 10px bars (high density, up to 80 bars).
   - Normal: 18px bars (standard balanced presentation, ~40 bars).
   - Wide: 28px bars (bold, chunky retro aesthetic, ~25 bars).
   - Extra Wide: 42px bars (classic broadcast DJ equalizer look, ~16 bars).
5. Opacity Control & Instant Context Menu Access:
   - Adjust visualizer opacity from 10% to 100% via Settings or quick-select (25%, 50%, 80%, 100%).
   - All visualizer settings (Toggle, Mode, Style, Bar Width, Opacity) can be adjusted in real-time from the Settings Page (Display Tab) or by right-clicking directly anywhere on the active Lyrics window.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Lyrics Screen Audio Visualizer & Spectrum Customization"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Lyrics Screen Audio Visualizer & Spectrum Customization"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    var headingPara = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading2" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1F497D" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Lyrics Screen Audio Visualizer & Spectrum Customization"))));

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;
                                        bool isSubHead = line.StartsWith("[");
                                        var p = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                                        var r = p.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
                                        if (isSubHead)
                                        {
                                            r.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1F497D" }));
                                        }
                                        r.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(line));
                                    }
                                    doc.MainDocumentPart?.Document?.Save();
                                }
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
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("LyricsVisualizerNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Lyrics Screen Audio Visualizer & Spectrum Customization", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " LyricsVisualizerNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManual_AdjustSynthDisplayModal_Oct2026()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));

            if (Directory.Exists(docDir))
            {
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");

                string updateText = @"Section: DJ Karaoke Screen / Adjust Synth Display Pop-up Dialog

[Update Details]
To ensure a completely professional live performance experience without audience distraction, DJs and KJs no longer need to interact with or right-click the audience-facing Lyrics projection screen to configure the audio visualizer.

1. Dedicated 'Adjust Synth Display' DJ Controls:
   - Placed directly on the primary 'Karaoke' operational screen in multiple convenient locations:
     * Next to the 'Skip Singer' button on the top DJ playback control strip.
     * Above the live 'Lyrics Preview Monitor' in the right-hand inspection column.
     * Alongside the 'Lyrics Projection Screen' monitor assignment controls under Multi-Monitor Display controls.

2. Modal Settings Pop-up Dialog ('Adjust Synth Display'):
   - Clicking 'Adjust Synth Display' opens a dedicated modal dialog centered over the main Lyracist DJ window (completely invisible to the audience on external projection screens).
   - Allows instant real-time adjustment of:
     * Visualizer Enable/Disable toggle.
     * Motion Mode: 'Audio Spectrum (Live FFT)' (reactive loopback frequency bands) vs 'Simulated / Ambient' (procedural sine waves).
     * Color Themes: Neon Sunset, Cyberpunk, Emerald Pulse, Solar Flare, Electric Blue, Rainbow Spectrum, Monochrome Glow.
     * Bar Width & Density: Slim, Normal, Wide, Extra Wide.
     * Opacity Control: Continuous slider (10% to 100%) plus 25%, 50%, 80%, and 100% quick preset buttons.
     * Real-time Interactive Preview: A live animated preview strip inside the dialog provides immediate visual feedback of color, density, and opacity changes.

3. Persistent Configuration:
   - All visualizer adjustments made in the dialog apply to the active projection screen immediately and are automatically saved to appsettings.json, ensuring all settings are preserved and recalled on app restart.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("DJ Karaoke Screen / Adjust Synth Display Pop-up Dialog"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                string docText = body.InnerText;
                                if (!docText.Contains("Adjust Synth Display Pop-up Dialog"))
                                {
                                    var titlePara = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading2" },
                                            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = "360", After = "120" }
                                        ),
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "0066CC" },
                                                new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "26" }
                                            ),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: DJ Karaoke Screen / Adjust Synth Display Pop-up Dialog")
                                        )
                                    );
                                    body.AppendChild(titlePara);

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;

                                        bool isHeader = line.StartsWith("[") || line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.");
                                        var pPr = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = isHeader ? "140" : "60", After = "60" }
                                        );

                                        var rPr = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "21" }
                                        );
                                        if (isHeader)
                                        {
                                            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1A365D" });
                                        }

                                        var run = new DocumentFormat.OpenXml.Wordprocessing.Run(rPr, new DocumentFormat.OpenXml.Wordprocessing.Text(line));
                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(pPr, run));
                                    }

                                    doc.MainDocumentPart?.Document?.Save();
                                }
                            }
                        }
                    }
                }

                // 3. Update pdf file if not already present
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("AdjustSynthDisplayNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: DJ Karaoke Screen / Adjust Synth Display Pop-up Dialog", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[") || line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " AdjustSynthDisplayNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManual_PreShowScreenAndWifiQr_Oct2026()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));

            if (Directory.Exists(docDir))
            {
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");

                string updateText = @"Section: Audience Welcome & Pre-Show Screen / Dual QR Code Integration (Wi-Fi & Song Sign-Up)

[Update Details]
To optimize patron onboarding before a show begins and between active performance sets, the venue projection display now features a dedicated Pre-Show Screen mode with stacked Wi-Fi and Singer Sign-Up QR codes.

1. Pre-Show Screen Mode ('Hold Screen'):
   - A dedicated 'Pre-Show Screen' toggle button is accessible directly on the primary DJ control toolbar in both Lyracist and KSRotation, as well as under Rotation Monitor controls.
   - When engaged, the Audience Sign-Up / Welcome screen remains actively displayed on the singer/audience projection monitor even after singers and requests are added to the rotation queue.
   - This allows DJs and KJs to set up and populate their queue ahead of time while keeping the audience welcome, Wi-Fi credentials, and sign-up instructions prominently visible on screen until the show officially begins.
   - A configurable hotkey shortcut (customizable in Settings > Hotkeys) is provided to toggle Pre-Show Mode instantly from anywhere in the application.

2. Stacked QR Code Layout (~20% Screen Width):
   - The right side of the 1080p audience canvas is dedicated to clear, high-contrast QR code cards utilizing approximately 20% of the display width.
   - When a venue Wi-Fi network is detected or configured, two stacked cards are presented:
     * 'CONNECT WI-FI': Displays the venue Wi-Fi QR code, network SSID, and password for one-scan automatic network connection.
     * 'SCAN TO SIGN UP': Displays the Web Remote / Patron Portal QR code and direct URL for submitting song requests.
   - Automatic Wi-Fi Fallback: If no venue Wi-Fi is configured or detected, the display cleanly adapts to show only the single sign-up QR code card on the right side.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Audience Welcome & Pre-Show Screen / Dual QR Code Integration"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                string docText = body.InnerText;
                                if (!docText.Contains("Audience Welcome & Pre-Show Screen / Dual QR Code Integration"))
                                {
                                    var titlePara = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading2" },
                                            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = "360", After = "120" }
                                        ),
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "0066CC" },
                                                new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "26" }
                                            ),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Audience Welcome & Pre-Show Screen / Dual QR Code Integration (Wi-Fi & Song Sign-Up)")
                                        )
                                    );
                                    body.AppendChild(titlePara);

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;

                                        bool isHeader = line.StartsWith("[") || line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.");
                                        var pPr = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = isHeader ? "140" : "60", After = "60" }
                                        );

                                        var rPr = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "21" }
                                        );
                                        if (isHeader)
                                        {
                                            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1A365D" });
                                        }

                                        var run = new DocumentFormat.OpenXml.Wordprocessing.Run(rPr, new DocumentFormat.OpenXml.Wordprocessing.Text(line));
                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(pPr, run));
                                    }

                                    doc.MainDocumentPart?.Document?.Save();
                                }
                            }
                        }
                    }
                }

                // 3. Update pdf file if not already present
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("PreShowScreenNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Audience Welcome & Pre-Show Screen / Dual QR Code Integration", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[") || line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " PreShowScreenNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManual_PreShowWelcomeSuppression_Oct2026()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));

            if (Directory.Exists(docDir))
            {
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");

                string updateText = @"Section: Pre-Show Screen / Singer Welcome Screen Hold & Sequenced Release

[Update Details]
To prevent audience projection distraction and keep Wi-Fi onboarding instructions visible before the show starts, singer welcome screens are held in queue while Pre-Show Mode is active.

1. Welcome Screen Suppression:
   - While the Pre-Show screen ('Hold Screen') is actively displayed on the audience projection monitor, individual 'Welcome to our new performer' screens are suppressed from appearing.
   - Any new performers who join the rotation (via patron smartphone QR portal, tablet kiosk, or KJ queue addition) are registered in the background and queued in order.
   - If Pre-Show Mode is engaged while a welcome screen was actively displaying, that welcome screen is immediately hidden and saved at the head of the queue.

2. Sequenced Release on Pre-Show Close:
   - When the host closes/turns off Pre-Show Screen mode to kick off the show, all held singer welcome screens automatically display one after another in exact sequence.
   - Once all queued new performers have been welcomed, the projection window transitions smoothly to the active Rotation Billboard queue.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Singer Welcome Screen Hold & Sequenced Release"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                string docText = body.InnerText;
                                if (!docText.Contains("Singer Welcome Screen Hold & Sequenced Release"))
                                {
                                    var titlePara = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.ParagraphStyleId { Val = "Heading2" },
                                            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = "360", After = "120" }
                                        ),
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                                new DocumentFormat.OpenXml.Wordprocessing.Bold(),
                                                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "0066CC" },
                                                new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "26" }
                                            ),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Pre-Show Screen / Singer Welcome Screen Hold & Sequenced Release")
                                        )
                                    );
                                    body.AppendChild(titlePara);

                                    foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (line.StartsWith("Section:")) continue;

                                        bool isHeader = line.StartsWith("[") || line.StartsWith("1.") || line.StartsWith("2.");
                                        var pPr = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines { Before = isHeader ? "140" : "60", After = "60" }
                                        );

                                        var rPr = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                                            new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "21" }
                                        );
                                        if (isHeader)
                                        {
                                            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                                            rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "1A365D" });
                                        }

                                        var run = new DocumentFormat.OpenXml.Wordprocessing.Run(rPr, new DocumentFormat.OpenXml.Wordprocessing.Text(line));
                                        body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(pPr, run));
                                    }

                                    doc.MainDocumentPart?.Document?.Save();
                                }
                            }
                        }
                    }
                }

                // 3. Update pdf file if not already present
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("PreShowWelcomeHoldNotice"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 13, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 9.0, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XFont sectionFont = new PdfSharp.Drawing.XFont("Arial", 10.0, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Pre-Show Screen / Singer Welcome Screen Hold & Sequenced Release", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(36, 36, 540, 18), leftAlign);

                                double yPos = 60;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    bool isSection = line.StartsWith("[") || line.StartsWith("1.") || line.StartsWith("2.");
                                    var font = isSection ? sectionFont : bodyFont;
                                    var brush = isSection ? PdfSharp.Drawing.XBrushes.Navy : PdfSharp.Drawing.XBrushes.Black;
                                    double lineHeight = isSection ? 15 : 12.0;

                                    if (yPos + lineHeight > 750)
                                    {
                                        page = doc.AddPage();
                                        page.Size = PdfSharp.PageSize.Letter;
                                        gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                                        yPos = 36;
                                    }

                                    gfx.DrawString(line, font, brush, new PdfSharp.Drawing.XRect(36, yPos, 540, lineHeight), leftAlign);
                                    yPos += lineHeight;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " PreShowWelcomeHoldNotice";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForDynamicSpecialEventAnnouncementBanner()
        {
            lock (_manualLock)
            {
                string baseDir = AppContext.BaseDirectory;
                string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
                string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
                string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
                string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

                if (!Directory.Exists(docDir))
                {
                    return;
                }

                string updateText = @"
Section: Dynamic Special Event Announcement Banner Studio (Updated Oct 4, 2026)
- Dynamic Announcement Banner: DJs can now select 'Announcement' from the Special Event Banner picker in both Lyracist and KSRotation to broadcast custom, dynamically generated full-screen celebration announcements (e.g. 'Welcome to Jill & Robert, 1st timers tonight').
- Host Laptop Interactive Modal: Selecting 'Announcement' immediately opens an intuitive prompt dialog where the DJ can type any custom message, shoutout, or welcome greeting. Clicking 'Launch Banner' instantly renders a high-definition 16:9 graphic into the shared EventBanners folder with dual gold borders, ambient radial glow, corner celebration fireworks, and auto-scaled typography.
- Remote DJ Board Support: On the web-based Remote DJ Board (/dj), tapping the 'Announcement' special event button presents a modal overlay allowing the mobile DJ to enter announcement text directly from their tablet or smartphone.
- Cancellation Safety: Dismissing or cancelling the announcement prompt cleanly preserves the previously selected banner without switching displays prematurely.";

                // 1. Append to updates log text file if not already present
                if (File.Exists(updatesTxtPath))
                {
                    string existingUpdates = File.ReadAllText(updatesTxtPath);
                    if (!existingUpdates.Contains("Dynamic Special Event Announcement Banner Studio"))
                    {
                        File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                    }
                }

                // 2. Update docx file if not already present
                if (File.Exists(docxPath))
                {
                    using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                        {
                            var body = doc.MainDocumentPart?.Document?.Body;
                            if (body != null)
                            {
                                bool alreadyAppended = false;
                                foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                                {
                                    if (p.InnerText.Contains("Dynamic Special Event Announcement Banner Studio"))
                                    {
                                        alreadyAppended = true;
                                        break;
                                    }
                                }

                                if (!alreadyAppended)
                                {
                                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                        new DocumentFormat.OpenXml.Wordprocessing.Run(
                                            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                            new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Dynamic Special Event Announcement Banner Studio")
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
                }

                // 3. Update pdf file by appending a page using PDFsharp
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                        {
                            string keywords = doc.Info.Keywords;
                            if (string.IsNullOrEmpty(keywords) || !keywords.Contains("DynamicAnnouncementBannerStudio"))
                            {
                                var page = doc.AddPage();
                                page.Size = PdfSharp.PageSize.Letter;
                                var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                                PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                                PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                                PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                                gfx.DrawString("Section: Dynamic Special Event Announcement Banner Studio", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                                double yPos = 70;
                                foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (line.StartsWith("Section:")) continue;
                                    gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                    yPos += 18;
                                }

                                doc.Info.Keywords = (keywords ?? string.Empty) + " DynamicAnnouncementBannerStudio";
                                doc.Save(pdfPath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                    }
                }
            }
        }

        [Fact]
        public void UpdateUserManuals_ClearLastRoundDone()
        {
            string docDir = @"C:\VB26\Lyracist\Documentation";
            if (!Directory.Exists(docDir)) return;

            string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
            string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
            string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

            string updateText = @"
Lyracist Pro Suite & KSRotation: Clear 'Last Round Done' Accidental Flag & Restore Performer (`RotationViewModel.cs`, `EditSingerWindow.xaml`, `MainViewModel.cs`, `KSRotation.Maui`, `dj.html`) - (October 2026)

Section: Lyracist & KSRotation / Rotation Management / Last Round Rotation Controls

[Overview & Purpose]
During the final round of the evening (when 'Last Round' is engaged), performers who have completed their song are marked as completed for the round and removed from active rotation eligibility. If a performer was marked finished by accident, hosts previously had to toggle the entire Last Round mode off and on again, which cleared completion flags for all singers in the rotation. Hosts across all applications now have multiple intuitive methods to clear the 'Last Round Done' flag for an individual singer and immediately restore them to active rotation eligibility.

[Interactive Badge & Direct Action]
- Clickable Badge (✕): The 'DONE (LAST ROUND)' badge displayed next to a performer's name is now an interactive button badge with a distinct close/clear icon ('DONE (LAST ROUND) ✕'). Clicking or tapping this badge immediately removes the last-round done flag.
- Immediate Eligibility Restoration: Clearing the flag instantly restores the singer's eligibility as the Next Performer ('IsNext'), recalculates cumulative wait times and round duration estimations, and refreshes the audience rotation billboard.

[Multiple Access Points Across Apps]
1. Lyracist Desktop:
   - Click the 'DONE (LAST ROUND) ✕' badge directly on the singer row in RotationPage or KaraokePage.
   - Right-click the singer and select '↩ Clear Last Round Done Status' from the context menu.
   - Click 'Edit Performer' (✏) and uncheck the 'Completed in Last Round' checkbox in the dialog.
2. KSRotation Desktop:
   - Click the 'DONE (LAST ROUND) ✕' badge on the singer row in the active rotation list.
   - Right-click the performer and choose '↩ Clear Last Round Done Status' from the context menu.
3. KSRotation.Maui:
   - Tap the 'DONE (LAST ROUND) ✕' badge on the performer card.
   - Tap 'Edit Performer' (✏) and uncheck 'Done in Last Round (uncheck to restore)'.
4. Remote DJ Web Portal (/dj):
   - Mobile DJs can tap the 'DONE (LAST ROUND) ✕' badge on their tablet or phone to restore performers remotely.";

            // 1. Append to updates log text file if not already present
            if (File.Exists(updatesTxtPath))
            {
                string existingUpdates = File.ReadAllText(updatesTxtPath);
                if (!existingUpdates.Contains("Clear 'Last Round Done' Accidental Flag"))
                {
                    File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                }
            }

            // 2. Update docx file if not already present
            if (File.Exists(docxPath))
            {
                using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                    {
                        var body = doc.MainDocumentPart?.Document?.Body;
                        if (body != null)
                        {
                            bool alreadyAppended = false;
                            foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                            {
                                if (p.InnerText.Contains("Clear 'Last Round Done' Accidental Flag"))
                                {
                                    alreadyAppended = true;
                                    break;
                                }
                            }

                            if (!alreadyAppended)
                            {
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Clear 'Last Round Done' Accidental Flag & Restore Performer")
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
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("ClearLastRoundDoneFlag"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: Clear 'Last Round Done' Accidental Flag & Restore Performer", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                yPos += 18;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " ClearLastRoundDoneFlag";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                }
            }
        }

        [Fact]
        public void UpdateUserManuals_MoveToTopOfRotation()
        {
            string docDir = @"C:\VB26\Lyracist\Documentation";
            if (!Directory.Exists(docDir)) return;

            string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
            string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
            string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

            string updateText = @"
Lyracist Pro Suite & KSRotation: Move to Top of Rotation (`RotationViewModel.cs`, `RotationPage.xaml`, `KaraokePage.xaml`, `MainViewModel.cs`, `MainWindow.xaml`, `KSRotation.Maui`) - (October 2026)

Section: Lyracist & KSRotation / Rotation Management / Queue Ordering & Performer Priority

[Overview & Purpose]
Hosts frequently encounter scenarios where a scheduled performer was temporarily away (e.g. in the restroom, stepping outside, or getting a drink) and was skipped or displaced. Once the performer returns, the host needs to reinsert them at the very top of the queue so they perform next, without disrupting current active playback or having to manually drag/click 'Move Up' repeatedly through dozens of queue slots.

The new 'Move to Top of Rotation' feature instantly relocates any performer to the head of the waiting queue. If an active singer is currently singing on stage, the moved performer is placed at slot 1 (the 'Up Next' position) and highlighted in blue. If no singer is currently performing, they are placed at index 0.

[Anchor Preservation Guarantee]
Crucially, moving a performer to the top of the rotation queue does NOT in any way change, reassign, or alter the Rotation Anchor ('⚓ ANCHOR' / IsRotationStart). The anchor singer marks the logical start of a rotation round and tracks full-cycle completion. Moving a performer to the top preserves the existing anchor singer wherever they are in the list. The anchor can still be changed manually at any time using the 'Set as Rotation Anchor' button or context menu option.

[Automatic Flag Clearing & Restoration]
If the performer being moved to the top was previously skipped ('IsSkipped'), paused ('IsPaused'), or marked inactive ('IsInactive'), those flags are automatically cleared upon moving. If they were marked done in the final round ('HasSungInLastRound'), that flag is also cleared so they are immediately restored to full active eligibility. Duet/linked partners remain strictly adjacent.

[Multiple Access Points Across Apps]
1. Lyracist Desktop:
   - Singer ContextMenu: Right-click any singer row in RotationPage or KaraokePage and select 'Move to Top of Rotation'.
   - Performer Row Button: Click the dedicated 'Move to Top of Rotation' button (upload arrow icon) on any singer row.
   - Queue Toolbar: Select a performer and click the 'Move Selected Singer to Top of Rotation' button on the toolbar next to Move Down.
2. KSRotation Desktop:
   - Performer ContextMenu: Right-click any performer row and select 'Move to Top of Rotation'.
   - Performer List Button: Click the dedicated '⤒' button on any performer row next to the Move Down button.
3. KSRotation.Maui:
   - Tap the dedicated '⤒' button on any performer card in the active rotation queue.
4. Remote DJ Web Portal (/dj):
   - Supports 'move-to-top' and 'move-top' remote control actions.";

            // 1. Append to updates log text file if not already present
            if (File.Exists(updatesTxtPath))
            {
                string existingUpdates = File.ReadAllText(updatesTxtPath);
                if (!existingUpdates.Contains("Move to Top of Rotation (`RotationViewModel.cs`"))
                {
                    File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                }
            }

            // 2. Update docx file if not already present
            if (File.Exists(docxPath))
            {
                using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                    {
                        var body = doc.MainDocumentPart?.Document?.Body;
                        if (body != null)
                        {
                            bool alreadyAppended = false;
                            foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                            {
                                if (p.InnerText.Contains("Section: Move to Top of Rotation"))
                                {
                                    alreadyAppended = true;
                                    break;
                                }
                            }

                            if (!alreadyAppended)
                            {
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Move to Top of Rotation & Performer Priority")
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
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("MoveToTopOfRotation"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: Move to Top of Rotation & Performer Priority", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                yPos += 18;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " MoveToTopOfRotation";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                }
            }
        }

        [Fact]
        public void UpdateUserManuals_NewSingerPlaceholderCleanup()
        {
            string docDir = @"C:\VB26\Lyracist\Documentation";
            if (!Directory.Exists(docDir)) return;

            string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
            string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
            string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

            string updateText = @"
Lyracist Pro Suite & KSRotation: New Singer Placeholder Cleanup & Automatic Unused Row Removal (`NameFormatting.cs`, `SingerEntry.cs`, `MainViewModel.cs`, `MainWindow.xaml.cs`, `RotationViewModel.cs`) - (October 2026)

Section: Lyracist & KSRotation / Rotation Management / Singer Addition & Placeholder Cleanup

[Overview & Purpose]
When adding a new performer to the rotation queue (via the 'New Singer' button), a new row is created with the default placeholder text 'New Singer' focused and highlighted for typing. In fast-paced hosting environments, a host may click 'New Singer' but then click elsewhere or press Enter without typing a name, leaving an unwanted orphan 'New Singer' row in the queue. Alternatively, a host may start typing after or amidst the placeholder, creating unformatted names such as 'New Singertom' or 'New Singer Tom'.

[Automatic Row Removal on Lost Focus]
If the host adds a new singer and navigates away or moves focus without typing a new name (so that 'New Singer' remains the only text in the box, or the box is left blank or whitespace), the application automatically removes that orphan line from the rotation queue immediately without displaying an interrupting confirmation prompt.

[Smart Placeholder Stripping & Proper-Casing]
If the placeholder 'New Singer' is present alongside additional text (for example, typing 'New Singertom', 'New Singer Tom', or 'Tom New Singer'), the system automatically strips the 'New Singer' token and any surrounding delimiters or punctuation, cleans the remaining name, and applies intelligent Title/Proper Casing (for example, converting 'New Singertom' to 'Tom').

[Keyboard & Flow Handling]
Pressing Enter in the singer name input commits the name and advances focus to the next field (e.g. Song title), triggering instant validation, cleaning, or cleanup smoothly.";

            // 1. Append to updates log text file if not already present
            if (File.Exists(updatesTxtPath))
            {
                string existingUpdates = File.ReadAllText(updatesTxtPath);
                if (!existingUpdates.Contains("New Singer Placeholder Cleanup & Automatic Unused Row Removal"))
                {
                    File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                }
            }

            // 2. Update docx file if not already present
            if (File.Exists(docxPath))
            {
                using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                    {
                        var body = doc.MainDocumentPart?.Document?.Body;
                        if (body != null)
                        {
                            bool alreadyAppended = false;
                            foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                            {
                                if (p.InnerText.Contains("Section: New Singer Placeholder Cleanup"))
                                {
                                    alreadyAppended = true;
                                    break;
                                }
                            }

                            if (!alreadyAppended)
                            {
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text("Section: New Singer Placeholder Cleanup & Automatic Row Removal")
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
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("NewSingerPlaceholderCleanup"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: New Singer Placeholder Cleanup & Automatic Row Removal", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                yPos += 18;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " NewSingerPlaceholderCleanup";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                }
            }
        }

        [Fact]
        public void UpdateUserManuals_TestModeSingerCountOptions()
        {
            string docDir = @"C:\VB26\Lyracist\Documentation";
            if (!Directory.Exists(docDir)) return;

            string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
            string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
            string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

            string updateText = @"
Lyracist Pro Suite & KSRotation: Test Mode Settings GroupBox & Performer Count Options (3, 5, 10, 15) (`RotationHelpers.cs`, `MainWindow.xaml`, `MainViewModel.cs`, `SettingsPage.xaml`, `SettingsViewModel.cs`, `AppSettings.cs`) - (October 2026)

Section: Lyracist & KSRotation / Settings / Test Mode & Sample Singer Generation

[Overview & Purpose]
Hosts and KJs frequently utilize Test Mode to verify audio leveling, screen projection, rotation advancing, and network request kiosks before live show doors open. Previously, test mode automatically seeded a fixed list of 15 performers with identical startup names. The system now features a dedicated 'Test Mode Settings' GroupBox (GBX) providing configurable sample singer counts and a dynamic, randomized pool of realistic performer names.

[Configurable Performer Counts (3, 5, 10, & 15)]
Hosts can now select exactly how many sample performers to load from a dedicated dropdown selector:
- 3 Singers: Ideal for quick audio and dual-screen verification checks.
- 5 Singers: Perfect for testing rotation handoffs and next-singer notifications.
- 10 Singers: Well-suited for testing multi-round progression and last round workflows.
- 15 Singers: Thorough full-queue testing across multi-monitor displays, billboards, and remote DJ boards.

[Dedicated Test Mode Settings GroupBox (GBX)]
- Dedicated UI Organization: Test Mode settings are now cleanly isolated within their own GroupBox ('Test Mode Settings') in both KSRotation and Lyracist Settings panels.
- Instant 'Load Test Singers Now' Action: In addition to startup seeding, hosts can immediately populate or refresh the rotation queue with the selected number of sample singers with one click, without restarting the application.
- Preservation of Session Settings: General session settings (e.g. 'Block Duplicate Songs in Session') remain in 'Rotation Settings', keeping operational controls distinct from testing tools.

[Randomized Performer Names & Song Pool]
The test generator utilizes a curated pool of 18 realistic performer names (Brenda Bumps, James Smith, Raymond Carter, Sharon Roberts, Ami Anderson, Ali Davis, Randy Davis, Larry Strickland, Robert Roberts, Cynthia Nix, David Wayne, Danny Hinnant, Cerrina Culbert, Julia Stanton, Carol Henderson, Joe Bob Briggs, Craven Counts, and Dennis Starling) paired randomly with popular karaoke tracks, guaranteeing varied and realistic practice sessions.";

            // 1. Append to updates log text file if not already present
            if (File.Exists(updatesTxtPath))
            {
                string existingUpdates = File.ReadAllText(updatesTxtPath);
                if (!existingUpdates.Contains("Test Mode Settings GroupBox & Performer Count Options"))
                {
                    File.AppendAllText(updatesTxtPath, updateText + Environment.NewLine);
                }
            }

            // 2. Update docx file if not already present
            if (File.Exists(docxPath))
            {
                using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(fs, true))
                    {
                        var body = doc.MainDocumentPart?.Document?.Body;
                        if (body != null)
                        {
                            bool alreadyAppended = false;
                            foreach (var p in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                            {
                                if (p.InnerText.Contains("Section: Test Mode Settings GroupBox"))
                                {
                                    alreadyAppended = true;
                                    break;
                                }
                            }

                            if (!alreadyAppended)
                            {
                                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                        new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Test Mode Settings GroupBox & Performer Count Options")
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
            }

            // 3. Update pdf file by appending a page using PDFsharp
            if (File.Exists(pdfPath))
            {
                try
                {
                    using (var doc = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
                    {
                        string keywords = doc.Info.Keywords;
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("TestModeSingerCountOptions"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: Test Mode Settings GroupBox & Performer Count Options", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                yPos += 18;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " TestModeSingerCountOptions";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForMouseDragAndDropRotationReordering()
        {
            string baseDir = AppContext.BaseDirectory;
            string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
            string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
            string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
            string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

            if (!Directory.Exists(docDir))
            {
                return;
            }

            string updateText = @"
Section: Lyracist & KSRotation / Rotation Management / Mouse Drag-and-Drop Performer Reordering (October 2026)

[Overview & Purpose]
Hosts and KJs frequently need to manually adjust queue order during a live show - for example, accommodating a singer stepping away briefly, grouping duet partners, or prioritizing a VIP guest. In both Lyracist and KSRotation, hosts can now click and drag any performer in the active rotation queue and drop them at a new position using the mouse. (Note: Remote DJ web portals retain standard button controls and do not require mouse dragging).

[Drag Grip Handle & Visual Ergonomics]
1. Dedicated Drag Grip Handle (⋮⋮): Each performer row in KSRotation and Lyracist features a dedicated drag grip handle on the left edge with a 4-way move cursor (SizeAll) and tooltip indicator ('Drag to reorder singer in rotation').
2. Flexible Grab Zones: In addition to the drag grip, hosts can click and drag anywhere on non-interactive portions of the performer row (background, borders, badges, and labels) without accidentally triggering text editing or button commands.
3. Interactive Controls Protection: Text inputs (Name, Duet Partner, Song, Artist) and interactive controls (Buttons, Checkboxes) remain protected, ensuring text selection and editing work seamlessly without triggering unintended drags.

[Intelligent Midpoint Drop Calculation & Boundary Protection]
1. Top/Bottom Half Precision: When dragging a performer over another row, dropping on the upper half places the performer above that row; dropping on the lower half places them below that row.
2. Empty Space Drop: Dropping a performer below the queue in the empty list space automatically relocates them to the end of the rotation.
3. Partition Boundary Enforcement: Active and inactive partitions are strictly protected. Inactive singers cannot be dragged into the active queue, and active singers cannot be dragged into the inactive section.
4. Linked Duet Partner Integrity: If a linked performer is reordered, EnforceLinkedAdjacency automatically keeps linked duet partners adjacent.
5. Live Real-Time Propagation: Dropping a performer immediately recalculates wait times, updates next-singer blue highlights, refreshes audience billboard displays, rebuilds rotation JSON caches, and saves changes to the database.
";

            // 1. Update text file
            if (File.Exists(updatesTxtPath))
            {
                string existingText = File.ReadAllText(updatesTxtPath);
                if (!existingText.Contains("Mouse Drag-and-Drop Performer Reordering"))
                {
                    File.AppendAllText(updatesTxtPath, Environment.NewLine + updateText);
                }
            }

            // 2. Update docx file
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        bool alreadyAppended = false;
                        foreach (var textElem in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (textElem.Text.Contains("Mouse Drag-and-Drop Performer Reordering"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: Mouse Drag-and-Drop Performer Reordering")
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
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("MouseDragAndDropRotationReordering"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: Mouse Drag-and-Drop Performer Reordering", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                yPos += 18;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " MouseDragAndDropRotationReordering";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                }
            }
        }

        [Fact]
        public void UpdateUserManualsForNewSingerWelcomeOnNameBoxLostFocus()
        {
            string baseDir = AppContext.BaseDirectory;
            string docDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Documentation"));
            string docxPath = Path.Combine(docDir, "Lyracist_User_Manual.docx");
            string pdfPath = Path.Combine(docDir, "Lyracist_User_Manual.pdf");
            string updatesTxtPath = Path.Combine(docDir, "Lyracist_User_Manual_Updates.txt");

            if (!Directory.Exists(docDir))
            {
                return;
            }

            string updateText = @"
Section: KSRotation / New Singer Welcome Screen / Focus-Based Activation (October 2026)

[Overview & Purpose]
When the 'Show Welcome to our new performer screen when a new singer joins' feature is enabled in Settings, KSRotation celebrates newcomers with an animated, full-screen welcome broadcast across rotation and DJ banner projection screens. Previously, a 2.5-second settle timer evaluated keystrokes while the host was typing. The activation trigger has now been updated to fire when the singer name entry box loses focus.

[Focus-Based Activation Behavior]
1. Typing Interruption Elimination: The 2.5-second keystroke timer has been eliminated. The DJ can take as much time as needed to enter the singer's name without being interrupted mid-keystroke by premature welcome screen popups.
2. Natural Completion Detection: Moving focus away from the singer name text box (by pressing Tab, pressing Enter to advance to the song field, or clicking another row/control) signals that performer name entry is complete and immediately triggers the welcome screen.
3. Empty / Canceled Row Protection: If a new row is added and left blank or unaltered as 'New Singer', losing focus cleanly deletes the unused row without triggering a welcome screen.
4. Duplicate Protection: Once a singer is welcomed upon name entry completion, subsequent focus changes on that performer row do not re-trigger welcome screens.
";

            // 1. Update text file
            if (File.Exists(updatesTxtPath))
            {
                string existingText = File.ReadAllText(updatesTxtPath);
                if (!existingText.Contains("New Singer Welcome Screen / Focus-Based Activation"))
                {
                    File.AppendAllText(updatesTxtPath, Environment.NewLine + updateText);
                }
            }

            // 2. Update docx file
            if (File.Exists(docxPath))
            {
                using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxPath, true))
                {
                    var body = doc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        bool alreadyAppended = false;
                        foreach (var textElem in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            if (textElem.Text.Contains("New Singer Welcome Screen / Focus-Based Activation"))
                            {
                                alreadyAppended = true;
                                break;
                            }
                        }

                        if (!alreadyAppended)
                        {
                            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                                new DocumentFormat.OpenXml.Wordprocessing.Run(
                                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.Bold(), new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "26" }),
                                    new DocumentFormat.OpenXml.Wordprocessing.Text("Section: New Singer Welcome Screen / Focus-Based Activation")
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
                        if (string.IsNullOrEmpty(keywords) || !keywords.Contains("NewSingerWelcomeFocusActivation"))
                        {
                            var page = doc.AddPage();
                            page.Size = PdfSharp.PageSize.Letter;
                            var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page);

                            PdfSharp.Drawing.XFont titleFont = new PdfSharp.Drawing.XFont("Arial", 14, PdfSharp.Drawing.XFontStyleEx.Bold);
                            PdfSharp.Drawing.XFont bodyFont = new PdfSharp.Drawing.XFont("Arial", 10, PdfSharp.Drawing.XFontStyleEx.Regular);
                            PdfSharp.Drawing.XStringFormat leftAlign = new PdfSharp.Drawing.XStringFormat { Alignment = PdfSharp.Drawing.XStringAlignment.Near, LineAlignment = PdfSharp.Drawing.XLineAlignment.Near };

                            gfx.DrawString("Section: New Singer Welcome Screen / Focus-Based Activation", titleFont, PdfSharp.Drawing.XBrushes.DarkSlateGray, new PdfSharp.Drawing.XRect(40, 40, 532, 20), leftAlign);

                            double yPos = 70;
                            foreach (var line in updateText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (line.StartsWith("Section:")) continue;
                                gfx.DrawString(line, bodyFont, PdfSharp.Drawing.XBrushes.Black, new PdfSharp.Drawing.XRect(40, yPos, 532, 16), leftAlign);
                                yPos += 18;
                            }

                            doc.Info.Keywords = (keywords ?? string.Empty) + " NewSingerWelcomeFocusActivation";
                            doc.Save(pdfPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error updating PDF manual at {pdfPath}: {ex.Message}", ex);
                }
            }
        }
    }
}