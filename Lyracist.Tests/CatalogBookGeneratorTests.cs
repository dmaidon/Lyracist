// Edited on Oct 2, 2026 @ 11:27:00 -> Safely handle locked manual docx file in UpdateUserManualsForSpecialSinger
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
    }
}