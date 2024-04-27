using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.Model
{
    internal static class InternalIcons
    {
        private static BitmapImage errorIcon;
        public static ImageSource ErrorIcon => errorIcon ??= ResourceLookup.GetImage("Internal/cross.png");

        private static BitmapImage warningIcon;
        public static ImageSource WarningIcon => warningIcon ??= ResourceLookup.GetImage("Internal/warning.png");
    }
}
