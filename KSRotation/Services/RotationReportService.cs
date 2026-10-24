// Edited on Oct 22, 2026 @ 10:25:00 -> Use MMMddyy date format in EML draft subject
using KSRotation.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <returns>The PDF and CSV report paths (always written if this method returns normally), plus
        /// an EmailError message if the report was saved but the optional email draft step failed —
        /// callers should still treat a non-null EmailError as an overall success for the report itself.</returns>
        public static Task<(string PdfPath, string CsvPath, string? EmailError)> SaveAsync(
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
                DateTime perfDate = RotationReportGenerator.GetPerformanceDate(historyList);
                string baseFileName = RotationReportGenerator.BuildReportBaseFileName(venueName, perfDate);
                string pdfPath = RotationReportGenerator.GeneratePdf(singersList, historyList, venueName, baseFileName);
                string csvPath = RotationReportGenerator.GenerateCsv(singersList, historyList, venueName, baseFileName);

                // The PDF/CSV are already safely on disk at this point. Emailing them is a best-effort
                // extra step — a mail-client/SMTP-pickup failure here must not make the caller think the
                // whole save failed and skip flushing the database / clearing the queue.
                string? emailError = null;
                if (sendEmail && !string.IsNullOrWhiteSpace(emailRecipient))
                {
                    try
                    {
                        CreateEmlDraft(emailRecipient.Trim(), venueName, perfDate, baseFileName, pdfPath, csvPath);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError("RotationReportService.CreateEmlDraft", ex);
                        emailError = ex.Message;
                    }
                }

                return (pdfPath, csvPath, emailError);
            });
        }

        private static void CreateEmlDraft(
            string emailRecipient,
            string venueName,
            DateTime perfDate,
            string baseFileName,
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

                string dateStr = perfDate.ToString("MMMddyy", CultureInfo.InvariantCulture);
                mail.Subject = string.IsNullOrWhiteSpace(venueName)
                    ? $"Karaoke Rotation Report - {dateStr}"
                    : $"Karaoke Rotation Report - {venueName} - {dateStr}";
                mail.Body = string.IsNullOrWhiteSpace(venueName)
                    ? $"Attached are the Karaoke Rotation Reports (PDF and CSV formats) for {perfDate:dddd, MMMM d, yyyy}."
                    : $"Attached are the Karaoke Rotation Reports (PDF and CSV formats) for {venueName} on {perfDate:dddd, MMMM d, yyyy}.";
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
                        string destEmlName = $"{baseFileName}.eml";
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
