using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.View.Utils
{
    public class PopupToolTipContent : Border
    {
        static PopupToolTipContent()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PopupToolTipContent), new FrameworkPropertyMetadata(typeof(PopupToolTipContent)));
        }
    }
}
