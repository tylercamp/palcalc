using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;

namespace PalCalc.UI.Model
{
    internal static class SkillElementIcon
    {
        private static Dictionary<string, ImageSource> images;
        public static Dictionary<string, ImageSource> Images
        {
            get
            {
                if (images == null)
                {
                    var result = new Dictionary<string, ImageSource>();

                    foreach (var elem in PalDB.LoadEmbedded().Elements)
                    {
                        var source = new BitmapImage();
                        source.BeginInit();
                        source.StreamSource = ResourceLookup.Get($"SkillElements/{elem.InternalName}.png");
                        source.EndInit();

                        result.Add(elem.InternalName, source);
                    }

                    images = result;
                }

                return images;
            }
        }

        private static ImageSource defaultImage;
        public static ImageSource DefaultImage => defaultImage ??= Images["Normal"];
    }
}
