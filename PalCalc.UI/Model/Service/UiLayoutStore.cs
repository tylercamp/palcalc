using Serilog;

namespace PalCalc.UI.Model.Service
{
    internal static class UILayoutStore
    {
        private static readonly ILogger logger = Log.ForContext(typeof(UILayoutStore));
        private static bool suppressSaves;

        public static bool TryGetWindowPlacement(string key, out WindowPlacementSettings placement)
        {
            placement = null;

            var settings = AppSettings.Current;
            if (settings == null || string.IsNullOrWhiteSpace(key))
                return false;

            settings.UiLayout ??= new UiLayoutSettings();
            settings.UiLayout.Windows ??= new();

            return settings.UiLayout.Windows.TryGetValue(key, out placement);
        }

        public static bool TryGetGridLayout(string key, out GridLayoutSettings layout)
        {
            layout = null;

            var settings = AppSettings.Current;
            if (settings == null || string.IsNullOrWhiteSpace(key))
                return false;

            settings.UiLayout ??= new UiLayoutSettings();
            settings.UiLayout.Grids ??= new();

            return settings.UiLayout.Grids.TryGetValue(key, out layout);
        }

        public static void SaveWindowPlacement(string key, WindowPlacementSettings placement)
        {
            var settings = AppSettings.Current;
            if (suppressSaves ||
                settings == null ||
                string.IsNullOrWhiteSpace(key) ||
                placement == null)
                return;

            settings.UiLayout ??= new UiLayoutSettings();
            settings.UiLayout.Windows ??= new();
            settings.UiLayout.Windows[key] = placement;

            try
            {
                Storage.SaveAppSettings(settings);
            }
            catch (System.Exception ex)
            {
                logger.Warning(ex, "Unable to save window placement for {WindowLayoutKey}", key);
            }
        }

        public static void SaveGridLayout(string key, GridLayoutSettings layout)
        {
            var settings = AppSettings.Current;
            if (suppressSaves ||
                settings == null ||
                string.IsNullOrWhiteSpace(key) ||
                layout == null)
                return;

            settings.UiLayout ??= new UiLayoutSettings();
            settings.UiLayout.Grids ??= new();
            settings.UiLayout.Grids[key] = layout;
            SaveSettings(settings, "grid layout", key);
        }

        public static void Reset()
        {
            var settings = AppSettings.Current;
            if (settings == null)
                return;

            settings.UiLayout = new UiLayoutSettings();

            // Open windows and controls capture state as they close or are dragged. Suppress
            // those writes for the remainder of this session so they cannot undo the reset.
            suppressSaves = true;
            SaveSettings(settings, "UI layouts", null);
        }

        private static void SaveSettings(AppSettings settings, string description, string key)
        {
            try
            {
                Storage.SaveAppSettings(settings);
            }
            catch (System.Exception ex)
            {
                logger.Warning(
                    ex,
                    "Unable to save {LayoutDescription} {LayoutKey}",
                    description,
                    key
                );
            }
        }
    }
}
