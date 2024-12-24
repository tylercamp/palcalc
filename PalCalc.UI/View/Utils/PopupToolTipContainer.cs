using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.View.Utils
{
    public class PopupToolTipContainer : Border
    {
        public PopupToolTipContainer()
        {
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(2);
            Background = SystemColors.ControlBrush;
            BorderBrush = SystemColors.ControlDarkBrush;
            Padding = new Thickness(5);
        }
    }
}
