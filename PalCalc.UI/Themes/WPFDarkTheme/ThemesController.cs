using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Theme.WPF.Themes
{
    // TODO - this is currently sensitive to the order of dictionaries in App.xaml
    public static class ThemesController
    {
        private static ThemeType? currentTheme = null;
        public static ThemeType CurrentTheme
        {
            get
            {
                if (currentTheme == null)
                {
                    var themePath = ThemeDictionary.Source.OriginalString;
                    var themeName = Path.GetFileNameWithoutExtension(themePath);

                    foreach (var theme in Enum.GetValues<ThemeType>())
                    {
                        if (themeName == theme.GetName())
                        {
                            currentTheme = theme;
                            break;
                        }
                    }

                    currentTheme ??= ThemeType.None;
                }

                return currentTheme.Value;
            }

            private set
            {
                currentTheme = value;
            }
        }

        public static bool IsNativeTheme =>
            DesignerProperties.GetIsInDesignMode(new DependencyObject())
                ? File.ReadAllText("PalCalc.UI/App.xaml").Contains("NoTheme")
                : CurrentTheme == ThemeType.None;
        public static bool IsCustomTheme => !IsNativeTheme;

        private static ResourceDictionary ThemeDictionary
        {
            get => Application.Current.Resources.MergedDictionaries[0];
            set => Application.Current.Resources.MergedDictionaries[0] = value;
        }

        private static ResourceDictionary ControlColours
        {
            get => Application.Current.Resources.MergedDictionaries[1];
            set => Application.Current.Resources.MergedDictionaries[1] = value;
        }

        private static void RefreshControls(bool clearTheme)
        {
            Collection<ResourceDictionary> merged = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary dictionary = merged[2];
            merged.RemoveAt(2);

            // This seems to be faster than reloading the whole file, and it also seems to work
            if (clearTheme)
            {
                merged.Insert(2, new ResourceDictionary() { Source = new Uri($"Themes/WPFDarkTheme/DefaultControls.xaml", UriKind.Relative) });
            }
            else
            {
                merged.Insert(2, new ResourceDictionary() { Source = new Uri($"Themes/WPFDarkTheme/Controls.xaml", UriKind.Relative) });
            }

            // If the above doesn't work then fall back to this
            // Application.Current.Resources.MergedDictionaries[2] = new ResourceDictionary() { Source = new Uri("Themes/Controls.xaml", UriKind.Relative) };

            // Doesn't work
            // FieldInfo field = typeof(PropertyMetadata).GetField("_defaultValue", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField);
            // object style = merged[2][typeof(ComboBox)];
            // field.SetValue(DataGridComboBoxColumn.ElementStyleProperty.DefaultMetadata, style);
        }

        /// <summary>
        /// Attempts to set the given theme.
        /// </summary>
        /// <remarks>
        /// This function is sensitive to the "type" of theme - system or customized - where a change from one to the
        /// other requires an app restart to apply correctly.
        /// </remarks>
        /// <param name="forceTheme">If true, forcibly applies the theme. If false, only applies the theme if it's the same "type" as the active theme.</param>
        /// <returns>Whether the theme has been applied.</returns>
        public static bool SetTheme(ThemeType theme, bool forceTheme)
        {
            string themeName = theme.GetName();
            if (string.IsNullOrEmpty(themeName))
            {
                return false;
            }

            if (theme == CurrentTheme) return true;

            var changedThemeType = (
                (CurrentTheme == ThemeType.None) != (theme == ThemeType.None)
            );

            if (changedThemeType && !forceTheme) return false;

            CurrentTheme = theme;
            ThemeDictionary = new ResourceDictionary() { Source = new Uri($"Themes/WPFDarkTheme/ColourDictionaries/{themeName}.xaml", UriKind.Relative) };
            ControlColours = new ResourceDictionary() { Source = new Uri("Themes/WPFDarkTheme/ControlColours.xaml", UriKind.Relative) };

            RefreshControls(clearTheme: theme == ThemeType.None);

            return true;
        }

        public static object GetResource(object key)
        {
            return ThemeDictionary[key];
        }

        public static SolidColorBrush GetBrush(string name)
        {
            return GetResource(name) is SolidColorBrush brush ? brush : new SolidColorBrush(Colors.White);
        }
    }
}