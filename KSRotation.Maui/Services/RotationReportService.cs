// Created on Jul 27, 2026 @ 10:00:00 -> Maui-specific report delivery: reuses the shared
// RotationReportGenerator (PDF/CSV) but hands off the email step to the device's own mail app
// instead of the desktop .eml-draft-and-launch trick KSRotation's own RotationReportService uses.
using KSRotation.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
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
        /// <returns>A tuple containing the PDF and CSV report paths.</returns>
        public static async Task<(string PdfPath, string CsvPath)> SaveAsync(
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

            var (pdfPath, csvPath) = await Task.Run(() =>
            {
                string pdf = RotationReportGenerator.GeneratePdf(singersList, historyList, venueName);
                string csv = RotationReportGenerator.GenerateCsv(singersList, historyList);
                return (pdf, csv);
            });

            if (sendEmail && !string.IsNullOrWhiteSpace(emailRecipient))
            {
                await ComposeEmailAsync(emailRecipient.Trim(), venueName, pdfPath, csvPath);
            }

            return (pdfPath, csvPath);
        }

        private static async Task ComposeEmailAsync(string emailRecipient, string venueName, string pdfPath, string csvPath)
        {
            try
            {
                var recipients = emailRecipient
                    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();

                var message = new EmailMessage
                {
                    Subject = $"Karaoke Rotation Report - {venueName} - {DateTime.Now:yyyy-MM-dd}",
                    Body = $"Attached are the Karaoke Rotation Reports (PDF and CSV formats) for {venueName} on {DateTime.Now:dddd, MMMM d, yyyy}.",
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
