using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PalCalc.UI2.Model
{
    internal static class ResourceLookup
    {
        public static Stream Get(string pathInResources)
        {
            if (Design.IsDesignMode)
            {
                return File.OpenRead($"PalCalc.UI2/Resources/{pathInResources}");
            }
            else
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                return asm.GetManifestResourceStream(asm.GetName().Name + ".Properties." + pathInResources);
            }
        }
    }
}
