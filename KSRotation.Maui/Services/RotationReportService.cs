// Edited on Oct 22, 2026 @ 10:25:00 -> Use MMMddyy date format in email compose subject
using KSRotation.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using System.Globalization;
using System.IO;

namespace KSRotation.Services
{
    public static class RotationReportService
    {
        /// <summary>
        /// Saves the night's rotation report in CSV and PDF formats, and — if requested — opens the
        /// device's email app with the reports attached and ready to send.
        /// </summary>
        /// <param name="singers">Active queue of singers.</param>
        /// <param name="history">Completed performances list.</param>
        /// <param name="venueName">Venue name.</param>
        /// <param name="emailRecipient">Email recipient(s).</param>
        /// <param name="sendEmail">Whether to open a pre-filled email with the reports attached.</param>
        /// <returns>The PDF and CSV report paths (always written if this method returns normally), plus
        /// an EmailError message if the report was saved but composing the email failed — callers should
        /// still treat a non-null EmailError as an overall success for the report itself.</returns>
        public static async Task<(string PdfPath, string CsvPath, string? EmailError)> SaveAsync(
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

            DateTime perfDate = RotationReportGenerator.GetPerformanceDate(historyList);
            string baseFileName = RotationReportGenerator.BuildReportBaseFileName(venueName, perfDate);

            var (pdfPath, csvPath) = await Task.Run(() =>
            {
                string pdf = RotationReportGenerator.GeneratePdf(singersList, historyList, venueName, baseFileName);
                string csv = RotationReportGenerator.GenerateCsv(singersList, historyList, venueName, baseFileName);
                return (pdf, csv);
            });

            // The PDF/CSV are already safely on disk at this point. Composing the email is a best-effort
            // extra step — a failure here (no mail app, attachment issue) must not make the caller think
            // the whole save failed and skip flushing the database / clearing the queue.
            string? emailError = null;
            if (sendEmail && !string.IsNullOrWhiteSpace(emailRecipient))
            {
                try
                {
                    await ComposeEmailAsync(emailRecipient.Trim(), venueName, perfDate, pdfPath, csvPath);
                }
                catch (Exception ex)
                {
                    emailError = ex.Message;
                }
            }

            return (pdfPath, csvPath, emailError);
        }

        private static async Task ComposeEmailAsync(string emailRecipient, string venueName, DateTime perfDate, string pdfPath, string csvPath)
        {
            try
            {
                var recipients = emailRecipient
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();

                string dateStr = perfDate.ToString("MMMddyy", CultureInfo.InvariantCulture);
                var message = new EmailMessage
                {
                    Subject = string.IsNullOrWhiteSpace(venueName)
                        ? $"Karaoke Rotation Report - {dateStr}"
                        : $"Karaoke Rotation Report - {venueName} - {dateStr}",
                    Body = string.IsNullOrWhiteSpace(venueName)
                        ? $"Attached are the Karaoke Rotation Reports (PDF and CSV formats) for {perfDate:dddd, MMMM d, yyyy}."
                        : $"Attached are the Karaoke Rotation Reports (PDF and CSV formats) for {venueName} on {perfDate:dddd, MMMM d, yyyy}.",
                    To = recipients,
                    Attachments = []
                };

                if (File.Exists(pdfPath))
                {
                    message.Attachments.Add(new EmailAttachment(pdfPath));
                }
                if (File.Exists(csvPath))
                {
                    message.Attachments.Add(new EmailAttachment(csvPath));
                }

                if (!Email.Default.IsComposeSupported)
                {
                    throw new InvalidOperationException("No email app is available on this device.");
                }

                await MainThread.InvokeOnMainThreadAsync(() => Email.Default.ComposeAsync(message));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to open the email draft.", ex);
            }
        }
    }
}
