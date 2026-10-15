namespace nvidiaProfileInspector.UI.Converters
{
    using nvidiaProfileInspector.Common;
    using nvidiaProfileInspector.Common.Meta;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media;


    public class StateToIconConverter : IValueConverter
    {
        private static Geometry IconGearNvidia => IconResourceCache.GetGeometry("IconGearNvidia");
        private static Geometry IconUser => IconResourceCache.GetGeometry("IconUser");
        private static Geometry IconGlobe => IconResourceCache.GetGeometry("IconGlobe");
        private static Geometry IconSettings => IconResourceCache.GetGeometry("IconSettings");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SettingState)
            {
                SettingState state = (SettingState)value;
                if (state == SettingState.NvidiaSetting)
                    return IconGearNvidia;
                if (state == SettingState.UserdefinedSetting)
                    return IconUser;
                if (state == SettingState.GlobalSetting)
                    return IconGlobe;

                return IconSettings;
            }
            return IconSettings;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SettingState)
            {
                SettingState state = (SettingState)value;
                if (state == SettingState.UserdefinedSetting)
                    return Application.Current.FindResource("TextPrimaryBrush");
                return Application.Current.FindResource("TextSecondaryBrush");
            }
            return Application.Current.FindResource("TextSecondaryBrush");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateToSettingIconColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SettingState)
            {
                SettingState state = (SettingState)value;
                if (state == SettingState.UserdefinedSetting)
                    return Application.Current.FindResource("WarningBrush");
                if (state == SettingState.NvidiaSetting)
                    return Application.Current.FindResource("NvidiaGreenBrush");
                if (state == SettingState.GlobalSetting)
                    return Application.Current.FindResource("AccentBrush");
                return Application.Current.FindResource("TextSecondaryBrush");
            }
            return Application.Current.FindResource("TextSecondaryBrush");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SettingState)
            {
                SettingState state = (SettingState)value;
                switch (state)
                {
                    case SettingState.NvidiaSetting:
                        return "Nvidia Setting - Defined by NVIDIA driver";
                    case SettingState.UserdefinedSetting:
                        return "User-defined Setting - Custom setting added by user";
                    case SettingState.GlobalSetting:
                        return "Global Setting - Applies to all applications";
                    default:
                        return "Setting";
                }
            }
            return "Setting";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool boolValue = value is bool && (bool)value;
            if (invert) boolValue = !boolValue;
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && !(bool)value;
        }
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool && (bool)value;
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Collapsed;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter != null && parameter.ToString() == "Invert";
            bool isNull = value == null;
            if (invert) isNull = !isNull;
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProfileNameToIconConverter : IValueConverter
    {
        private static readonly object _fallbackSync = new object();
        private static readonly Dictionary<string, ImageSource> _fallbackCache = new Dictionary<string, ImageSource>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                string profileName = value as string;
                bool isGlobal = false;
                bool isUser = false;

                if (value is UI.ViewModels.ProfileListItem profileItem)
                {
                    profileName = profileItem.ProfileName;
                    isGlobal = profileItem.IsGlobal;
                    isUser = profileItem.IsUserDefined;
                }
                else if (value is UI.ViewModels.ModifiedProfileItem modifiedItem)
                {
                    profileName = modifiedItem.ProfileName;
                    isUser = modifiedItem.IsUserDefined;
                }

                if (!string.IsNullOrEmpty(profileName))
                {
                    var bootstrapper = nvidiaProfileInspector.App.Bootstrapper;
                    var games = bootstrapper?.Resolve<nvidiaProfileInspector.Services.InstalledGamesService>();
                    var exeIcon = games?.GetIconForProfile(profileName);
                    if (exeIcon != null)
                        return exeIcon;
                }

                return RenderFallback(isGlobal, isUser);
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource RenderFallback(bool isGlobal, bool isUser)
        {
            string geometryKey = isUser ? "IconUser" : (isGlobal ? "IconGlobe" : "IconNvidia");
            string brushKey = isUser ? "NvidiaGreenBrush" : (isGlobal ? "AccentBrush" : "TextSecondaryBrush");

            string theme = null;
            try { theme = nvidiaProfileInspector.App.Bootstrapper?.Resolve<nvidiaProfileInspector.Services.ThemeManager>()?.CurrentTheme; }
            catch { }
            string cacheKey = theme + "|" + geometryKey + "|" + brushKey;

            lock (_fallbackSync)
            {
                ImageSource cached;
                if (_fallbackCache.TryGetValue(cacheKey, out cached))
                    return cached;
            }

            try
            {
                var geometry = IconResourceCache.GetGeometry(geometryKey);
                var brush = Application.Current?.TryFindResource(brushKey) as Brush;
                if (geometry == null || brush == null)
                    return null;

                var image = new DrawingImage(new GeometryDrawing(brush, null, geometry));
                image.Freeze();

                lock (_fallbackSync)
                {
                    _fallbackCache[cacheKey] = image;
                }
                return image;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SettingIdToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var row = value as UI.ViewModels.SettingItemViewModel;
                if (row == null)
                    return null;

                var bootstrapper = nvidiaProfileInspector.App.Bootstrapper;
                var meta = bootstrapper?.Resolve<nvidiaProfileInspector.Common.DrsSettingsMetaService>();
                if (meta == null)
                    return null;

                var description = meta.GetDescription(row.SettingId);
                description = nvidiaProfileInspector.Common.Helper.DlssHelper.ReplaceDlssVersions(description ?? "");
                description = description.Replace("\\r\\n", "\r\n");
                if (!string.IsNullOrEmpty(row.AlternateNames))
                    description = "Alternate names: " + row.AlternateNames + "\r\n" + description;

                return string.IsNullOrWhiteSpace(description) ? null : description;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string val = value as string;
            string param = parameter as string;

            if (param == "ProfileCombo")
            {
                return val == "ProfileCombo" ? Visibility.Visible : Visibility.Collapsed;
            }

            bool invert = param == "Invert";
            bool hasContent = !string.IsNullOrEmpty(val);
            if (invert) hasContent = !hasContent;
            return hasContent ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ModifiedToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isModified = false;
            if (value is bool)
                isModified = (bool)value;
            else if (value is int)
                isModified = (int)value > 0;

            if (isModified)
                return Application.Current.FindResource("TextPrimaryBrush");

            return Application.Current.FindResource("TextSecondaryBrush");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageBoxButtonToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MessageBoxButton buttons && parameter is string buttonName)
            {
                bool isVisible = buttons switch
                {
                    MessageBoxButton.OK => buttonName == "OK",
                    MessageBoxButton.OKCancel => buttonName == "OK" || buttonName == "Cancel",
                    MessageBoxButton.YesNo => buttonName == "Yes" || buttonName == "No",
                    MessageBoxButton.YesNoCancel => buttonName == "Yes" || buttonName == "No" || buttonName == "Cancel",
                    _ => false
                };
                return isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && values[0] is double value && values[1] is double maximum && values[2] is double width)
            {
                if (maximum <= 0) return 0;
                double ratio = value / maximum;
                return width * ratio;
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SettingMetaSourceToIconConverter : IValueConverter
    {
        private static Geometry IconNvidia => IconResourceCache.GetGeometry("IconNvidia");
        private static Geometry IconSettings => IconResourceCache.GetGeometry("IconSettings");
        private static Geometry IconBitEditor => IconResourceCache.GetGeometry("IconBitEditor");
        private static Geometry IconInfo => IconResourceCache.GetGeometry("IconInfo");
        private static Geometry IconSearch => IconResourceCache.GetGeometry("IconSearch");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SettingMetaSource source)
            {
                switch (source)
                {
                    case SettingMetaSource.CustomSettings:
                        return IconSettings;
                    case SettingMetaSource.DriverSettings:
                        return IconNvidia;
                    case SettingMetaSource.ConstantSettings:
                        return IconBitEditor;
                    case SettingMetaSource.ReferenceSettings:
                        return IconInfo;
                    case SettingMetaSource.ScannedSettings:
                        return IconSearch;
                }
            }
            return IconSettings;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FavoriteToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)
            {
                return isFavorite
                ? Application.Current.FindResource("WarningBrush")
                : Application.Current.FindResource("TextSecondaryBrush");
            }
            return Application.Current.FindResource("TextSecondaryBrush");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal static class IconResourceCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly System.Collections.Generic.Dictionary<string, Geometry> GeometryCache = new System.Collections.Generic.Dictionary<string, Geometry>(StringComparer.Ordinal);

        public static Geometry GetGeometry(string resourceKey)
        {
            lock (SyncRoot)
            {
                if (GeometryCache.TryGetValue(resourceKey, out var geometry))
                    return geometry;

                geometry = Application.Current?.FindResource(resourceKey) as Geometry;
                GeometryCache[resourceKey] = geometry;
                return geometry;
            }
        }
    }
}
