using nvidiaProfileInspector.Common.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace nvidiaProfileInspector.Services
{
    public class ThemeManager
    {
        private const string OnyxTheme = "OnyxTheme.xaml";
        private const string SlateLightTheme = "SlateLightTheme.xaml";
        private const string MidnightTheme = "MidnightTheme.xaml";
        private const string CleanWhiteTheme = "CleanWhiteTheme.xaml";
        private const string VantaTheme = "VantaTheme.xaml";
        private const string EmberTheme = "EmberTheme.xaml";

        private static readonly string[] ValidThemes = {
            MidnightTheme,
            EmberTheme,
            OnyxTheme,
            VantaTheme,
            SlateLightTheme,
            CleanWhiteTheme
        };

        private static readonly string[] ValidDensities = { "Modern", "Compact", "Classic" };
        private static readonly string[] ValidTextRenderings = { "Modern", "Classic" };

        public string CurrentTheme { get; private set; }
        public string CurrentDensity { get; private set; } = "Modern";
        public string CurrentTextRendering { get; private set; } = "Modern";

        public bool IsCompactDensity => string.Equals(CurrentDensity, "Compact", StringComparison.OrdinalIgnoreCase);
        public bool IsClassicDensity => string.Equals(CurrentDensity, "Classic", StringComparison.OrdinalIgnoreCase);
        public bool IsClassicTextRendering => string.Equals(CurrentTextRendering, "Classic", StringComparison.OrdinalIgnoreCase);

        public event Action<string> ThemeChanged;

        public ThemeManager()
        {
            LoadSavedTheme();
        }

        public void LoadSavedTheme()
        {
            try
            {
                var settings = UserSettings.LoadSettings();
                ApplyAndPersistTheme(NormalizeThemeName(settings.Theme), savePreference: false);
                ApplyDensity(NormalizeDensity(settings.DisplayDensity), savePreference: false);
                ApplyTextRendering(NormalizeTextRendering(settings.TextRendering), savePreference: false);
            }
            catch
            {
                ApplyAndPersistTheme(OnyxTheme, savePreference: false);
                ApplyDensity("Modern", savePreference: false);
                ApplyTextRendering("Modern", savePreference: false);
            }
        }

        public void ToggleTheme()
        {
            try
            {
                var currentTheme = GetCurrentAppliedThemeName();
                ApplyAndPersistTheme(GetNextThemeName(currentTheme), savePreference: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling theme: {ex.Message}");
            }
        }

        public void SetTheme(string themeName)
        {
            try
            {
                var normalized = NormalizeThemeName(themeName);
                ApplyAndPersistTheme(normalized, savePreference: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting theme: {ex.Message}");
            }
        }

        public void SetDensity(string density)
        {
            ApplyDensity(NormalizeDensity(density), savePreference: true);
        }

        public void SetTextRendering(string mode)
        {
            ApplyTextRendering(NormalizeTextRendering(mode), savePreference: true);
        }

        public void ApplyTextRenderingTo(Window window)
        {
            ApplyTextRenderingToWindow(window, CurrentTextRendering);
        }

        private void ApplyDensity(string density, bool savePreference)
        {
            CurrentDensity = density;

            var app = Application.Current;
            if (app == null)
                return;

            var compact = string.Equals(density, "Compact", StringComparison.OrdinalIgnoreCase);
            var classic = string.Equals(density, "Classic", StringComparison.OrdinalIgnoreCase);

            if (classic)
            {
                app.Resources["ListItemPadding"] = new Thickness(1, 0, 1, 0);
                app.Resources["ListItemMargin"] = new Thickness(0, 0, 0, 0);
                app.Resources["ComboBoxItemPadding"] = new Thickness(12, 2, 12, 2);
                app.Resources["ComboBoxItemMargin"] = new Thickness(2, 0, 2, 0);
                app.Resources["GroupHeaderOuterMargin"] = new Thickness(0, 2, 0, 1);
                app.Resources["GroupHeaderPadding"] = new Thickness(4, 1, 4, 2);
                app.Resources["GroupItemsPresenterMargin"] = new Thickness(8, 1, 8, 1);
                app.Resources["FlatGroupHeaderMargin"] = new Thickness(0, 4, 0, 1);
                app.Resources["FlatSettingRowMargin"] = new Thickness(4, 0, 4, 0);
            }
            else
            {
                app.Resources["ListItemPadding"] = compact ? new Thickness(2, 1, 2, 1) : new Thickness(4, 4, 4, 4);
                app.Resources["ListItemMargin"] = compact ? new Thickness(0, 1, 0, 1) : new Thickness(0, 2, 0, 2);
                app.Resources["ComboBoxItemPadding"] = compact ? new Thickness(12, 5, 12, 5) : new Thickness(12, 8, 12, 8);
                app.Resources["ComboBoxItemMargin"] = compact ? new Thickness(2, 0, 2, 0) : new Thickness(2, 1, 2, 1);
                app.Resources["GroupHeaderOuterMargin"] = compact ? new Thickness(0, 4, 0, 2) : new Thickness(0, 8, 0, 4);
                app.Resources["GroupHeaderPadding"] = compact ? new Thickness(4, 2, 4, 4) : new Thickness(4, 4, 4, 8);
                app.Resources["GroupItemsPresenterMargin"] = compact ? new Thickness(8, 2, 8, 2) : new Thickness(8, 4, 8, 4);
                app.Resources["FlatGroupHeaderMargin"] = compact ? new Thickness(0, 8, 0, 2) : new Thickness(0, 16, 0, 4);
                app.Resources["FlatSettingRowMargin"] = compact ? new Thickness(4, 1, 4, 1) : new Thickness(4, 2, 4, 2);
            }

            if (savePreference)
                SaveDensityPreference(density);
        }

        private void ApplyTextRendering(string mode, bool savePreference)
        {
            CurrentTextRendering = mode;

            var app = Application.Current;
            if (app?.Windows != null)
            {
                foreach (Window window in app.Windows)
                    ApplyTextRenderingToWindow(window, mode);
            }
            // MainWindow may not exist yet when the saved settings load.
            if (app?.MainWindow != null)
                ApplyTextRenderingToWindow(app.MainWindow, mode);

            if (savePreference)
                SaveTextRenderingPreference(mode);
        }

        private static void ApplyTextRenderingToWindow(Window window, string mode)
        {
            if (window == null)
                return;

            var classic = string.Equals(mode, "Classic", StringComparison.OrdinalIgnoreCase);
            if (classic)
            {
                TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
                TextOptions.SetTextHintingMode(window, TextHintingMode.Fixed);
                TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);
                RenderOptions.SetClearTypeHint(window, ClearTypeHint.Enabled);
            }
            else
            {
                window.ClearValue(TextOptions.TextFormattingModeProperty);
                window.ClearValue(TextOptions.TextHintingModeProperty);
                window.ClearValue(TextOptions.TextRenderingModeProperty);
                window.ClearValue(RenderOptions.ClearTypeHintProperty);
            }
        }

        private static string NormalizeTextRendering(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return "Modern";

            return ValidTextRenderings.FirstOrDefault(d =>
                       string.Equals(d, mode, StringComparison.OrdinalIgnoreCase))
                   ?? "Modern";
        }

        private static string NormalizeDensity(string density)
        {
            if (string.IsNullOrWhiteSpace(density))
                return "Modern";

            return ValidDensities.FirstOrDefault(d =>
                       string.Equals(d, density, StringComparison.OrdinalIgnoreCase))
                   ?? "Modern";
        }

        private void SaveThemePreference(string themeName)
        {
            try
            {
                var settings = UserSettings.LoadSettings();
                settings.Theme = themeName;
                settings.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving theme preference: {ex.Message}");
            }
        }

        private void SaveDensityPreference(string density)
        {
            try
            {
                var settings = UserSettings.LoadSettings();
                settings.DisplayDensity = density;
                settings.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving density preference: {ex.Message}");
            }
        }

        private void SaveTextRenderingPreference(string mode)
        {
            try
            {
                var settings = UserSettings.LoadSettings();
                settings.TextRendering = mode;
                settings.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving text rendering preference: {ex.Message}");
            }
        }

        private static ResourceDictionary GetThemeDictionary(IList<ResourceDictionary> mergedDicts)
        {
            return mergedDicts.FirstOrDefault(d => GetThemeName(d.Source) != null);
        }

        private static string GetCurrentAppliedThemeName()
        {
            var app = Application.Current;
            if (app == null)
                return null;

            return GetThemeName(GetThemeDictionary(app.Resources.MergedDictionaries)?.Source);
        }

        private static string GetThemeName(Uri source)
        {
            if (source == null)
                return null;

            var themeName = Path.GetFileName(source.OriginalString);
            return ValidThemes.FirstOrDefault(validTheme =>
                string.Equals(validTheme, themeName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeThemeName(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                return OnyxTheme;

            if (!themeName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                return OnyxTheme;

            // Legacy aliases from retired theme names.
            if (string.Equals(themeName, "DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
                return OnyxTheme;
            if (string.Equals(themeName, "AmoledTheme.xaml", StringComparison.OrdinalIgnoreCase))
                return VantaTheme;

            return ValidThemes.FirstOrDefault(validTheme =>
                       string.Equals(validTheme, themeName, StringComparison.OrdinalIgnoreCase))
                   ?? OnyxTheme;
        }

        private static string GetNextThemeName(string currentTheme)
        {
            if (ValidThemes.Length == 0)
                return OnyxTheme;

            if (string.IsNullOrWhiteSpace(currentTheme))
                return ValidThemes[0];

            var currentThemeIndex = Array.FindIndex(
                ValidThemes,
                theme => string.Equals(theme, currentTheme, StringComparison.OrdinalIgnoreCase));

            if (currentThemeIndex < 0)
                return ValidThemes[0];

            var nextThemeIndex = (currentThemeIndex + 1) % ValidThemes.Length;
            return ValidThemes[nextThemeIndex];
        }

        private void ApplyAndPersistTheme(string themeName, bool savePreference)
        {
            var app = Application.Current;
            if (app == null)
                return;

            var normalizedThemeName = NormalizeThemeName(themeName);
            var mergedDicts = app.Resources.MergedDictionaries;
            var themeDict = GetThemeDictionary(mergedDicts);

            ReplaceThemeDictionary(mergedDicts, themeDict, normalizedThemeName);

            CurrentTheme = normalizedThemeName;

            ThemeChanged?.Invoke(normalizedThemeName);

            if (savePreference)
                SaveThemePreference(normalizedThemeName);
        }

        private static void ReplaceThemeDictionary(IList<ResourceDictionary> mergedDicts, ResourceDictionary existingTheme, string themeName)
        {
            var newThemeDictionary = new ResourceDictionary
            {
                Source = new Uri($"/UI/Themes/{themeName}", UriKind.Relative)
            };

            if (existingTheme == null)
            {
                mergedDicts.Add(newThemeDictionary);
                return;
            }

            var index = mergedDicts.IndexOf(existingTheme);
            mergedDicts.RemoveAt(index);
            mergedDicts.Insert(index, newThemeDictionary);
        }
    }
}
