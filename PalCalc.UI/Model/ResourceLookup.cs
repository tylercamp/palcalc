using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
    }
}
