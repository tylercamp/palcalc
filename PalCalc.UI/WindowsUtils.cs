using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI
{
    internal static class WindowsUtils
    {
        public static void OpenPathInExplorer(string path)
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            Process.Start("explorer.exe", fullPath);
        }
    }
}
