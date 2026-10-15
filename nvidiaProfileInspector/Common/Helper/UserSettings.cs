using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;


namespace nvidiaProfileInspector.Common.Helper
{
    public class UserSettings
    {
        public int WindowTop { get; set; }

        public int WindowLeft { get; set; }

        public int WindowWidth { get; set; }

        public int WindowHeight { get; set; }

        public WindowState WindowState { get; set; }

        public int SettingsFilterMode { get; set; } = -1;

        public List<string> HiddenSettingGroups { get; set; } = new List<string>();

        public bool DisableUpdateCheck { get; set; } = false;

        public bool DisableSplashScreen { get; set; } = false;

        public string UpdateChannel { get; set; } = "Release";

        public List<uint> FavoriteSettingIds { get; set; } = new List<uint>();

        public string Theme { get; set; } = "OnyxTheme.xaml";

        public string DisplayDensity { get; set; } = "Modern";

        public string TextRendering { get; set; } = "Modern";

        public bool IsFilterAdvancedMode { get; set; } = true;

        public bool ApplicationsCollapsed { get; set; } = false;

        public bool ShowStatusFooter { get; set; } = false;

        public bool GamesWindowMaximized { get; set; } = false;

        public double GamesWindowLeft { get; set; }

        public double GamesWindowTop { get; set; }

        public double GamesWindowWidth { get; set; }

        public double GamesWindowHeight { get; set; }

        public bool ShowDescriptions { get; set; } = true;

        public string ToolbarOrientation { get; set; } = "Top";

        public int SettingsVersion { get; set; } = 0;

        public List<string> HiddenGameExes { get; set; } = new List<string>();

        public List<string> DisabledGameSources { get; set; } = new List<string>();

        public string Win11BackdropMode { get; set; } = "Default";

        // Source filter selection (replaces the legacy SettingsFilterMode combo).
        // Setting sources decide which sources contribute setting rows.
        public bool SettingSourceCommon { get; set; } = true;

        public bool SettingSourceDriver { get; set; } = false;

        public bool SettingSourceConstants { get; set; } = false;

        public bool SettingSourceReference { get; set; } = false;

        public bool SettingSourceScan { get; set; } = false;

        // Value sources decide which sources contribute predefined values to a dropdown.
        public bool ValueSourceCommon { get; set; } = true;

        public bool ValueSourceDriver { get; set; } = false;

        public bool ValueSourceConstants { get; set; } = true;

        public bool ValueSourceReference { get; set; } = true;

        public bool ValueSourceScan { get; set; } = true;

        // Post-filter: only show settings with a user override or an unsaved edit.
        public bool ModifiedOnly { get; set; } = false;

        // When true, settings that are active in the current profile (predefined, global,
        // or user value) appear regardless of whether their setting source is enabled.
        public bool ShowActiveFromDisabledSources { get; set; } = true;

        // Value dropdown behavior: merge same-value entries across sources into one, and
        // let the predefined scan (app list) values merge into the common values too.
        public bool MergeDistinctValues { get; set; } = true;

        public bool AddPredefinedAppListToCommon { get; set; } = true;

        public bool AppendAppListToCommonNames { get; set; } = false;

        // Prefix the raw value to common (CSN) value names. Off by default.
        public bool AddRawValueToCommon { get; set; } = false;

        // Allow a setting's name and description to come from sources that are not currently
        // enabled as setting sources.
        public bool AllowMetaFromInactiveSources { get; set; } = true;

        // Prefix the setting id to the setting name in the list. Off by default.
        public bool ShowSettingIdInName { get; set; } = false;

        private static string GetSettingsFilename()
        {
            return Path.Combine(GetProductDataFolder(), "settings.xml"); ;
        }

        internal static string GetProductDataFolder()
        {
            var fiPortalbleSettings = new FileInfo("settings.xml");
            if (fiPortalbleSettings.Exists) return fiPortalbleSettings.DirectoryName;

            string productName = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(), typeof(AssemblyProductAttribute))).Product;

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), productName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public void SaveSettings()
        {
            XMLHelper<UserSettings>.SerializeToXmlFile(this, GetSettingsFilename(), Encoding.Unicode, true);
        }

        private const int CurrentSettingsVersion = 2;

        public static UserSettings LoadSettings()
        {
            var filename = GetSettingsFilename();
            if (!File.Exists(filename)) return new UserSettings();

            try
            {
                var settings = XMLHelper<UserSettings>.DeserializeFromXMLFile(GetSettingsFilename());
                if (settings.SettingsVersion < CurrentSettingsVersion)
                {
                    settings.AddPredefinedAppListToCommon = true;
                    settings.AppendAppListToCommonNames = false;
                    settings.SettingsVersion = CurrentSettingsVersion;
                    settings.SaveSettings();
                }
                return settings;
            }
            catch
            {
                return new UserSettings();
            }
        }
    }
}
