using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace LyracistKeyGen
{
    public class SmtpSettings
    {
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool EnableSsl { get; set; } = true;
        public string FromAddress { get; set; } = "";
    }

    public static class EmailSender
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LyracistKeyGen",
            "smtp_settings.json"
        );

        public static SmtpSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<SmtpSettings>(json) ?? new SmtpSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load SMTP settings: {ex.Message}");
            }
            return new SmtpSettings();
        }

        public static void SaveSettings(SmtpSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.ReadAllText(SettingsPath); // Force write handle check if file exists
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save SMTP settings: {ex.Message}");
            }
        }

        public static void SendLicenseKey(SmtpSettings settings, string recipientEmail, string recipientName, string key)
        {
            if (string.IsNullOrWhiteSpace(settings.SmtpHost) || string.IsNullOrWhiteSpace(settings.Username))
            {
                throw new InvalidOperationException("SMTP host or username credentials are not configured.");
            }

            using var mail = new MailMessage();
            mail.From = new MailAddress(settings.FromAddress);
            mail.To.Add(recipientEmail);
            mail.Subject = "Your Lyracist License Key";
            mail.Body = $@"Hi {recipientName},

Thank you for using Lyracist!

Here is your generated Lyracist Pro registration key:
{key}

Best regards,
Lyracist Licensing Service";

            using var smtp = new SmtpClient(settings.SmtpHost, settings.SmtpPort);
            smtp.Credentials = new NetworkCredential(settings.Username, settings.Password);
            smtp.EnableSsl = settings.EnableSsl;
            smtp.Send(mail);
        }

        public static void TestConnection(SmtpSettings settings, string testRecipient)
        {
            SendLicenseKey(settings, testRecipient, "Test Recipient", "XXXXX-XXXXX-XXXXX");
        }
    }
}
