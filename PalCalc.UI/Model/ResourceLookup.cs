using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.Model
{
    internal static class ResourceLookup
    {
        public static Stream Get(string pathInResources)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return File.OpenRead($"PalCalc.UI/Resources/{pathInResources}");
            }
            else
            {
                return Application.GetResourceStream(new Uri($"/Resources/{pathInResources}", UriKind.Relative)).Stream;
            }
        }

        public static BitmapImage GetImage(string pathInResources)
        {
            var res = new BitmapImage();
            res.BeginInit();
            res.StreamSource = Get(pathInResources);
            res.EndInit();
            return res;
        }
    }
}
