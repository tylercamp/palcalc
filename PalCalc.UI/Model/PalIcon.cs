using Newtonsoft.Json;
using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.Model
{
    internal static class PalIcon
    {
        private static ILogger logger = Log.ForContext(typeof(PalIcon));

        private static Dictionary<string, string> GetIconOverrides()
        {
            using (var stream = ResourceLookup.Get("PalIconOverride.json"))
            using (var reader = new StreamReader(stream))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            }
        }

        private static Stream IconStream(string iconName)
        {
            try
            {
                return ResourceLookup.Get($"Pals/{iconName}");
            }
            catch (IOException)
            {
                // fallback for pals with missing icons
                logger.Warning("pal icon {iconName} is unavailable, using fallback", iconName);
                return ResourceLookup.Get("Pals/Human.png");
            }
        }

        private static ImageSource defaultIcon;
        public static ImageSource DefaultIcon
        {
            get
            {
                if (defaultIcon == null)
                {
                    var source = new BitmapImage();
                    source.BeginInit();
                    source.StreamSource = ResourceLookup.Get("Pals/Human.png");
                    source.EndInit();
                    defaultIcon = source;
                }
                return defaultIcon;
            }
        }

        private static ImageBrush defaultIconBrush;
        public static ImageBrush DefaultIconBrush => defaultIconBrush ??= new ImageBrush(DefaultIcon) { Stretch = Stretch.Fill };

        private static Dictionary<Pal, ImageSource> images;
        public static Dictionary<Pal, ImageSource> Images
        {
            get
            {
                if (images == null)
                {
                    var overrides = GetIconOverrides();

                    images = new Dictionary<Pal, ImageSource>();
                    foreach (var pal in PalDB.LoadEmbedded().Pals)
                    {
                        var iconName = overrides.ContainsKey(pal.Name)
                            ? overrides[pal.Name]
                            : $"{pal.Name}.png";

                        var source = new BitmapImage();
                        source.BeginInit();
                        source.StreamSource = IconStream(iconName);
                        source.EndInit();

                        images.Add(pal, source);
                    }
                }

                return images;
            }
        }

        private static Dictionary<Pal, ImageBrush> imageBrushes;
        public static Dictionary<Pal, ImageBrush> ImageBrushes => imageBrushes ??= Images.ToDictionary(kvp => kvp.Key, kvp => new ImageBrush(kvp.Value) { Stretch = Stretch.Fill });
    }
}
