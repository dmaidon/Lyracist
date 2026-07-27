// Last Edit: Jul 27, 2026 10:00 - Split PDF/CSV generation out into RotationReportGenerator (shared with
// KSRotation.Maui); this file now only holds the desktop-specific email-draft delivery mechanism.
using KSRotation.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;

namespace KSRotation.Services
{
    public static class RotationReportService
    {
        private static readonly char[] EmailSeparator = [',', ';'];

        private static string ReportDirectoryPath => AppPaths.ReportsDirectoryPath;

        /// <summary>
        /// Saves the night's rotation report in CSV and PDF formats, drafts an email with the reports attached,
        /// and opens it in the default mail client if configured.
        /// </summary>
        /// <param name="singers">Active queue of singers.</param>
        /// <param name="history">Completed performances list.</param>
        /// <param name="venueName">Venue name.</param>
        /// <param name="emailRecipient">Email recipient(s).</param>
        /// <param name="sendEmail">Whether to draft and open the email.</param>
        /// <returns>A tuple containing the PDF and CSV report paths.</returns>
        public static Task<(string PdfPath, string CsvPath)> SaveAsync(
            IEnumerable<SingerEntry> singers,
            IEnumerable<SongPerformance> history,
            string venueName,
            string? emailRecipient,
            bool sendEmail)
        {
            ArgumentNullException.ThrowIfNull(singers);
            ArgumentNullException.ThrowIfNull(history);

            List<SingerEntry> singersList = [.. singers];
            List<SongPerformance> historyList = [.. history];

            return Task.Run(() =>
            {
                string pdfPath = RotationReportGenerator.GeneratePdf(singersList, historyList, venueName);
                string csvPath = RotationReportGenerator.GenerateCsv(singersList, historyList);

                if (sendEmail && !string.IsNullOrWhiteSpace(emailRecipient))
                {
                    CreateEmlDraft(emailRecipient.Trim(), venueName, pdfPath, csvPath);
                }

                return (pdfPath, csvPath);
            });
        }

        private static void CreateEmlDraft(
            string emailRecipient,
            string venueName,
            string pdfPath,
            string csvPath)
        {
            try
            {
                using var mail = new MailMessage();
                mail.From = new MailAddress("no-reply@ksrotation.local", "Karaoke Rotation Manager");
                if (!string.IsNullOrWhiteSpace(emailRecipient))
                {
                    foreach (var email in emailRecipient.Split(EmailSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        mail.To.Add(email.Trim());
                    }
                }
                else
                {
                    mail.To.Add("recipient@example.com");
                }

                mail.Subject = $"Karaoke Rotation Report - {venueName} - {DateTime.Now:yyyy-MM-dd}";
                mail.Body = $"Attached are the Karaoke Rotation Reports (PDF and CSV formats) for {venueName} on {DateTime.Now:dddd, MMMM d, yyyy}.";
                mail.Headers.Add("X-Unsent", "1");

                if (File.Exists(pdfPath))
                {
                    mail.Attachments.Add(new Attachment(pdfPath));
                }
                if (File.Exists(csvPath))
                {
                    mail.Attachments.Add(new Attachment(csvPath));
                }

                // Setup SmtpClient to write to specified folder
                string tempPickupDir = Path.Combine(Path.GetTempPath(), "KSRotationEmailDrafts_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempPickupDir);

                using (var client = new SmtpClient())
                {
                    client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                    client.PickupDirectoryLocation = tempPickupDir;
                    client.Host = "localhost";
                    client.Send(mail);
                }

                // Find the created EML file in the pickup directory, then clean up temp dir
                string[] files = Directory.GetFiles(tempPickupDir, "*.eml");
                try
                {
                    if (files.Length > 0)
                    {
                        string sourceEml = files[0];
                        string destEmlName = $"Rotation_Report_{DateTime.Now:yyyyMMdd_HHmmss}.eml";
                        string destEmlPath = Path.Combine(ReportDirectoryPath, destEmlName);

                        Directory.CreateDirectory(ReportDirectoryPath);
                        File.Copy(sourceEml, destEmlPath, overwrite: true);

                        // Start process to open the EML file in the default client
                        Process.Start(new ProcessStartInfo(destEmlPath) { UseShellExecute = true });
                    }
                }
                finally
                {
                    try { Directory.Delete(tempPickupDir, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create or open the email draft.", ex);
            }
        }
    }
}
