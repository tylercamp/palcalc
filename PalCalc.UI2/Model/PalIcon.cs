using Avalonia.Media.Imaging;
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

namespace PalCalc.UI2.Model
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

        private static Dictionary<Pal, Bitmap> images;
        public static Dictionary<Pal, Bitmap> Images
        {
            get
            {
                if (images == null)
                {
                    var overrides = GetIconOverrides();

                    images = new Dictionary<Pal, Bitmap>();
                    foreach (var pal in PalDB.LoadEmbedded().Pals)
                    {
                        var iconName = overrides.ContainsKey(pal.Name)
                            ? overrides[pal.Name]
                            : $"{pal.Name}.png";

                        var source = new Bitmap($"/Assets/Pals/{iconName}");
                        images.Add(pal, source);
                    }
                }

                return images;
            }
        }
    }
}
