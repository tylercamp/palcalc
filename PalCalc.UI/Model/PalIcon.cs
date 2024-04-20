using Newtonsoft.Json;
using PalCalc.Model;
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
        private static Dictionary<string, string> GetIconOverrides()
        {
            using (var stream = Application.GetResourceStream(new Uri("/Resources/PalIconOverride.json", UriKind.Relative)).Stream)
            using (var reader = new StreamReader(stream))
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            }
        }

        private static Stream IconStream(string iconName)
        {
            try
            {
                var uri = new Uri($"/Resources/Pals/{iconName}", UriKind.Relative);
                return Application.GetResourceStream(uri).Stream;
            }
            catch (IOException)
            {
                // fallback for pals with missing icons
                // TODO - log
                var uri = new Uri("/Resources/Pals/Human.png", UriKind.Relative);
                return Application.GetResourceStream(uri).Stream;
            }
        }

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
    }
}
