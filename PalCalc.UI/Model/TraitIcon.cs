using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.Model
{
    internal static class TraitIcon
    {
        private static Dictionary<int, ImageSource> images;
        public static Dictionary<int, ImageSource> Images
        {
            get
            {
                if (images == null)
                {
                    Stream IconStream(string iconName) => ResourceLookup.Get($"TraitRank/{iconName}");
                    ImageSource IconImage(string iconName)
                    {
                        var source = new BitmapImage();
                        source.BeginInit();
                        source.StreamSource = IconStream(iconName);
                        source.EndInit();
                        return source;
                    }

                    images = new Dictionary<int, ImageSource>();

                    images.Add(-3, IconImage("Passive_Negative_3_icon.png"));
                    images.Add(-2, IconImage("Passive_Negative_2_icon.png"));
                    images.Add(-1, IconImage("Passive_Negative_1_icon.png"));

                    images.Add(0, IconImage("Passive_Positive_1_icon.png"));

                    images.Add(1, IconImage("Passive_Positive_1_icon.png"));
                    images.Add(2, IconImage("Passive_Positive_2_icon.png"));
                    images.Add(3, IconImage("Passive_Positive_3_icon.png"));
                }

                return images;
            }
        }
    }
}
