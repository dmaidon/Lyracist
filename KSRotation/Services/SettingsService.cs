// Last Edit: Jun 30, 2026 06:47 - Switched settings directory resolution to shared AppPaths helper.
using KSRotation.Models;
using Lyracist.Shared;
using System.IO;
using System.Text.Json;

namespace KSRotation.Services
{
    public static class SettingsService
    {
        private const string SettingsFileName = "appsettings.json";

        private static readonly JsonSerializerOptions SerializerOptions = AppJsonContext.Default.Options;

        private static string SettingsDirectoryPath => AppPaths.SettingsDirectoryPath;

        private static string SettingsFilePath => Path.Combine(SettingsDirectoryPath, SettingsFileName);

        /// <summary>
        /// Loads persisted application settings from disk.
        /// </summary>
        public static AppSettings Load()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                using FileStream stream = File.OpenRead(SettingsFilePath);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(stream, SerializerOptions);
                return settings ?? new AppSettings();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"The settings file at '{SettingsFilePath}' is invalid JSON.", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to read settings from '{SettingsFilePath}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Access denied while reading settings from '{SettingsFilePath}'.", ex);
            }
        }

        /// <summary>
        /// Saves application settings to disk.
        /// </summary>
        /// <param name="settings">The settings values to persist.</param>
        public static void Save(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            try
            {
                AtomicJsonFile.Serialize(SettingsFilePath, settings, SerializerOptions);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to save settings to '{SettingsFilePath}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Access denied while saving settings to '{SettingsFilePath}'.", ex);
            }
        }
    }
}