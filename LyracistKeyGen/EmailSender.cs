// Edited on Aug 30, 2026 @ 08:26:00 -> Update settings path to Settings/keygen_settings.json with legacy fallback migration
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Lyracist.Shared;

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
        private static readonly string SettingsPath = Path.Combine(Globals.SettingsDir, "keygen_settings.json");

        public static SmtpSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    // Fallback migration: Check legacy path
                    string legacyPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "LyracistKeyGen",
                        "smtp_settings.json"
                    );
                    if (File.Exists(legacyPath))
                    {
                        Directory.CreateDirectory(Globals.SettingsDir);
                        File.Copy(legacyPath, SettingsPath, true);
                    }
                }

                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<SmtpSettings>(json) ?? new SmtpSettings();
                    settings.Password = EncryptionHelper.Decrypt(settings.Password);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Failed to load SMTP settings: {ex}");
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

                var clone = new SmtpSettings
                {
                    SmtpHost = settings.SmtpHost,
                    SmtpPort = settings.SmtpPort,
                    Username = settings.Username,
                    Password = Lyracist.Shared.EncryptionHelper.Encrypt(settings.Password),
                    EnableSsl = settings.EnableSsl,
                    FromAddress = settings.FromAddress
                };

                string json = JsonSerializer.Serialize(clone, new JsonSerializerOptions { WriteIndented = true });
                if (File.Exists(SettingsPath))
                {
                    File.ReadAllText(SettingsPath); // Force write handle check if file exists
                }
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Failed to save SMTP settings: {ex}");
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
