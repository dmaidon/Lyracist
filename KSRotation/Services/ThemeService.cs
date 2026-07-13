// Last Edit: Jun 30, 2026 08:40 - Live-follow Windows theme when "System" is selected via SystemEvents.UserPreferenceChanged.
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Windows.Media;

namespace KSRotation.Services
{
    /// <summary>
    /// Applies Material Design themes at runtime; extracted from MainViewModel so the logic is testable
    /// and MainViewModel does not need a direct reference to MaterialDesignThemes.Wpf.
    /// </summary>
    public static class ThemeService
    {
        private const string MaterialDesignPaperKey = "MaterialDesignPaper";
        private const string MaterialDesignCardBackgroundKey = "MaterialDesignCardBackground";
        private const string MaterialDesignBodyKey = "MaterialDesignBody";
        private const string MaterialDesignBodyLightKey = "MaterialDesignBodyLight";
        private const string AppContrastTextBrushKey = "AppContrastTextBrush";
        private const string AppHeaderBrushKey = "AppHeaderBrush";
        private const string AppSplitFlapBackgroundBrushKey = "AppSplitFlapBackgroundBrush";
        private const string AppSplitFlapBorderBrushKey = "AppSplitFlapBorderBrush";
        private static readonly SolidColorBrush DarkPaperOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x23, 0x26, 0x2E));
        private static readonly SolidColorBrush DarkCardOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x2C, 0x30, 0x39));
        private static readonly SolidColorBrush DarkBodyOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0xEE, 0xF2, 0xF7));
        private static readonly SolidColorBrush DarkBodyLightOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0xC9, 0xD1, 0xDA));
        private static readonly SolidColorBrush DarkContrastTextOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0xEE, 0xF2, 0xF7));
        private static readonly SolidColorBrush LightContrastTextOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
        private static readonly SolidColorBrush DarkHeaderOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0xC4, 0xB5, 0xFD));
        private static readonly SolidColorBrush LightHeaderOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED));
        private static readonly SolidColorBrush DarkSplitFlapBackgroundOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x3B, 0x41, 0x4D));
        private static readonly SolidColorBrush LightSplitFlapBackgroundOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x29, 0x3B));
        private static readonly SolidColorBrush DarkSplitFlapBorderOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x20, 0x24, 0x2D));
        private static readonly SolidColorBrush LightSplitFlapBorderOverride = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A));

        /// <summary>The most recently applied theme selection; used to decide whether to react to OS theme changes.</summary>
        private static string s_currentThemeName = "System";

        static ThemeService()
        {
            // When the user has "System" selected, follow live Windows light/dark switches.
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General)
            {
                return;
            }

            if (!string.Equals(s_currentThemeName, "System", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // UserPreferenceChanged may fire off the UI thread; marshal the re-apply onto the dispatcher.
            var app = System.Windows.Application.Current;
            if (app != null)
            {
                app.Dispatcher.BeginInvoke(new Action(() => Apply("System")));
            }
            else
            {
                Apply("System");
            }
        }

        /// <summary>
        /// Applies the named theme ("Light", "Dark", or anything else for System-follows-Windows).
        /// Exceptions are logged via <see cref="LoggerService"/> rather than surfaced to the caller.
        /// </summary>
        public static void Apply(string themeName)
        {
            try
            {
                s_currentThemeName = themeName;

                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();

                if (string.Equals(themeName, "Dark", StringComparison.OrdinalIgnoreCase))
                {
                    theme.SetBaseTheme(BaseTheme.Dark);
                }
                else if (string.Equals(themeName, "Light", StringComparison.OrdinalIgnoreCase))
                {
                    theme.SetBaseTheme(BaseTheme.Light);
                }
                else
                {
                    bool isDark = IsSystemDarkMode();
                    theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
                }

                paletteHelper.SetTheme(theme);
                ApplyBackgroundOverrides(theme.GetBaseTheme() == BaseTheme.Dark);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("ThemeService.Apply", ex);
            }
        }

        private static void ApplyBackgroundOverrides(bool isDark)
        {
            if (System.Windows.Application.Current?.Resources is not System.Windows.ResourceDictionary resources)
            {
                return;
            }

            if (isDark)
            {
                resources[MaterialDesignPaperKey] = DarkPaperOverride;
                resources[MaterialDesignCardBackgroundKey] = DarkCardOverride;
                resources[MaterialDesignBodyKey] = DarkBodyOverride;
                resources[MaterialDesignBodyLightKey] = DarkBodyLightOverride;
                resources[AppContrastTextBrushKey] = DarkContrastTextOverride;
                resources[AppHeaderBrushKey] = DarkHeaderOverride;
                resources[AppSplitFlapBackgroundBrushKey] = DarkSplitFlapBackgroundOverride;
                resources[AppSplitFlapBorderBrushKey] = DarkSplitFlapBorderOverride;
                return;
            }

            resources.Remove(MaterialDesignPaperKey);
            resources.Remove(MaterialDesignCardBackgroundKey);
            resources.Remove(MaterialDesignBodyKey);
            resources.Remove(MaterialDesignBodyLightKey);
            resources[AppContrastTextBrushKey] = LightContrastTextOverride;
            resources[AppHeaderBrushKey] = LightHeaderOverride;
            resources[AppSplitFlapBackgroundBrushKey] = LightSplitFlapBackgroundOverride;
            resources[AppSplitFlapBorderBrushKey] = LightSplitFlapBorderOverride;
        }

        private static SolidColorBrush CreateFrozenBrush(System.Windows.Media.Color color)
        {
            SolidColorBrush brush = new(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>Returns <see langword="true"/> when the Windows "Apps use light theme" registry value is 0 (dark mode on).</summary>
        public static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser
                    .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var registryValue = key?.GetValue("AppsUseLightTheme");
                if (registryValue != null)
                    return (int)registryValue == 0;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("ThemeService.IsSystemDarkMode", ex);
            }
            return false;
        }
    }
}
