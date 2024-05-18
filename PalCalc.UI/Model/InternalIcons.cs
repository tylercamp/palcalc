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
        private static BitmapImage folderIcon;
        public static ImageSource FolderIcon => folderIcon ??= ResourceLookup.GetImage("Internal/folder.png");

        private static BitmapImage deleteIcon;
        public static ImageSource DeleteIcon => deleteIcon ??= ResourceLookup.GetImage("Internal/delete.png");
    }
}
